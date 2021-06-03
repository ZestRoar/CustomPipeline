using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using Mad.Core.Concurrent.Synchronization;

[assembly: InternalsVisibleTo("CustomPipelinesTest")]

namespace CustomPipelines
{
    internal class CustomPipe
    {
        internal const int MaxSegmentPoolSize = 256;
        // 메모리 운용
        private readonly CustomPipeBuffer customBuffer;

        // 파이프 상태 체크 용
        private readonly CustomPipeState pipeState;

        private bool disposed;

        private Exception? completeException;

        // 인터페이스 제공
        private readonly CustomPipeReader readerPipe;
        private readonly CustomPipeWriter writerPipe;


        public long Length => customBuffer.GetUnconsumedBytes();
        public ReadOnlySequence<byte> Buffer => customBuffer.ReadBuffer;
        public CustomPipe() : this(CustomPipeOptions.Default)
        {
           
        }

        public CustomPipe(CustomPipeOptions options)
        {
            this.customBuffer = new CustomPipeBuffer(options ?? CustomPipeOptions.Default);

            this.pipeState = new CustomPipeState();

            readerPipe = new CustomPipeReader(this);
            writerPipe = new CustomPipeWriter(this);
        }


        public CustomPipeReader Reader => this.readerPipe;
        public CustomPipeWriter Writer => this.writerPipe;

        public void RegisterTarget(int targetBytes)
            => this.customBuffer.RegisterTarget(targetBytes);

        public void CancelWrite() => this.pipeState.CancelWrite();
        public void CancelRead() => this.pipeState.CancelRead();

        public ReadResult ReadResult =>
            new(this.pipeState.IsReadingCanceled, this.pipeState.IsReadingCompleted);
        public ReadResult WriteResult =>
            new(this.pipeState.IsWritingCanceled, this.pipeState.IsWritingCompleted);

        public bool TryAdvance(int bytes)
        {
            // 쓰기를 한 메모리 이상으로 커밋 불가능
            if (this.customBuffer.CheckWritingOutOfRange(bytes))
            {
                throw new ArgumentOutOfRangeException();
            }

            this.customBuffer.Advance(bytes);

            return CommitWrittenBytes();    // 점유하고 threshold 걸리면 알려주고 다음 동작 보류
        }
        public Signal Advance(int bytes)
        {
            // 쓰기를 한 메모리 이상으로 커밋 불가능
            if (this.customBuffer.CheckWritingOutOfRange(bytes))
            {
                throw new ArgumentOutOfRangeException();
            }

            this.customBuffer.Advance(bytes);

            this.customBuffer.writeSignal.Reset();
            return this.customBuffer.writeSignal;
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
            if (this.pipeState.IsReadingCompleted)
            {
                throw new InvalidOperationException
                    ("Reading is not allowed after reader was completed.");
            }

            this.customBuffer.AdvanceTo(ref startPosition, ref endPosition);
            
        }
        
        internal bool CommitWrittenBytes()
        {
            var isWritable = this.customBuffer.Commit();
            if (!this.customBuffer.CanRead && this.customBuffer.CheckReadable())
            {
                var result = new ReadResult(this.customBuffer.ReadBuffer,
                    this.pipeState.IsReadingCanceled, this.pipeState.IsReadingCompleted);
                this.customBuffer.SetResult(result);
            }
            return isWritable;
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

            // threshold 걸려있으면 null 반환
            if (!this.customBuffer.CanWrite)
            {
                return null;
            }

            // 메모리 없으면 세그먼트 추가 할당
            if (this.customBuffer.CheckWriterMemoryInvalid(sizeHint))
            {
                this.customBuffer.AllocateWriteHead(sizeHint); 
            }

            return this.customBuffer.Memory;
        }

        public bool Write(byte[] buffer, int offset = 0) =>
            TryWrite(new ReadOnlyMemory<byte>(buffer, offset, buffer.Length));

        public bool Write(byte[] buffer, int offset, int count) =>
            TryWrite(new ReadOnlyMemory<byte>(buffer, offset, count));

        public bool Write(Span<byte> span) => Write(span.ToArray());

        public bool TryWrite(ReadOnlyMemory<byte> sourceMemory)
        {
            if (!this.customBuffer.CanWrite)        // pause 상태
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
                this.customBuffer.WriteMultiSegment(sourceMemory.Span);   // 대용량 쓰기
            }

            this.CommitWrittenBytes();

            return true;
        }

        public bool TryRead(out ReadResult result, int targetBytes=0)
        {
            result = new ReadResult(this.customBuffer.ReadBuffer,
                this.pipeState.IsReadingCanceled, this.pipeState.IsReadingCompleted);

            this.customBuffer.RegisterTarget(targetBytes);
            
            return this.customBuffer.CheckReadable() ? true : false;
        }

        public Future<ReadResult> Read(int targetBytes=0)
        {
            if (this.pipeState.IsReadingCompleted)
            {
                throw new InvalidOperationException("No Reading Allowed");
            }

            if (this.completeException != null)
            {
                throw this.completeException;
            }

            this.RegisterTarget(targetBytes);

            this.customBuffer.CanRead = false;
            return this.customBuffer.readPromise;
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

            completeException = exception;

            // 쓰기 중인 버퍼가 있으므로 취소 처리하고 정리 (정상 종료)
            if (this.pipeState.IsWritingCompleted)
            {
                this.CompletePipe();
            }
        }

        public void CompleteWriter(Exception exception = null)
        {
            this.CommitWrittenBytes(); // 보류 중인 버퍼 커밋

            completeException = exception;

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