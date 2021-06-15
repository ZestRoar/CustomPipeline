using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Mad.Core.Concurrent.Synchronization;

namespace CustomPipelines
{
    internal class CustomPipeBuffer
    {
#nullable enable

        // 동기화 오브젝트
        private readonly object syncPushPop = new object();
        private readonly object syncReadTail = new object();


        // 세그먼트 풀 & 옵션
        private CustomBufferSegmentStack customBufferSegmentPool;
        private readonly CustomPipeOptions options;

        // 인덱싱 갱신 용(바이트 더하기)
        private long unconsumedBytes;
        private long uncommittedBytes;
        private int writingHeadBytesBuffered;
        private bool advanceAwait;

        // 메모리 관리용 데이터, 마지막 위치를 인덱싱하며 해제 바이트 측정에 사용
        private long lastExaminedIndex = -1;

        // 읽기 중인 메모리의 시작 부분을 나타냄
        private CustomBufferSegment? readHead;
        private int readHeadIdx;

        // 읽기 가능한 메모리의 끝 부분을 나타냄
        private CustomBufferSegment? readTail;
        private int readTailIdx;

        // 쓰기 중인 바이트 범위 중 시작 부분을 나타냄
        private CustomBufferSegment? writingHead;
        private Memory<byte> writingHeadMemory;

        // 콜백 등록 용도
        private int readTargetBytes;
        internal Signal WriteSignal;
        internal Future<ReadResult> ReadPromise;


        public CustomPipeBuffer(CustomPipeOptions options)
        {
            this.options = options;
            this.customBufferSegmentPool = new CustomBufferSegmentStack(options);
            this.readTargetBytes = -1;
            this.CanWrite = true;
            this.CanRead = true;
            this.WriteSignal = new Signal();
            this.ReadPromise = new Future<ReadResult>();
            this.lastExaminedIndex = 0;
            this.advanceAwait = false;
        }

        public Memory<byte> Memory => this.writingHeadMemory;
        public ReadOnlySequence<byte> ReadBuffer 
            => (this.readHead == null) || (this.readTail == null) ? default
               : new ReadOnlySequence<byte>(
                   this.readHead, this.readHeadIdx,
                   this.readTail, this.readTailIdx);

        public long UnconsumedBytes  => this.unconsumedBytes;
        public bool CheckReadable() 
            => (this.unconsumedBytes >= this.readTargetBytes);
        public bool CheckWritingOutOfRange(int bytes) 
            => (uint)bytes > (uint)this.writingHeadMemory.Length;
        public bool CheckAnyUncommittedBytes() 
            => this.uncommittedBytes > 0;
        public bool CheckWritable(int sizeHint)
            => this.writingHeadMemory.Length >= sizeHint;

        public int CalcBytesShortWrite(int sizeHint)
            => Math.Max(0,sizeHint - this.writingHeadMemory.Length);

        // ======================================================== Callback

        public bool CanWrite { get; set; }
        public bool CanRead { get; set; }

        public void SetResult(ReadResult result)
        {
            this.ReadPromise.SetResult(result);
            this.CanRead = true;
            this.ReadPromise = new Future<ReadResult>();
            Console.WriteLine($"버퍼 : {this.ReadBuffer.Length.ToString()} " +
                              $"( this.unconsumedBytes({this.unconsumedBytes.ToString()}) >= this.readTargetBytes({this.readTargetBytes.ToString()}) : {this.CheckReadable().ToString()})");
        }
        public void RegisterTarget(int targetBytes)
        {
            this.readTargetBytes = targetBytes;
            this.CanRead = false;
        }

        // ======================================================== Advance & Commit

        public void Advance(int bytesWritten)
        {
            AdvanceFrom(bytesWritten);

            WriteSignal.Reset();

            this.advanceAwait = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AdvanceFrom(int bytesWritten)
        {
            this.uncommittedBytes += bytesWritten;
            this.writingHeadBytesBuffered += bytesWritten;
            this.writingHeadMemory = this.writingHeadMemory[bytesWritten..];
        }

        public bool Commit()
        {
            if (this.writingHead == null)   // 쓰기버퍼 비어있는 상태로 complete 호출 시 발생
            {
                return true;
            }

            // Advance로 인한 구간 증가 적용
            this.CommitWritingHead();
            
            lock (syncReadTail)
            {
                // Flush로 인한 read 구간 증가 적용 
                this.readTail = this.writingHead;
                this.readTailIdx = this.writingHead.End;
            }

            var oldLength = Interlocked.Exchange(ref this.unconsumedBytes, this.unconsumedBytes+this.uncommittedBytes);
            this.uncommittedBytes = 0;
            
            if (!this.CanWrite)
            {
                return this.CanWrite;
            }

            if (this.options.CheckPauseWriter(oldLength, this.unconsumedBytes))
            {
                this.CanWrite = false;
            }
            else
            {
                this.WriteSignal.Set();
            }

            return this.CanWrite;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CommitWritingHead()
        {
            this.writingHead!.End += this.writingHeadBytesBuffered;
            this.writingHeadBytesBuffered = 0;
        }

        // ======================================================== Allocate

        public int AllocateWriteHead(int sizeHint)  // 부족했던 바이트 반환
        {
            this.advanceAwait = true;       // 쓰기 시도가 감지되어 Advance 대기 중임을 표시

            if (this.CheckWritable(sizeHint))
            {
                return 0;
            }

            int bytesShorts = this.CalcBytesShortWrite(sizeHint);

            if (this.writingHead == null)
            {
                InitFirstSegment(sizeHint);
            }
            else 
            {
                this.CommitWritingHead();   // 써 놓은거 커밋해서 

                CustomBufferSegment newSegment = AllocateSegment(sizeHint);

                this.writingHead!.SetNext(newSegment);
                this.writingHead = newSegment;
            }
            
            return bytesShorts;
        }

        private CustomBufferSegment AllocateSegment(int sizeHint)
        {
            CustomBufferSegment newSegment;
            lock (syncPushPop)
            {
                newSegment = this.customBufferSegmentPool.TryPop();
            }

            int sizeToRequest = Math.Max(this.options.MinimumSegmentSize, sizeHint);

            newSegment.SetOwnedMemory(ArrayPool<byte>.Shared.Rent(sizeToRequest));

            this.writingHeadMemory = newSegment.AvailableMemory;

            return newSegment;     
        }

        // 큰 쓰기 작업을 해야할 때 추가적인 세그먼트 할당이 필요하면 진행
        public void WriteMultiSegment(ReadOnlySpan<byte> source)
        {
            if (this.writingHead == null)
            {
                throw new NullReferenceException("writingHead is null");
            }
            
            var destination = this.writingHeadMemory.Span;

            while (true)
            {
                var writable = Math.Min(destination.Length, source.Length);
                source[..writable].CopyTo(destination);
                source = source[writable..];
                AdvanceFrom(writable);

                if (source.Length == 0)
                {
                    break;
                }

                CommitWritingHead();

                // 메모리 풀 사용을 위해 할당만 요청
                var newSegment = AllocateSegment(0);

                this.writingHead!.SetNext(newSegment);
                this.writingHead = newSegment;

                destination = this.writingHeadMemory.Span;
            }
            WriteSignal.Reset();
            this.advanceAwait = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InitFirstSegment(int sizeHint)
        {
            CustomBufferSegment newSegment = AllocateSegment(sizeHint);

            this.writingHead = this.readHead = this.readTail = newSegment;
        }

        // ======================================================== AdvanceTo

        public void AdvanceTo(ref SequencePosition consumePosition)
        {
            var consumedSegment = (CustomBufferSegment?)consumePosition.GetObject();
            var consumedIndex = consumePosition.GetInteger();

            // 메모리 시퀀스 갱신
            UpdateBuffer(ref consumedSegment, ref consumedIndex);

            // 소비된 메모리 해제
            UnlinkUsedMemory(ref consumedSegment, ref consumedIndex);

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateBuffer(ref CustomBufferSegment? examinedSegment, ref int examinedIndex)
        {
            if (examinedSegment == null || this.lastExaminedIndex < 0)
            {
                return;
            }

            var examinedBytes = CustomBufferSegment.GetLength(
                this.lastExaminedIndex, examinedSegment, examinedIndex);

            if (examinedBytes < 0)
            {
                throw new InvalidOperationException();
            }

            var oldLength = Interlocked.Exchange(ref this.unconsumedBytes, this.unconsumedBytes-examinedBytes);

            // 인덱스 절대값
            this.lastExaminedIndex = examinedSegment.RunningIndex + examinedIndex;

            Debug.Assert(this.unconsumedBytes >= 0, "GetUnconsumedBytes has gone negative");

            this.ValidateWriteIfAwait(oldLength);

            return;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ValidateWriteIfAwait(long oldLength)
        {
            if ((this.CanWrite == false) && this.options.CheckResumeWriter(oldLength, this.unconsumedBytes))
            {
                this.CanWrite = true;
                this.WriteSignal.Set();
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UnlinkUsedMemory(ref CustomBufferSegment? consumedSegment, ref int consumedIndex)
        {
            CustomBufferSegment? returnStart = null;
            CustomBufferSegment? returnEnd = null;

            if (consumedSegment == null)
            {
                return;
            }

            returnStart = readHead ?? throw new InvalidOperationException();
            returnEnd = consumedSegment;

            if (consumedIndex == returnEnd.Length)  // 세그먼트 전체를 소비했으면
            {
                // 쓰기중인 버퍼가 아니면 다음 블록으로
                if (this.writingHead != returnEnd)
                {
                    MoveReturnEndToNextBlock(ref returnEnd);    // 다음 블록 할당
                }
                // Advance가 끝났고, 펜딩된 쓰기 작업이 없으면 블록 전체 해제
                else if (this.advanceAwait == false && this.writingHeadBytesBuffered == 0)
                {

                    // 블록이 해제될 것이므로 메모리 끊어놓기
                    this.writingHead = null;
                    this.writingHeadMemory = default;
                    this.lastExaminedIndex = 0;

                    MoveReturnEndToNextBlock(ref returnEnd); // 모두 null로

                    // 이 상태에서 정상 종료가 가능
                }
                else
                {
                    this.readHead = consumedSegment;
                    this.readHeadIdx = consumedIndex;
                }
            }
            else
            {
                this.readHead = consumedSegment;
                this.readHeadIdx = consumedIndex;
            }

            // 소비된 메모리 반환 작업
            PushMemory(ref returnStart, ref returnEnd);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PushMemory(ref CustomBufferSegment? returnStart, ref CustomBufferSegment? returnEnd)
        {
            while (returnStart != null && returnStart != returnEnd)
            {
                var next = returnStart.NextSegment;
                returnStart.ResetMemory();

                Debug.Assert(returnStart != this.readHead,
                    "Returning _readHead segment that's in use!");
                Debug.Assert(returnStart != this.readTail,
                    "Returning _readTail segment that's in use!");
                Debug.Assert(returnStart != this.writingHead,
                    "Returning _writingHead segment that's in use!");

                lock (syncPushPop)
                {
                    this.customBufferSegmentPool.Push(returnStart);
                }

                returnStart = next;
            }
        }

        private void MoveReturnEndToNextBlock(ref CustomBufferSegment? returnEnd)
        {
            var nextBlock = returnEnd!.NextSegment;
            lock (syncReadTail)
            {
                if (this.readTail == returnEnd)
                {
                    this.readTail = nextBlock;
                    this.readTailIdx = 0;
                }
            }

            this.readHead = nextBlock;
            this.readHeadIdx = 0;

            returnEnd = nextBlock;
        }

        // ======================================================== Complete
        public void Reset()
        {
            this.readTailIdx = 0;
            this.readHeadIdx = 0;
            this.lastExaminedIndex = -1;
            this.uncommittedBytes = 0;
            this.unconsumedBytes = 0;
            this.readTargetBytes = -1;
            this.CanWrite = true;
            this.CanRead = true;
        }

        public void Complete()
        {
            // 세그먼트 전부 반환
            CustomBufferSegment? segment = readHead ?? readTail;
            while (segment != null)
            {
                CustomBufferSegment returnSegment = segment;
                segment = segment.NextSegment;

                returnSegment.ResetMemory();
            }

            this.writingHead = null;
            this.writingHeadMemory = default;
            this.readHead = null;
            this.readTail = null;
            this.lastExaminedIndex = -1;
            this.readTargetBytes = -1;
        }

    }
}
