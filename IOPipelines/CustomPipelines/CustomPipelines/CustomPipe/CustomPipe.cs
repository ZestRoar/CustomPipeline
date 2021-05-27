using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace CustomPipelines
{

    public class CustomPipe 
    {
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
        }



        public void Advance(int bytes)
        {
            if (this.pipeState.IsReadingOver)
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
            if (this.pipeState.IsReadingOver)
            {
                Trace.WriteLine("No Reading Allowed");
            }

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


            if (this.pipeState.IsWritingOver)
            {
                CompletePipe();
            }
        }

        public void CompleteWriter(Exception exception = null)
        {

            CommitUnsynchronized(); // 보류 중인 버퍼 커밋

            this.pipeState.FinishWriting();
            this.pipeState.FinishReading();


            if (this.pipeState.IsReadingOver)
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

        public StateResult Flush()
        {
            CommitUnsynchronized();
            return FlushResult();
        }

        public bool FlushAsync()
        {

            var wasEmpty = CommitUnsynchronized();

            this.pipeState.BeginFlush();

            if (this.pipeState.IsWritingCompleted)
            {
                UpdateFlushResult(); // cancel 갱신 안하면 계속 취소 상태 (후처리 필요)
            }
            else
            {
                // async 처리
            }

            if (!wasEmpty)
            {
                this.pipeState.FinishReading();
            }

            Debug.Assert(this.pipeState.IsWritingOver || this.pipeState.IsReadingOver);


            return this.pipeState.IsWritingOver;
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

        public void BlockingFlush()
        {
            while (FlushAsync())
            {
            }
        }

        public StateResult FlushResult()
        {
            bool isCanceled = !this.pipeState.FlushObserved;
            this.pipeState.FlushObserved = true;
            this.pipeState.FinishWriting();
            return new StateResult(isCanceled, this.pipeState.IsReadingOver);
        }

        private void UpdateFlushResult()
        {
            this.pipeState.EndFlush();
        }

        public long GetPosition()
        {
            throw new NotImplementedException();
        }

        public Memory<byte> GetWriterMemory(int sizeHint = 0)
        {
            if (this.pipeState.IsWritingOver)
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




        public Span<byte> GetWriterSpan(int sizeHint = 0)
        {
            throw new NotImplementedException();
        }

        public Span<byte> GetReaderSpan() // byte 배열 제공용도
        {
            throw new NotImplementedException();
        }

        public bool Read()
        {
            throw new NotImplementedException();
        }

        public bool ReadAsync()
        {
            if (this.pipeState.IsReadingOver)
            {
                Trace.WriteLine("NoReadingAllowed");
            }

            UpdateReadResult();
            //this.pipeState.ReadObserved = true;   // 조건부
            //this.pipeState.EndRead();           // 메커니즘 추가 필요

            callbacks.ReadCallback.RunCallback();

            return this.pipeState.IsReadingOver;
        }

        public StateResult BlockingRead()
        {
            while (ReadAsync())
            {
            }

            return ReadResult();
        }

        public StateResult ReadResult()
        {
            return new StateResult(this.pipeState.ReadObserved, this.pipeState.IsWritingOver);
        }

        private void UpdateReadResult()
        {
            if (this.pipeState.ReadObserved)
            {
                this.pipeState.BeginReadTentative();
            }
            else
            {
                this.pipeState.BeginRead();
            }
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }

        public bool Write(byte[] buffer, int offset = 0) =>
            WriteAsync(new ReadOnlyMemory<byte>(buffer, offset, buffer.Length));

        public bool Write(byte[] buffer, int offset, int count) =>
            WriteAsync(new ReadOnlyMemory<byte>(buffer, offset, count));

        public bool WriteAsync(Object? obj)
        {
            throw new NotImplementedException();
        }

        public bool WriteAsync(Stream? stream)
        {
            long originalPosition = 0;
            if (stream.CanSeek)
            {
                originalPosition = stream.Position;
                stream.Position = 0;
            }

            bool writeActivated = false;
            using (var memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                writeActivated = WriteAsync(memoryStream.ToArray());
            }

            return writeActivated;
        }

        public bool WriteAsync(ReadOnlyMemory<byte> sourceMemory)
        {
            if (this.pipeState.IsWritingOver)
            {
                Trace.WriteLine("NoWritingAllowed");
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

            //PrepareFlush(out completionData, out result, cancellationToken);
            FlushAsync();

            callbacks.WriteCallback.RunCallback();

            return this.pipeState.IsWritingOver;
        }

        public StateResult WriteResult()
        {
            if (this.pipeState.IsReadingOver)
            {
                return new StateResult(false, true);
            }

            return FlushResult(); // 아직 문제의 여지가 존재
        }

        public StateResult WriteEmpty(int bufferSize)
        {
            throw new NotImplementedException();
        }
    }
}