using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace CustomPipelines
{

    public class CustomPipe 
    {
        // 메모리 운용
        private CustomPipeBuffer customBuffer;

        // 세그먼트 크기 등 옵션 지정
        private readonly CustomPipeOptions options;

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
            this.options = options ?? CustomPipeOptions.Default;
            this.customBuffer = new CustomPipeBuffer(this.options);

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

        private void ResetState()
        {
            this.pipeState.Reset();
            this.customBuffer.Reset();
            disposed = false;
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

        public bool TryRead(out StateResult result)
        {
            throw new NotImplementedException();
        }

        public void CompleteReader(Exception exception = null)
        {

            if (this.pipeState.IsReadingActive)
            {
                this.pipeState.EndRead();
            }

            this.pipeState.FinishReading();
            this.pipeState.FinishWriting();


            if (this.pipeState.CanWrite)
            {
                CompletePipe();
            }
        }

        public void CompleteWriter(Exception exception = null)
        {

            CommitUnsynchronized(); // 보류 중인 버퍼 커밋

            this.pipeState.FinishWriting();
            this.pipeState.FinishReading();


            if (this.pipeState.CanRead)
            {
                CompletePipe();
            }
        }

        private void CompletePipe()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            this.customBuffer.Complete();
        }

        public void Flush()
        {
            CommitUnsynchronized();
        }

        internal bool CommitUnsynchronized()
        {
            this.pipeState.EndWrite();

            if (this.customBuffer.CheckAnyUnflushedBytes())
            {
                // 더이상 쓸 데이터가 없음
                return true;
            }

            this.customBuffer.CommitCore();

            return false;
        }


        
        public long GetPosition()
        {
            throw new NotImplementedException();
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

            if (!this.pipeState.IsWritingActive || this.customBuffer.CheckWriterMemoryInavailable(sizeHint))
            {
                this.pipeState.BeginWrite();
                this.customBuffer.AllocateWriteHeadSynchronized(sizeHint); // 세그먼트가 없다면 만들어서 쓰기용 구획을 준비해 두기
            }

            return this.customBuffer.Memory;
        }


        public bool Read()
        {
            if (!this.pipeState.CanRead)
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

        public bool Write(byte[] buffer, int offset = 0) =>
            Write(new ReadOnlyMemory<byte>(buffer, offset, buffer.Length));

        public bool Write(byte[] buffer, int offset, int count) =>
            Write(new ReadOnlyMemory<byte>(buffer, offset, count));

        public long WriteAsync(Stream? stream)
        {
            long originalPosition = 0;
            if (stream.CanSeek)
            {
                originalPosition = stream.Position;
                stream.Position = 0;
            }

            long writtenbytes = 0;
            using (var memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                if(Write(memoryStream.ToArray()))
                {
                    writtenbytes = memoryStream.Length;
                }
            }

            return writtenbytes;
        }

        public bool Write(ReadOnlyMemory<byte> sourceMemory)
        {
            if(this.pipeState.CanNotWrite)
            {
                return false;
            }

            if (!this.pipeState.IsWritingActive || this.customBuffer.CheckWriterMemoryInavailable(0))
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
                this.customBuffer.WriteMultiSegment(sourceMemory.Span);
            }

            Flush();

            return true;
        }

        public StateResult WriteEmpty(int bufferSize)
        {
            throw new NotImplementedException();
        }
    }
}