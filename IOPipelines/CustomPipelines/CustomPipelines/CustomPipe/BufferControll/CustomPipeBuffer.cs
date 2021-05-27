using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CustomPipelines
{
    internal class CustomPipeBuffer
    {
        // 세그먼트 풀
        private CustomBufferSegmentStack customBufferSegmentPool;
        private CustomPipeOptions options;

        // readHead - readTail - writingHead - lastExamined 순

        // 인덱싱 갱신 용(바이트 더하기)
        private long unconsumedBytes;
        private long unflushedBytes;
        private int writingHeadBytesBuffered;

        // 메모리 관리용 데이터, 마지막 위치를 인덱싱하며 해제 바이트 측정에 사용
        private long lastExaminedIndex = -1;

        // 읽기 중인 메모리의 시작 부분을 나타냄
        private CustomBufferSegment? readHead;
        private int readHeadIndex;

        // 읽기 가능한 메모리의 끝 부분을 나타냄
        private CustomBufferSegment? readTail;
        private int readTailIndex;

        // 쓰기 중인 바이트 범위 중 시작 부분을 나타냄
        private CustomBufferSegment? writingHead;
        private Memory<byte> writingHeadMemory;

        public CustomPipeBuffer(CustomPipeOptions options)
        {
            this.options = options;
            this.customBufferSegmentPool = new CustomBufferSegmentStack(options);
        }


        public long Length => unconsumedBytes;
        public Memory<byte> Memory => writingHeadMemory;
        public ReadOnlySequence<byte> ReadBuffer => readHead == null ? default
               : new ReadOnlySequence<byte>(readHead, readHeadIndex, readTail, readTailIndex);
        

        public bool CheckWritingOutOfRange(int bytes) => (uint)bytes > (uint)this.writingHeadMemory.Length;
        public bool CheckAnyUnflushedBytes() => this.unflushedBytes == 0;
        public bool CheckWriterMemoryInavailable(int sizeHint) => writingHeadMemory.Length == 0 || writingHeadMemory.Length < sizeHint;
        public bool WritePending { get; } = false;
        
        public void Reset()
        {
            this.readTailIndex = 0;
            this.readHeadIndex = 0;
            this.lastExaminedIndex = -1;
            this.unflushedBytes = 0;
            this.unconsumedBytes = 0;
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
        }

        public void AdvanceCore(int bytesWritten)
        {
            this.unflushedBytes += bytesWritten;
            this.writingHeadBytesBuffered += bytesWritten;
            this.writingHeadMemory = this.writingHeadMemory.Slice(bytesWritten);
        }

        public void AdvanceReader(CustomBufferSegment? consumedSegment, int consumedIndex,
            CustomBufferSegment? examinedSegment, int examinedIndex)
        {
            // Throw if examined < consumed
            if (consumedSegment != null && examinedSegment != null &&
                CustomBufferSegment.GetLength(consumedSegment, consumedIndex, examinedSegment, examinedIndex) < 0)
            {
                Trace.WriteLine("InvalidExaminedOrConsumedPosition");
            }

            CustomBufferSegment? returnStart = null;
            CustomBufferSegment? returnEnd = null;

            // read 구간을 모두 소비한 것인지 검사
            var examinedEverything = false;
            if (examinedSegment == this.readTail)
            {
                examinedEverything = examinedIndex == this.readTailIndex;
            }

            // 메모리 시퀀스 등록
            if (examinedSegment != null && this.lastExaminedIndex >= 0)
            {
                long examinedBytes = CustomBufferSegment.GetLength(this.lastExaminedIndex, examinedSegment, examinedIndex);
                long oldLength = this.unconsumedBytes;

                if (examinedBytes < 0)
                {
                    Trace.WriteLine("InvalidExaminedPosition");
                }

                this.unconsumedBytes -= examinedBytes;

                // 인덱스 절대값
                this.lastExaminedIndex = examinedSegment.RunningIndex + examinedIndex;

                Debug.Assert(this.unconsumedBytes >= 0, "Length has gone negative");

                //if (oldLength >= this.options.ResumeWriterThreshold &&
                //    unconsumedBytes < this.options.ResumeWriterThreshold)
                //{
                //    //_writerAwaitable.Complete(out completionData);
                //    this.pipeState.FinishWriting();
                //}
            }

            // 소비된 메모리 해제
            if (consumedSegment != null)
            {
                if (this.readHead == null)
                {
                    Trace.WriteLine("AdvanceToInvalidCursor");
                    return;
                }

                returnStart = this.readHead;
                returnEnd = consumedSegment;

                void MoveReturnEndToNextBlock()
                {
                    CustomBufferSegment? nextBlock = returnEnd!.NextSegment;
                    if (this.readTail == returnEnd)
                    {
                        this.readTail = nextBlock;
                        this.readTailIndex = 0;
                    }

                    this.readHead = nextBlock;
                    this.readHeadIndex = 0;

                    returnEnd = nextBlock;
                }

                if (consumedIndex == returnEnd.Length)
                {
                    // 쓰기중인 버퍼가 아니면 다음 블록으로
                    if (this.writingHead != returnEnd)
                    {
                        MoveReturnEndToNextBlock();
                    }
                    // Advance가 끝났고, 펜딩된 쓰기 작업이 없으면 다음 블록으로
                    else if (this.writingHeadBytesBuffered == 0 && !this.WritePending)
                    {
                        // 블록이 해제될 것이므로 메모리 끊어놓기
                        this.writingHead = null;
                        this.writingHeadMemory = default;

                        MoveReturnEndToNextBlock();
                    }
                    else
                    {
                        this.readHead = consumedSegment;
                        this.readHeadIndex = consumedIndex;
                    }
                }
                else
                {
                    this.readHead = consumedSegment;
                    this.readHeadIndex = consumedIndex;
                }
            }

            // 소비된 메모리 반환 작업
            while (returnStart != null && returnStart != returnEnd)
            {
                CustomBufferSegment? next = returnStart.NextSegment;
                returnStart.ResetMemory();

                Debug.Assert(returnStart != readHead, "Returning _readHead segment that's in use!");
                Debug.Assert(returnStart != readTail, "Returning _readTail segment that's in use!");
                Debug.Assert(returnStart != writingHead, "Returning _writingHead segment that's in use!");

                this.customBufferSegmentPool.Push(returnStart);

                returnStart = next;
            }

            
        }
    
        public void CommitCore()
        {
            // Advance로 인한 구간 증가 적용
            Debug.Assert(this.writingHead != null);
            this.CommitWritngHead();

            // Flush로 인한 read 구간 증가 적용 
            this.readTail = this.writingHead;
            this.readTailIndex = this.writingHead.End;

            this.unconsumedBytes += this.unflushedBytes;
            this.unflushedBytes = 0;

        }

        public void AllocateWriteHeadSynchronized(int sizeHint)
        {
            if (this.writingHead == null)
            {
                CustomBufferSegment newSegment = AllocateSegment(sizeHint);

                this.writingHead = this.readHead = this.readTail = newSegment;
                this.lastExaminedIndex = 0;
            }
            else if (this.CheckWriterMemoryInavailable(sizeHint))
            {
                this.CommitWritngHead();

                CustomBufferSegment newSegment = AllocateSegment(sizeHint);

                this.writingHead.SetNext(newSegment);
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
            CustomBufferSegment newSegment = CreateSegmentUnsynchronized();

            MemoryPool<byte>? pool = null;
            int maxSize = -1;

            if (!this.options.IsDefaultSharedMemoryPool)
            {
                pool = this.options.Pool;
                maxSize = pool.MaxBufferSize;
            }

            // 공유 풀 있으면 풀에서 빌리고 없으면 배열 풀에서 메모리 빌리기
            if (sizeHint <= maxSize)
            {
                newSegment.SetOwnedMemory(pool!.Rent(Math.Max(this.options.MinimumSegmentSize, sizeHint)));
            }
            else
            {
                int sizeToRequest = Math.Max(this.options.MinimumSegmentSize, sizeHint);
                newSegment.SetOwnedMemory(ArrayPool<byte>.Shared.Rent(sizeToRequest));
            }

            this.writingHeadMemory = newSegment.AvailableMemory;

            return newSegment;
        }
        private CustomBufferSegment CreateSegmentUnsynchronized()
        {
            if (customBufferSegmentPool.TryPop(out CustomBufferSegment? segment))
            {
                return segment;
            }

            return new CustomBufferSegment();   // 메모리 풀 알아서 증가 (C++의 std::vector 처럼 배열 자동 증가)
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CommitWritngHead()
        {
            Debug.Assert(this.writingHead != null);
            this.writingHead.End += this.writingHeadBytesBuffered;
            this.writingHeadBytesBuffered = 0;
        }

        public void WriteMultiSegment(ReadOnlySpan<byte> source)
        {
            Debug.Assert(writingHead != null);
            Span<byte> destination = writingHeadMemory.Span;

            while (true)
            {
                int writable = Math.Min(destination.Length, source.Length);
                source.Slice(0, writable).CopyTo(destination);
                source = source.Slice(writable);
                AdvanceCore(writable);

                if (source.Length == 0)
                {
                    break;
                }

                // CommitWritingHead()
                writingHead.End += writable;
                writingHeadBytesBuffered = 0;

                // 메모리 풀 사용을 위해 할당만 요청
                CustomBufferSegment newSegment = AllocateSegment(0);

                writingHead.SetNext(newSegment);
                writingHead = newSegment;

                destination = writingHeadMemory.Span;
            }
        }
    }
}
