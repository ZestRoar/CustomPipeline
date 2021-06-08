using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Mad.Core.Concurrent.Synchronization;

[assembly: InternalsVisibleTo("CustomPipelinesTest")]

namespace CustomPipelines
{
    internal class CustomPipe : ICustomPipeWriter, ICustomPipeReader
    {
#nullable enable
        internal const int MaxSegmentPoolSize = 256;

        // 파이프 운용
        private readonly CustomPipeBuffer customBuffer;
        private readonly CustomPipeState pipeState;

        // 처리 관련
        private bool disposed = false;
        private object? _state;
        private ExceptionDispatchInfo? completeException;

        public CustomPipe() : this(CustomPipeOptions.Default)
        {
           
        }

        public CustomPipe(CustomPipeOptions? options)
        {
            this.customBuffer = new CustomPipeBuffer(options ?? CustomPipeOptions.Default);
            this.pipeState = new CustomPipeState();
        }

        public long Length => customBuffer.UnconsumedBytes;
        public ReadOnlySequence<byte> Buffer => customBuffer.ReadBuffer;

        public void RegisterTarget(int targetBytes)
            => this.customBuffer.RegisterTarget(targetBytes);

        public void CancelWrite() => this.pipeState.CancelWrite();
        public void CancelRead() => this.pipeState.CancelRead();

        public ReadResult ReadResult =>
            new(this.pipeState.IsReadingCanceled, this.pipeState.IsReadingCompleted);
        public WriteResult WriteResult =>
            new(this.pipeState.IsWritingCanceled, this.pipeState.IsWritingCompleted);


        // ===================================================================== Writer 

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

            if (!this.customBuffer.CanWrite)
            {
                return this.customBuffer.WriteSignal;
            }

            this.customBuffer.Advance(bytes);

            this.customBuffer.Commit();

            return this.customBuffer.WriteSignal;
        }
       
        private bool CommitWrittenBytes()
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

        public Memory<byte>? GetWriterMemory(int sizeHint = 1)
        {
            if (this.pipeState.IsWritingCompleted)
            {
                throw new InvalidOperationException();
            }

            if (sizeHint < 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            this.customBuffer.AllocateWriteHead(sizeHint);

            return this.customBuffer.Memory;
        }


        // =============================================================== Direct Write 
        // Write는 메모리를 받아 써서 커밋하는 것으로 카피가 하나 일어남 (성능 상 GetMemory + Advance 사용을 추천)

        public bool Write(byte[] buffer, int offset = 0) =>
            Write(new ReadOnlyMemory<byte>(buffer, offset, buffer.Length));

        public bool Write(byte[] buffer, int offset, int count) =>
            Write(new ReadOnlyMemory<byte>(buffer, offset, count));

        public bool Write(Span<byte> span) => Write(span.ToArray());

        public bool Write(ReadOnlyMemory<byte> sourceMemory)
        {
            if (this.completeException != null)
            {
                this.completeException.Throw();
            }

            if (!this.customBuffer.CanWrite)        // pause 상태
            {
                return false;
            }

            // 메모리 없으면 할당
            if (this.customBuffer.Memory.Length == 0)
            {
                this.customBuffer.AllocateWriteHead(sourceMemory.Length);
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

            return true;
        }

        public bool WriteAndCommit(ReadOnlyMemory<byte> sourceMemory)
        {
            var result = Write(sourceMemory);
            this.CommitWrittenBytes();
            return result;
        }

        public void Flush() => this.CommitWrittenBytes();

        // ===================================================================== Reader 

        public bool TryRead(out ReadResult result, int targetBytes = 0)
        {
            completeException?.Throw();

            if (this.pipeState.IsReadingCompleted)
            {
                throw new InvalidOperationException("No Reading Allowed");
            }

            result = new ReadResult(this.customBuffer.ReadBuffer,
                this.pipeState.IsReadingCanceled, this.pipeState.IsWritingCompleted);

            this.pipeState.ResumeRead();

            this.customBuffer.RegisterTarget(targetBytes);

            return this.customBuffer.CheckReadable() ? true : false;
        }

        public Future<ReadResult> Read(int targetBytes = 0)
        {
            completeException?.Throw();

            if (this.pipeState.IsReadingCompleted)
            {
                throw new InvalidOperationException("No Reading Allowed");
            }

            this.RegisterTarget(targetBytes);

            return this.customBuffer.ReadPromise;
        }

        public void AdvanceToEnd()
        {
            var endPosition = this.customBuffer.ReadBuffer.End;
            this.AdvanceTo(endPosition);
        }

        public void AdvanceTo(SequencePosition endPosition)
        {
            if (this.pipeState.IsReadingCompleted)
            {
                throw new InvalidOperationException
                    ("Reading is not allowed after reader was completed.");
            }
            this.customBuffer.AdvanceTo(ref endPosition);
        }

        // =================================================================== Complete 

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

            if (exception != null)
            {
                completeException = ExceptionDispatchInfo.Capture(exception);
            }

            // 쓰기 중인 버퍼가 있으면 취소 처리하고 정리해야함
            if (this.pipeState.IsWritingCompleted)
            {
                this.CompletePipe();
            }
        }

        public void CompleteWriter(Exception exception = null)
        {
            this.CommitWrittenBytes(); // 보류 중인 버퍼 커밋
            this.pipeState.CompleteWrite();

            if (exception != null)
            {
                completeException = ExceptionDispatchInfo.Capture(exception);
            }

            // 읽기 중인 버퍼가 있으면 콜백 처리하고 정리해야함
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