﻿using System;
using System.Buffers;
using System.Runtime.CompilerServices;

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

        public long Length => customBuffer.GetUnconsumedBytes();
        public ReadOnlySequence<byte> Buffer => customBuffer.ReadBuffer;
        public CustomPipe() : this(CustomPipeOptions.Default)
        {
           
        }

        public CustomPipe(CustomPipeOptions options)
        {
            this.customBuffer = new CustomPipeBuffer(options ?? CustomPipeOptions.Default);

            this.pipeState = new CustomPipeState();
        }

        public void RegisterReadCallback(Action action, int targetBytes, bool repeat = false)
            => this.customBuffer.RegisterReadCallback(action, targetBytes, repeat);

        public void RegisterWriteCallback(Action action, bool repeat = false)
            => this.customBuffer.RegisterWriteCallback(action, repeat);

        public void CancelWrite() => this.pipeState.CancelWrite();
        public void CancelRead() => this.pipeState.CancelRead();

        public StateResult ReadResult =>
            new(this.pipeState.IsReadingCanceled, this.pipeState.IsReadingCompleted);
        public StateResult WriteResult =>
            new(this.pipeState.IsWritingCanceled, this.pipeState.IsWritingCompleted);

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
            if (this.pipeState.IsReadingCompleted)
            {
                throw new InvalidOperationException
                    ("Reading is not allowed after reader was completed.");
            }

            this.customBuffer.AdvanceTo(ref startPosition, ref endPosition);
            
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
            {
                return null;
            }

            if (this.customBuffer.CheckWriterMemoryInvalid(sizeHint))
            {
                this.customBuffer.AllocateWriteHead(sizeHint); // 세그먼트가 없다면 만들어서 쓰기용 구획을 준비해 두기
            }

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

        public bool TryRead(out StateResult result)
        {
            if (this.pipeState.IsReadingCompleted || this.customBuffer.GetUnconsumedBytes() > 0)
            {
                result = new StateResult(this.pipeState.IsReadingCanceled, this.pipeState.IsReadingCompleted);
                return true;
            }

            result = default;
            return false;   
        }


        public bool Read()
        {
            if (this.pipeState.IsReadingCompleted)
            {
                throw new InvalidOperationException("No Reading Allowed");
            }

            if (completeException != null)
            {
                throw completeException;
            }

            if (!this.customBuffer.CanRead) // 이미 콜백 걸려있음
            {
                return false;
            }

            this.customBuffer.CanRead = false;
            return customBuffer.CheckReadable() ? true : false;
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