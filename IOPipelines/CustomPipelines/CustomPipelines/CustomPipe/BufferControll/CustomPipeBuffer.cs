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
        private readonly object syncWriterThreshold = new object();
        private readonly object syncTailCommit = new object();

        // 세그먼트 풀 & 옵션
        private CustomBufferSegmentStack customBufferSegmentPool;
        private readonly CustomPipeOptions options;

        // 인덱싱 갱신 용(바이트 더하기)
        private long unconsumedBytes;
        private long uncommittedBytes;
        private int writingHeadBytesBuffered;

        // 메모리 관리용 데이터, 마지막 위치를 인덱싱하며 해제 바이트 측정에 사용
        private long lastExaminedIndex = -1;

        // 읽기 중인 메모리의 시작 부분을 나타냄
        private CustomBufferSegment? readHead;
        private int readHeadIdx;

        // 읽기 가능한 메모리의 끝 부분을 나타냄
        private CustomBufferSegment? readTail;
        private int readTailIdx;

        // 쓰기 중인 바이트 범위 중 시작 부분을 나타냄
        private CustomBufferSegment? writingHeadSegment;
        private Memory<byte> writingHeadMemory;

        // 콜백 등록 용도
        private int readTargetBytes;
        internal Signal WriteSignal;
        internal Future<ReadResult> ReadPromise;
        private bool canRead = true;
        private bool canWrite = true;


        public CustomPipeBuffer(CustomPipeOptions options)
        {
            this.options = options;
            this.customBufferSegmentPool = new CustomBufferSegmentStack(options);
            this.readTargetBytes = -1;
            this.canWrite = true;
            this.canRead = true;
            this.WriteSignal = new Signal();
            this.ReadPromise = new Future<ReadResult>();
            this.lastExaminedIndex = 0;
        }

        public Memory<byte> Memory => this.writingHeadMemory;
        public ReadOnlySequence<byte> ReadBuffer 
            => (this.readHead == null) || (this.readTail == null) || this.unconsumedBytes<=0 ? default
               : new ReadOnlySequence<byte>(
                   this.readHead, this.readHeadIdx,
                   this.readTail, this.readTailIdx);

        public long UnconsumedBytes  => this.unconsumedBytes;
        public bool CheckReadable()
            => (this.unconsumedBytes >= this.readTargetBytes);
        public bool CheckReadableIfCommit()
            => (this.unconsumedBytes+this.uncommittedBytes >= this.readTargetBytes);
        public bool CheckWritingOutOfRange(int bytes) 
            => (uint)bytes > (uint)this.writingHeadMemory.Length;
        public bool CheckWritable(int sizeHint)
            => this.writingHeadMemory.Length >= sizeHint;

        public int CalcBytesShortWrite(int sizeHint)
            => Math.Max(0,sizeHint - this.writingHeadMemory.Length);

        // ======================================================== Callback

        public bool CanWrite => this.canWrite;

        public bool CheckTarget(long bufferLength, int targetBytes)
        {
            this.readTargetBytes = targetBytes;

            return (bufferLength >= targetBytes);
        }
        public void RegisterTarget(int targetBytes)
        {
            this.readTargetBytes = targetBytes;
        }

        public void RequestRead()
        {
            this.canRead = false;
        }

        // ======================================================== Advance & Commit

        public bool Advance(int bytesWritten)
        {
            // 쓰기를 한 메모리 이상으로 커밋 불가능
            if (this.CheckWritingOutOfRange(bytesWritten))
            {
                throw new ArgumentOutOfRangeException();
            }

            AdvanceFrom(bytesWritten);

            if (DebugManager.consoleDump)
            {
                Console.WriteLine(
                    $"Advance : {bytesWritten.ToString()} ( {this.uncommittedBytes - bytesWritten} => {this.uncommittedBytes} )");
            }

            if (!this.canWrite)
            {
                return this.canWrite;
            }

            this.PauseWriterIfThreshold(out var writeLocked);

            bool readableIfCommit = false;

            if (!canRead)
            {
                readableIfCommit = CheckReadableIfCommit();     // 커밋할 명분이 충족되어야 함
            }

            if (DebugManager.consoleDump)
            {
                if (readableIfCommit)
                {
                    Console.WriteLine("target readable!");
                }
            }

            return writeLocked || readableIfCommit ? this.Commit() : this.canWrite;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PauseWriterIfThreshold(out bool writeLocked)
        {
            lock (syncWriterThreshold)  // Pause 중 AdvanceTo로 인한 Resume 누락을 방지
            {
                this.WriteSignal.Reset();
                
                writeLocked = this.options.CheckPauseWriter(this.unconsumedBytes + this.uncommittedBytes);
                if (writeLocked)
                {
                    Console.WriteLine($"Write Locked! : {this.unconsumedBytes.ToString()} + {this.uncommittedBytes.ToString()}");
                    this.canWrite = false;
                }
                else
                {
                    this.WriteSignal.Set();
                }
            }
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
            if (this.writingHeadSegment == null)   // 쓰기버퍼 비어있는 상태로 complete 호출 시 발생
            {
                return true;
            }

            // Advance로 인한 구간 증가 적용
            this.CommitWritingHead();

            // Flush로 인한 read 구간 증가 적용 
            lock (syncTailCommit)
            {
                this.readTail = this.writingHeadSegment;
                this.readTailIdx = this.writingHeadSegment.End;
            }

            Interlocked.Exchange(ref this.unconsumedBytes, this.unconsumedBytes+this.uncommittedBytes);
            if (DebugManager.consoleDump || !this.canWrite)
            {
                Console.WriteLine(
                    $"Commit : {this.uncommittedBytes.ToString()} ( {this.unconsumedBytes - this.uncommittedBytes} => {this.unconsumedBytes} )");
            }

            this.uncommittedBytes = 0;
            
            if (DebugManager.consoleDump || !this.canWrite)
            {
                Console.WriteLine($"Check : {canRead.ToString()} , {CheckReadable().ToString()}");
            }

            if (!canRead && CheckReadable())
            {
                this.ResumeReadIfAwait();
            }

            return this.canWrite;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ResumeReadIfAwait()
        {
            // Then 등록 전에 실행하면 에러이므로 스피닝

            if (!DebugManager.consoleDump)
            {
                Console.WriteLine($"SetResult : {this.ReadBuffer.Length.ToString()} ");
            }

            //Console.WriteLine("resume read!");
            //Console.WriteLine("canRead = true");
            Volatile.Write(ref this.canRead, true);
            Interlocked.Exchange(ref this.ReadPromise, new Future<ReadResult>())
                .SetResult(new ReadResult(this.ReadBuffer, false, false));

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CommitWritingHead()
        {
            this.writingHeadSegment!.End += this.writingHeadBytesBuffered;
            this.writingHeadBytesBuffered = 0;
        }

        // ======================================================== Allocate

        public int AllocateWriteHead(int sizeHint)  // 부족했던 바이트 반환
        {
            if (this.CheckWritable(sizeHint))
            {
                return 0;
            }

            int bytesShorts = this.CalcBytesShortWrite(sizeHint);

            if (this.writingHeadSegment == null)
            {
                InitFirstSegment(sizeHint);
            }
            else 
            {
                this.CommitWritingHead();   // 써 놓은거 커밋해서 

                CustomBufferSegment newSegment = AllocateSegment(sizeHint);

                this.writingHeadSegment!.SetNext(newSegment);
                this.writingHeadSegment = newSegment;
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
            if (this.writingHeadSegment == null)
            {
                throw new NullReferenceException("writingHeadSegment is null");
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

                this.CommitWritingHead();

                // 메모리 풀 사용을 위해 할당만 요청
                var newSegment = AllocateSegment(0);

                this.writingHeadSegment!.SetNext(newSegment);
                this.writingHeadSegment = newSegment;

                destination = this.writingHeadMemory.Span;
            }
            WriteSignal.Reset();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InitFirstSegment(int sizeHint)
        {
            CustomBufferSegment newSegment = AllocateSegment(sizeHint);

            this.writingHeadSegment = this.readHead = this.readTail = newSegment;
        }

        // ======================================================== AdvanceTo

        public void AdvanceTo(ref SequencePosition consumePosition)
        {
            var consumedSegment = (CustomBufferSegment?) consumePosition.GetObject();
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

            // 인덱스 절대값
            this.lastExaminedIndex = examinedSegment.RunningIndex + examinedIndex;

            Debug.Assert(this.unconsumedBytes >= 0, "GetUnconsumedBytes has gone negative");

            this.ResumeWriterIfAwait(examinedBytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ResumeWriterIfAwait(long examinedBytes)
        {
            lock (syncWriterThreshold) // Pause vs Resume
            {
                var oldLength = Interlocked.Exchange(ref this.unconsumedBytes, this.unconsumedBytes - examinedBytes);
                if ((this.canWrite == false) && this.options.CheckResumeWriter(this.unconsumedBytes))
                {
                    Console.WriteLine($"Write Unlocked! : {(this.unconsumedBytes).ToString()}");
                    this.canWrite = true;
                    this.WriteSignal.Set();
                }
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

            if (this.writingHeadSegment != returnEnd &&
                consumedIndex == returnEnd.Length) // 쓰기 블록이 넘어간 상태에서 현재 블록이 모두 사용 되었다!
            {
                // 쓰기중인 버퍼가 아니면 다음 블록으로
                var nextBlock = returnEnd!.NextSegment;
                this.readHead = nextBlock;
                this.readHeadIdx = 0;

                lock (syncTailCommit)
                {
                    if (this.readTail == returnEnd)
                    {
                        readTail = nextBlock;
                        readTailIdx = 0;
                    }
                }

                returnEnd = nextBlock;

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

                Debug.Assert(returnStart != this.readHead,
                    "Returning _readHead segment that's in use!");
                Debug.Assert(returnStart != this.readTail,
                    "Returning _readTail segment that's in use!");
                Debug.Assert(returnStart != this.writingHeadSegment,
                    "Returning _writingHead segment that's in use!");

                returnStart.ResetMemory();

                lock (syncPushPop)
                {
                    this.customBufferSegmentPool.Push(returnStart);
                }

                returnStart = next;
            }
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
            this.canWrite = true;
            this.canRead = true;
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

            this.writingHeadSegment = null;
            this.writingHeadMemory = default;
            this.readHead = null;
            this.readTail = null;
            this.lastExaminedIndex = -1;
            this.readTargetBytes = -1;
        }

    }
}
