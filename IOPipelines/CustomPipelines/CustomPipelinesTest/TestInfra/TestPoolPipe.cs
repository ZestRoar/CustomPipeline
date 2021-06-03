using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CustomPipelines;

namespace CustomPipelinesTest
{
    class TestPoolPipe
    {
        private readonly MemoryPool<byte>? _pool;

        internal const int MaxSegmentPoolSize = 256;
        // 메모리 운용
        private readonly TestPoolPipeBuffer customBuffer;

        // 파이프 상태 체크 용
        private readonly CustomPipeState pipeState;

        private bool disposed;

        public long Length => customBuffer.GetUnconsumedBytes();
        public ReadOnlySequence<byte> Buffer => customBuffer.ReadBuffer;


        public TestPoolPipe(CustomPipeOptions options)
        {
            this.customBuffer = new TestPoolPipeBuffer(options ?? CustomPipeOptions.Default);

            this.pipeState = new CustomPipeState();
        }

        public void Advance(int bytes)
        {
            // 쓰기를 한 메모리 이상으로 커밋 불가능
            if (this.customBuffer.CheckWritingOutOfRange(bytes))
            {
                throw new ArgumentOutOfRangeException();
            }

            this.customBuffer.Advance(bytes);
        }

        public void AdvanceAndCommit(int bytes)
        {
            Advance(bytes);
            CommitWrittenBytes();
        }

        public void AdvanceToEnd()
        {
            var endPosition = this.customBuffer.ReadBuffer.End;
            this.AdvanceTo(endPosition, endPosition);
        }

        public void AdvanceTo(SequencePosition endPosition)
        {
            this.AdvanceTo(endPosition, endPosition);
        }

        public void AdvanceTo(SequencePosition startPosition, SequencePosition endPosition)
        {
            this.customBuffer.AdvanceTo(ref startPosition, ref endPosition);

            //this.pipeState.EndRead();
        }

        internal bool CommitWrittenBytes()
        {
            if (!this.customBuffer.CheckAnyUncommittedBytes())
            {
                // 더이상 쓸 데이터가 없음
                return true;
            }

            this.customBuffer.Commit();

            return false;
        }

        public Memory<byte>? GetWriterMemory(int sizeHint = 0)
        {
            if (this.pipeState.IsWritingCompleted)
            {
                throw new InvalidOperationException();
            }

            if (sizeHint < 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            // 쓰기중이거나 메모리 없으면 null
            if (!this.customBuffer.CanWrite)
                return null;

            if (this.customBuffer.CheckWriterMemoryInvalid(sizeHint))
                this.customBuffer.AllocateWriteHead(sizeHint); // 세그먼트가 없다면 만들어서 쓰기용 구획을 준비해 두기

            //this.pipeState.BeginWrite();

            return this.customBuffer.Memory;
        }

        public bool Write(byte[] buffer, int offset = 0) =>
            Write(new ReadOnlyMemory<byte>(buffer, offset, buffer.Length));

        public bool Write(byte[] buffer, int offset, int count) =>
            Write(new ReadOnlyMemory<byte>(buffer, offset, count));

        public bool Write(Span<byte> span) => Write(span.ToArray());

        public bool Write(ReadOnlyMemory<byte> sourceMemory)
        {
            if (!this.customBuffer.CanWrite)        // 쓰기 제한 걸려 있음
            {
                return false;
            }

            // 메모리 없으면 할당
            if (this.customBuffer.CheckWriterMemoryInvalid(0))
            {
                this.customBuffer.AllocateWriteHead(0);
            }

            if (sourceMemory.Length <= this.customBuffer.Memory.Length)
            {
                sourceMemory.CopyTo(this.customBuffer.Memory);

                this.customBuffer.Advance(sourceMemory.Length);
            }
            else
            {
                this.customBuffer.WriteMultiSegment(sourceMemory.Span);     // 대용량 쓰기
            }

            this.CommitWrittenBytes();

            return true;
        }

        public bool TryRead(out ReadResult result)
        {
            if (this.pipeState.IsReadingCompleted || this.customBuffer.GetUnconsumedBytes() > 0)
            {
                result = new ReadResult(false, this.pipeState.IsWritingCompleted);
                return true;
            }

            result = default;
            return false;
        }

        
        public void Reset()
        {
            if (!disposed)
            {
                throw new InvalidOperationException();
            }
            this.CompletePipe();
            this.ResetState();
        }
        public void CompleteReader(Exception exception = null)
        {
            this.pipeState.CompleteRead();

            // 쓰기 중인 버퍼가 있으므로 취소 처리하고 정리 (정상 종료)
            if (this.pipeState.IsWritingCompleted)
            {
                this.CompletePipe();
            }
        }

        public void CompleteWriter(Exception exception = null)
        {
            this.CommitWrittenBytes(); // 보류 중인 버퍼 커밋

            // 읽기 중인 버퍼가 있으므로 콜백 처리하고 정리 (정상 종료)
            if (this.pipeState.IsReadingCompleted)
            {
                this.CompletePipe();
            }
        }

        private void CompletePipe()
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            this.customBuffer.Complete();
        }

        private void ResetState()
        {
            this.pipeState.Reset();
            this.customBuffer.Reset();
            this.disposed = false;
        }
    }
}
