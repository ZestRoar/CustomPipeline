using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace CustomPipelines
{
    internal class CustomPipeBuffer
    {
        // 세그먼트 풀 & 옵션
        private CustomBufferSegmentStack customBufferSegmentPool;
        private readonly CustomPipeOptions options;

        // readHead - readTail - writingHead - lastExamined 순

        // 인덱싱 갱신 용(바이트 더하기)
        private long unconsumedBytes;
        private long uncommittedBytes;
        private int writingHeadBytesBuffered;

        // 메모리 관리용 데이터, 마지막 위치를 인덱싱하며 해제 바이트 측정에 사용
        private long lastExaminedIndex = -1;

#nullable enable
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
        private readonly StateCallback readCallback;
        private readonly StateCallback writeCallback;


        public CustomPipeBuffer(CustomPipeOptions options)
        {
            this.options = options;
            this.customBufferSegmentPool = new CustomBufferSegmentStack(options);
            this.readCallback = new StateCallback();
            this.writeCallback = new StateCallback();
            this.readTargetBytes = -1;
            this.CanWrite = true;
            this.CanRead = true;
        }


        public Memory<byte> Memory => this.writingHeadMemory;
        public ReadOnlySequence<byte> ReadBuffer 
            => (this.readHead == null) || (this.readTail == null) ? default
               : new ReadOnlySequence<byte>(
                   this.readHead, this.readHeadIdx,
                   this.readTail, this.readTailIdx);
        
        public bool CanWrite { get; set; }
        public bool CanRead { get; set; }

        public long GetUnconsumedBytes() => this.unconsumedBytes;
        public bool CheckReadable() => this.unconsumedBytes > this.readTargetBytes;
        public bool CheckWritingOutOfRange(int bytes) 
            => (uint)bytes > (uint)this.writingHeadMemory.Length;
        public bool CheckAnyUncommittedBytes() => this.uncommittedBytes > 0;
        public bool CheckAnyReadableBytes() 
            => (this.readHead != this.readTail)||(this.readHeadIdx != this.readTailIdx);
        public bool CheckWriterMemoryInvalid(int sizeHint)
            => this.writingHeadMemory.Length == 0 || 
               this.writingHeadMemory.Length < sizeHint;

        public void RegisterReadCallback(Action action, int targetBytes, bool repeat = false)
        {
            this.readCallback.SetCallback(action, repeat);
            this.readTargetBytes = targetBytes;
            this.CanRead = false;
        }

        public void RegisterWriteCallback(Action action, bool repeat = true)
        {
            this.writeCallback.SetCallback(action, repeat);
        }

        public void Reset()
        {
            this.readTailIdx = 0;
            this.readHeadIdx = 0;
            this.lastExaminedIndex = -1;
            this.uncommittedBytes = 0;
            this.unconsumedBytes = 0;
            this.readTargetBytes = -1;
            this.readCallback.SetCallback(null, false);
            this.writeCallback.SetCallback(null, false);
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
            this.readCallback.SetCallback(null,false);
            this.writeCallback.SetCallback(null,false);
        }

        public void AdvanceCore(int bytesWritten)
        {
            this.uncommittedBytes += bytesWritten;
            this.writingHeadBytesBuffered += bytesWritten;
            this.writingHeadMemory = this.writingHeadMemory[bytesWritten..];
        }

        public void AdvanceReader(ref SequencePosition startPosition, ref SequencePosition endPosition)
        {
            var consumedSegment = (CustomBufferSegment?)startPosition.GetObject();
            var consumedIndex = startPosition.GetInteger();
            var examinedSegment = (CustomBufferSegment?)endPosition.GetObject();
            var examinedIndex = endPosition.GetInteger();

            // Throw if examined < consumed
            if (CustomBufferSegment.IsInvalidLength(
                consumedSegment, consumedIndex, 
                    examinedSegment, examinedIndex))
            {
                throw new ArgumentOutOfRangeException();
            }

            // 메모리 시퀀스 갱신
            if (UpdateBuffer(ref examinedSegment, ref examinedIndex) == false)
            {
                return;
            }

            // 소비된 메모리 해제
            UnlinkUsedMemory(ref consumedSegment, ref consumedIndex);

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool UpdateBuffer(ref CustomBufferSegment? examinedSegment, ref int examinedIndex)
        {
            if (examinedSegment == null || this.lastExaminedIndex < 0) 
                return false;

            var examinedBytes = CustomBufferSegment.GetLength(
                this.lastExaminedIndex, examinedSegment, examinedIndex);
            var oldLength = this.unconsumedBytes;

            if (examinedBytes < 0)
            {
                throw new OutOfMemoryException();
            }

            this.unconsumedBytes -= examinedBytes;

            // 인덱스 절대값
            this.lastExaminedIndex = examinedSegment.RunningIndex + examinedIndex;

            Debug.Assert(this.unconsumedBytes >= 0, "GetUnconsumedBytes has gone negative");

            if ((this.CanWrite == true) || !this.options.CheckResumeWriter(oldLength, unconsumedBytes))
                return true;

            this.CanWrite = true;

            this.writeCallback.RunCallback();

            return true;
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
                
            if (this.readHead == null)
            {
                Trace.WriteLine("AdvanceToInvalidCursor");
                return;
            }
            returnStart = this.readHead;
            returnEnd = consumedSegment;

            if (consumedIndex == returnEnd.Length)  // 세그먼트 전체를 소비했으면
            {
                // 쓰기중인 버퍼가 아니면 다음 블록으로
                if (this.writingHead != returnEnd)
                {
                    MoveReturnEndToNextBlock(ref returnEnd);    // 다음 블록 할당
                }
                // Advance가 끝났고, 펜딩된 쓰기 작업이 없으면 블록 전체 해제
                else if (this.writingHeadBytesBuffered == 0)
                {
                    // 블록이 해제될 것이므로 메모리 끊어놓기
                    this.writingHead = null;
                    this.writingHeadMemory = default;

                    MoveReturnEndToNextBlock(ref returnEnd);    // 모두 null로
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

                this.customBufferSegmentPool.Push(returnStart);

                returnStart = next;
            }
        }


        private void MoveReturnEndToNextBlock(ref CustomBufferSegment returnEnd)
        {
            var nextBlock = returnEnd!.NextSegment;
            if (this.readTail == returnEnd)
            {
                this.readTail = nextBlock;
                this.readTailIdx = 0;
            }

            this.readHead = nextBlock;
            this.readHeadIdx = 0;

            returnEnd = nextBlock;
        }

        public void CommitCore()
        {
            if (this.writingHead == null)
            {
                Trace.WriteLine("문제가 있어 파이프 라인 전부 해제 후 재가동합니다.");
                Complete();

                CustomBufferSegment newSegment = AllocateSegment(0);

                this.writingHead = this.readHead = this.readTail = newSegment;
                this.lastExaminedIndex = 0;
                return;
            }

            // Advance로 인한 구간 증가 적용
            this.CommitWritingHead();

            // Flush로 인한 read 구간 증가 적용 
            this.readTail = this.writingHead;
            this.readTailIdx = this.writingHead.End;

            var oldLength = this.unconsumedBytes;
            this.unconsumedBytes += this.uncommittedBytes;
            this.uncommittedBytes = 0;

            // LengthCheck
            if (this.readTargetBytes >= 0 && this.readTargetBytes < this.unconsumedBytes)       
            {
                this.readCallback.RunCallback();
                this.CanRead = true;
                if (readCallback.ActionNotExist)
                {
                    this.readTargetBytes = -1;
                }
            }
            if (this.options.CheckPauseWriter(oldLength, this.unconsumedBytes))
            {
                this.CanWrite = false;
            }

        }

        public void AllocateWriteHeadSynchronized(int sizeHint)
        {
            if (this.writingHead == null)
            {
                CustomBufferSegment newSegment = AllocateSegment(sizeHint);

                this.writingHead = this.readHead = this.readTail = newSegment;
                this.lastExaminedIndex = 0;
            }
            else if (this.CheckWriterMemoryInvalid(sizeHint))
            {
                this.CommitWritingHead();

                CustomBufferSegment newSegment = AllocateSegment(sizeHint);

                this.writingHead!.SetNext(newSegment);
                this.writingHead = newSegment;
            }
            else
            {
                Trace.WriteLine("Writer Memory Available!");
            }
        }

        private CustomBufferSegment AllocateSegment(int sizeHint)
        {
            Debug.Assert(sizeHint >= 0);
            CustomBufferSegment newSegment = this.customBufferSegmentPool.TryPop();

            int sizeToRequest = Math.Max(this.options.MinimumSegmentSize, sizeHint);

            newSegment.SetOwnedMemory(ArrayPool<byte>.Shared.Rent(sizeToRequest));

            this.writingHeadMemory = newSegment.AvailableMemory;

            return newSegment;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CommitWritingHead()
        {
            Debug.Assert(this.writingHead != null);
            this.writingHead.End += this.writingHeadBytesBuffered;
            this.writingHeadBytesBuffered = 0;
        }

        // 큰 쓰기 작업을 해야할 때 추가적인 세그먼트 할당이 필요하면 진행
        public void WriteMultiSegment(ReadOnlySpan<byte> source)
        {
            // 이 경우 쓰기 버퍼 누락으로 Cancel 콜백을 발동시켜야 함  
            if (this.writingHead == null)       
            {
                Trace.WriteLine("문제가 있어 파이프 라인 전부 해제 후 재가동합니다."); 
                Complete();

                CustomBufferSegment newSegment = AllocateSegment(0);

                this.writingHead = this.readHead = this.readTail = newSegment;
                this.lastExaminedIndex = 0;
                return;
            }
            
            var destination = this.writingHeadMemory.Span;

            while (true)
            {
                var writable = Math.Min(destination.Length, source.Length);
                source[..writable].CopyTo(destination);
                source = source[writable..];
                AdvanceCore(writable);

                if (source.Length == 0)
                {
                    break;
                }

                this.writingHead!.End += writable;
                this.writingHeadBytesBuffered = 0;

                // 메모리 풀 사용을 위해 할당만 요청
                var newSegment = AllocateSegment(0);

                this.writingHead!.SetNext(newSegment);
                this.writingHead = newSegment;

                destination = this.writingHeadMemory.Span;
            }
        }
    }
}
