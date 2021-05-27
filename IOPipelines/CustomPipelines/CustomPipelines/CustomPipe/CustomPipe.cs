using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace CustomPipelines
{

    public sealed class CustomPipe 
    {
        private CustomBufferSegmentStack customBufferSegmentPool;

        private readonly CustomPipeOptions options;

        // 여러 콜백 등록 용도
        private CallbackManager callbacks;

        // 파이프 상태 체크 용
        private CustomPipeState pipeState;

        private long unconsumedBytes;
        private long unflushedBytes;

        // readHead - readTail - writingHead - lastExamined 순

        // 메모리 관리용 데이터, 마지막 위치를 인덱싱하며 해제 바이트 측정에 사용
        private long lastExaminedIndex = -1;

        // 읽기 중인 메모리의 시작 부분을 나타냄
        private CustomBufferSegment? readHead;
        private int readHeadIndex;

        private bool disposed;

        // 읽기 가능한 메모리의 끝 부분을 나타냄
        private CustomBufferSegment? readTail;
        private int readTailIndex;

        // 쓰기 중인 바이트 범위 중 시작 부분을 나타냄
        private CustomBufferSegment? writingHead;
        private Memory<byte> writingHeadMemory;
        private int writingHeadBytesBuffered;

        public long Length => unconsumedBytes;

        public CustomPipe() : this(CustomPipeOptions.Default)
        {
           
        }

        public CustomPipe(CustomPipeOptions options)
        {
            if (options == null)
            {
                //ThrowHelper.ThrowArgumentNullException(ExceptionArgument.options);
            }

            customBufferSegmentPool = new CustomBufferSegmentStack(options);

            this.options = options;
            this.callbacks = new CallbackManager();
            this.pipeState = new CustomPipeState();
        }

        public bool CanWrite => false;
        public bool CanRead => true;


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
            this.readTailIndex = 0;
            this.readHeadIndex = 0;
            this.lastExaminedIndex = -1;
            this.unflushedBytes = 0;
            this.unconsumedBytes = 0;
        }



        public void Advance(int bytes)
        {

            if ((uint) bytes > (uint)this.writingHeadMemory.Length)
            {
                Trace.WriteLine("bytes : out of range");
            }

            if (this.pipeState.IsReadingOver)
            {
                return;
            }

            this.AdvanceCore(bytes);

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AdvanceCore(int bytesWritten)
        {
            this.unflushedBytes += bytesWritten;
            this.writingHeadBytesBuffered += bytesWritten;
            this.writingHeadMemory = this.writingHeadMemory.Slice(bytesWritten);
        }

        public void AdvanceToEnd()
        {
            SequencePosition endPosition = ReadResult().Buffer.Value.End;
            this.AdvanceTo(endPosition, endPosition);
        }

        public void AdvanceTo(SequencePosition endPosition)
        {
            AdvanceTo(endPosition, endPosition);
        }

        public void AdvanceTo(SequencePosition startPosition, SequencePosition endPosition)
        {
            if (this.pipeState.IsReadingOver)
            {
                Trace.WriteLine("No Reading Allowed");
            }

            AdvanceReader((CustomBufferSegment?) startPosition.GetObject(), startPosition.GetInteger(),
                (CustomBufferSegment?) endPosition.GetObject(), endPosition.GetInteger());
        }

        private void AdvanceReader(CustomBufferSegment? consumedSegment, int consumedIndex,
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


            var examinedEverything = false;
            if (examinedSegment == readTail)
            {
                examinedEverything = examinedIndex == readTailIndex;
            }

            if (examinedSegment != null && lastExaminedIndex >= 0)
            {
                long examinedBytes = CustomBufferSegment.GetLength(lastExaminedIndex, examinedSegment, examinedIndex);
                long oldLength = unconsumedBytes;

                if (examinedBytes < 0)
                {
                    Trace.WriteLine("InvalidExaminedPosition");
                }

                unconsumedBytes -= examinedBytes;

                // Store the absolute position
                lastExaminedIndex = examinedSegment.RunningIndex + examinedIndex;

                Debug.Assert(unconsumedBytes >= 0, "Length has gone negative");

                if (oldLength >= this.options.ResumeWriterThreshold &&
                    unconsumedBytes < this.options.ResumeWriterThreshold)
                {
                    //_writerAwaitable.Complete(out completionData);
                    this.pipeState.FinishWriting();
                }
            }

            if (consumedSegment != null)
            {
                if (readHead == null)
                {
                    Trace.WriteLine("AdvanceToInvalidCursor");
                    return;
                }

                returnStart = readHead;
                returnEnd = consumedSegment;

                void MoveReturnEndToNextBlock()
                {
                    CustomBufferSegment? nextBlock = returnEnd!.NextSegment;
                    if (readTail == returnEnd)
                    {
                        readTail = nextBlock;
                        readTailIndex = 0;
                    }

                    readHead = nextBlock;
                    readHeadIndex = 0;

                    returnEnd = nextBlock;
                }

                if (consumedIndex == returnEnd.Length)
                {
                    // If the writing head isn't block we're about to return, then we can move to the next one
                    // and return this block safely
                    if (writingHead != returnEnd)
                    {
                        MoveReturnEndToNextBlock();
                    }
                    // If the writing head is the same as the block to be returned, then we need to make sure
                    // there's no pending write and that there's no buffered data for the writing head
                    else if (writingHeadBytesBuffered == 0 && !this.pipeState.IsWritingActive)
                    {
                        // Reset the writing head to null if it's the return block and we've consumed everything
                        writingHead = null;
                        writingHeadMemory = default;

                        MoveReturnEndToNextBlock();
                    }
                    else
                    {
                        readHead = consumedSegment;
                        readHeadIndex = consumedIndex;
                    }
                }
                else
                {
                    readHead = consumedSegment;
                    readHeadIndex = consumedIndex;
                }
            }

            // We reset the awaitable to not completed if we've examined everything the producer produced so far
            // but only if writer is not completed yet
            if (examinedEverything && !this.pipeState.IsWritingOver)
            {
                Debug.Assert(this.pipeState.IsWritingOver,
                    "PipeWriter.FlushAsync is isn't completed and will deadlock");

                this.pipeState.ResumeWriting();
            }

            while (returnStart != null && returnStart != returnEnd)
            {
                CustomBufferSegment? next = returnStart.NextSegment;
                returnStart.ResetMemory();

                Debug.Assert(returnStart != readHead, "Returning _readHead segment that's in use!");
                Debug.Assert(returnStart != readTail, "Returning _readTail segment that's in use!");
                Debug.Assert(returnStart != writingHead, "Returning _writingHead segment that's in use!");

                customBufferSegmentPool.Push(returnStart);

                returnStart = next;
            }

            this.pipeState.EndRead();


            //TrySchedule(WriterScheduler, completionData);
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
            // Return all segments
            // if _readHead is null we need to try return _commitHead
            // because there might be a block allocated for writing
            CustomBufferSegment? segment = readHead ?? readTail;
            while (segment != null)
            {
                CustomBufferSegment returnSegment = segment;
                segment = segment.NextSegment;

                returnSegment.ResetMemory();
            }

            writingHead = null;
            writingHeadMemory = default;
            readHead = null;
            readTail = null;
            lastExaminedIndex = -1;

        }

        public StateResult Flush()
        {
            throw new NotImplementedException();
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

            if (unflushedBytes == 0)
            {
                // 더이상 쓸 데이터가 없음
                return true;
            }

            // flush를 통해 write 구간의 감소 발생
            Debug.Assert(writingHead != null);
            writingHead.End += writingHeadBytesBuffered;

            // flush를 통해 read 구간의 증가 발생
            readTail = writingHead;
            readTailIndex = writingHead.End;

            long oldLength = unconsumedBytes;
            unconsumedBytes += unflushedBytes;

            // Do not reset if reader is complete
            if (this.options.PauseWriterThreshold > 0 &&
                oldLength < this.options.PauseWriterThreshold &&
                unconsumedBytes >= this.options.PauseWriterThreshold &&
                !this.pipeState.IsReadingOver)
            {
                //writerAwaitable.SetUncompleted();
                this.pipeState.ResumeWriting();
            }

            unflushedBytes = 0;
            writingHeadBytesBuffered = 0;

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

            if (!this.pipeState.IsWritingActive || writingHeadMemory.Length == 0 || writingHeadMemory.Length < sizeHint)
            {
                AllocateWriteHeadSynchronized(sizeHint); // 세그먼트가 없다면 만들어서 쓰기용 구획을 준비해 두기
            }

            return writingHeadMemory;
        }

        private void AllocateWriteHeadSynchronized(int sizeHint)
        {

            this.pipeState.BeginWrite();

            if (writingHead == null)
            {
                CustomBufferSegment newSegment = AllocateSegment(sizeHint);

                writingHead = readHead = readTail = newSegment;
                lastExaminedIndex = 0;
            }
            else
            {
                int bytesLeftInBuffer = writingHeadMemory.Length;

                if (bytesLeftInBuffer == 0 || bytesLeftInBuffer < sizeHint)
                {
                    if (writingHeadBytesBuffered > 0)
                    {
                        writingHead.End += writingHeadBytesBuffered;
                        writingHeadBytesBuffered = 0;
                    }

                    CustomBufferSegment newSegment = AllocateSegment(sizeHint);

                    writingHead.SetNext(newSegment);
                    writingHead = newSegment;
                }
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

            if (sizeHint <= maxSize)
            {
                newSegment.SetOwnedMemory(pool!.Rent(Math.Max(this.options.MinimumSegmentSize, sizeHint)));
            }
            else
            {
                int sizeToRequest = Math.Min(int.MaxValue, Math.Max(this.options.MinimumSegmentSize, sizeHint));
                newSegment.SetOwnedMemory(ArrayPool<byte>.Shared.Rent(sizeToRequest));
            }

            writingHeadMemory = newSegment.AvailableMemory;

            return newSegment;
        }

        private CustomBufferSegment CreateSegmentUnsynchronized()
        {
            if (customBufferSegmentPool.TryPop(out CustomBufferSegment? segment))
            {
                return segment;
            }

            return new CustomBufferSegment();
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
            var readOnlySequence = readHead == null
                ? default
                : new ReadOnlySequence<byte>(readHead, readHeadIndex, readTail, readTailIndex);
            return new StateResult(this.pipeState.ReadObserved, this.pipeState.IsWritingOver, readOnlySequence);
        }

        public int ReadByte()
        {
            return readTailIndex - readHeadIndex;
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

            if (!this.pipeState.IsWritingActive || writingHeadMemory.Length == 0 || writingHeadMemory.Length < 0)
            {
                AllocateWriteHeadSynchronized(0);
            }

            if (sourceMemory.Length <= writingHeadMemory.Length)
            {
                sourceMemory.CopyTo(writingHeadMemory);

                AdvanceCore(sourceMemory.Length);
            }
            else
            {
                WriteMultiSegment(sourceMemory.Span);
            }

            //PrepareFlush(out completionData, out result, cancellationToken);
            FlushAsync();

            callbacks.WriteCallback.RunCallback();

            return this.pipeState.IsWritingOver;
        }

        private void WriteMultiSegment(ReadOnlySpan<byte> source)
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

                // We filled the segment
                writingHead.End += writable;
                writingHeadBytesBuffered = 0;

                // This is optimized to use pooled memory. That's why we pass 0 instead of
                // source.Length
                CustomBufferSegment newSegment = AllocateSegment(0);

                writingHead.SetNext(newSegment);
                writingHead = newSegment;

                destination = writingHeadMemory.Span;
            }
        }

        public int BlockingWrite(Stream? obj)
        {
            int bytes = 0;
            while (WriteAsync(obj))
            {
            }

            return bytes;
        }

        public int BlockingWrite(Object? obj)
        {
            int bytes = 0;
            while (WriteAsync(obj))
            {
            }

            return bytes;
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

        public object GetObject() // 세그먼트를 반환하는 용도
        {
            throw new NotImplementedException();
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            throw new NotImplementedException();
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            throw new NotImplementedException();
        }
    }
}