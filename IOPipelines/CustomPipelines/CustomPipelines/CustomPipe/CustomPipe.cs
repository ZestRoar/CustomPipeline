using System;
using System.Buffers;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("CustomPipelinesTest")]

namespace CustomPipelines
{
    internal class CustomPipe
    {
        internal const int MaxSegmentPoolSize = 256;
        // 메모리 운용
        private CustomPipeBuffer customBuffer;

        // 여러 콜백 등록 용도
        private CallbackManager callbacks;

        // 파이프 상태 체크 용
        private CustomPipeState pipeState;

        private bool disposed;

        public long Length => customBuffer.Length;
        public ReadOnlySequence<byte> Buffer => customBuffer.ReadBuffer;
        public CustomPipe() : this(CustomPipeOptions.Default)
        {
           
        }

        public CustomPipe(CustomPipeOptions options)
        {
            this.customBuffer = new CustomPipeBuffer(options ?? CustomPipeOptions.Default);

            this.callbacks = new CallbackManager();
            this.pipeState = new CustomPipeState();
        }

        public void RegisterReadCallback(Action action, bool repeat = true)
        {
            this.callbacks.ReadCallback = new StateCallback(action, repeat);
        }

        public void RegisterWriteCallback(Action action, bool repeat = true)
        {
            this.callbacks.WriteCallback = new StateCallback(action, repeat);
        }

        public void Advance(int bytes)
        {
            if (this.pipeState.CanRead)
            {
                return;
            }

            // 세그먼트 추가 할당이 필요
            if (customBuffer.CheckWritingOutOfRange(bytes))     
            {
                Trace.WriteLine("bytes : out of range");
            }

            this.customBuffer.AdvanceCore(bytes);
        }

        public void AdvanceToEnd()
        {
            SequencePosition endPosition = this.customBuffer.ReadBuffer.End;
            this.AdvanceTo(endPosition, endPosition);
        }

        public void AdvanceTo(SequencePosition endPosition)
        {
            this.AdvanceTo(endPosition, endPosition);
        }

        public void AdvanceTo(SequencePosition startPosition, SequencePosition endPosition)
        {
            this.customBuffer.AdvanceReader((CustomBufferSegment?) startPosition.GetObject(), startPosition.GetInteger(),
                (CustomBufferSegment?) endPosition.GetObject(), endPosition.GetInteger());
            
            this.pipeState.EndRead();
        }
        public StateResult Flush()
        {
            return new StateResult(false, CommitUnsynchronized());
        }

        internal bool CommitUnsynchronized()
        {
            this.pipeState.EndWrite();

            if (!this.customBuffer.CheckAnyUnflushedBytes())
            {
                // 더이상 쓸 데이터가 없음
                return true;
            }

            this.customBuffer.CommitCore();

            return false;
        }

        public Memory<byte> GetWriterMemory(int sizeHint = 0)
        {
            if (this.pipeState.CanNotWrite)
            {
                Trace.WriteLine("NoWritingAllowed");
            }

            if (sizeHint < 0)
            {
                Trace.WriteLine("OutOfRange");
            }

            if (!this.pipeState.IsWritingRunning || this.customBuffer.CheckWriterMemoryInavailable(sizeHint))
            {
                this.pipeState.BeginWrite();
                this.customBuffer.AllocateWriteHeadSynchronized(sizeHint); // 세그먼트가 없다면 만들어서 쓰기용 구획을 준비해 두기
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
            if (this.pipeState.CanNotWrite)
            {
                return false;
            }

            if (!this.pipeState.IsWritingRunning || this.customBuffer.CheckWriterMemoryInavailable(0))
            {
                this.customBuffer.AllocateWriteHeadSynchronized(0);
            }

            if (sourceMemory.Length <= this.customBuffer.Memory.Length)
            {
                sourceMemory.CopyTo(this.customBuffer.Memory);

                this.customBuffer.AdvanceCore(sourceMemory.Length);
            }
            else
            {
                this.customBuffer.WriteMultiSegment(sourceMemory.Span);     // 대용량 쓰기
            }

            Flush();

            return true;
        }

        public bool TryRead(out StateResult result)
        {
            if (this.pipeState.IsReadingCompleted || this.customBuffer.Length>0)
            {
                result = new StateResult(false, this.pipeState.IsWritingCompleted);
                return true;
            }

            this.pipeState.BeginRead(); // tentative
            result = default;
            return false;   
        }
        public bool Read()
        {
            if (this.pipeState.CanNotRead || !this.customBuffer.CheckAnyReadableBytes())    
            {
                return false;
            }

            this.pipeState.BeginRead();

            return true;
        }
        
       
        public void Reset()
        {
            this.CompletePipe();
            this.ResetState();
        }
        public void CompleteReader(Exception exception = null)
        {
            this.pipeState.EndRead();

            if (this.pipeState.CanWrite)
            {
                this.CompletePipe();
            }
        }

        public void CompleteWriter(Exception exception = null)
        {
            this.CommitUnsynchronized(); // 보류 중인 버퍼 커밋

            if (this.pipeState.CanRead)
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