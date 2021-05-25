using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CustomPipelines
{
    public sealed class CustomPipe : ICustomPipeline
    {
        internal const int InitialSegmentPoolSize = 16; // 65K
        public const int MaxSegmentPoolSize = 256; // 1MB
        
        private BufferSegmentStack bufferSegmentPool;

        private readonly CustomPipeOptions options;
        private readonly object sync = new object();
        private CustomPipeReader reader;
        private CustomPipeWriter writer;
        private StateCallback flushCallback;
        private StateCallback readCallback;
        private StateCallback writeCallback;
        private StateCallback flushCompletionCallback;
        private StateCallback readCompletionCallback;
        private StateCallback writeCompletionCallback;

        // 파이프 상에서의 공유 자원의 안전한 동기화를 위한 객체
        private object SyncObj => sync;

        private long unconsumedBytes;
        private long unflushedBytes;

        // readHead - readTail - writingHead - lastExamined 순인듯

        // 메모리 관리용 데이터, 마지막 위치를 인덱싱하며 해제 바이트 측정에 사용
        private long lastExaminedIndex = -1;

        // 읽기 중인 메모리의 시작 부분을 나타냄
        private BufferSegment? readHead;
        private int readHeadIndex;

        private bool disposed;

        // 읽기 가능한 메모리의 끝 부분을 나타냄
        private BufferSegment? readTail;
        private int readTailIndex;

        // 쓰기 중인 바이트 범위 중 시작 부분을 나타냄
        private BufferSegment? writingHead;
        private Memory<byte> writingHeadMemory;
        private int writingHeadBytesBuffered;

        // read/write 상태 판단을 위한 구조체
        private StateOperation operationState;
        public long Length => unconsumedBytes;

        // 완료 상태 체크용 (필요 시 awaitable, completion 추가 구현)
        private bool readCanceled;
        private bool writeCanceled;
        private bool flushCanceled;
        private bool readComplete;
        private bool writeComplete;
        private bool readAwait;
        private bool writeAwait;
        private bool readerRunning;


        public CustomPipe() : this(CustomPipeOptions.Default)
        {
        }
        public CustomPipe(CustomPipeOptions options)
        {
            if (options == null)
            {
                //ThrowHelper.ThrowArgumentNullException(ExceptionArgument.options);
            }

            bufferSegmentPool = new BufferSegmentStack(options.InitialSegmentPoolSize);

            this.options = options;
            reader = new CustomPipeReader(this);
            writer = new CustomPipeWriter(this);
        }

        public void RegisterReadCallback(Action action, bool repeat = true)
        {
            readCallback = new StateCallback(action, repeat);
        }
        public void RegisterFlushCallback(Action action, bool repeat = true)
        {
            flushCallback = new StateCallback(action, repeat);
        }
        public void RegisterWriteCallback(Action action, bool repeat = true)
        {
            writeCallback = new StateCallback(action, repeat);
        }
        public void RegisterReadCompletionCallback(Action action, bool repeat = true)
        {
            readCompletionCallback = new StateCallback(action, repeat);
        }
        public void RegisterFlushCompletionCallback(Action action, bool repeat = true)
        {
            flushCompletionCallback = new StateCallback(action, repeat);
        }
        public void RegisterWriteCompletionCallback(Action action, bool repeat = true)
        {
            writeCompletionCallback = new StateCallback(action, repeat);
        }

        private void ResetState()
        {
            readTailIndex = 0;
            readHeadIndex = 0;
            lastExaminedIndex = -1;
            unflushedBytes = 0;
            unconsumedBytes = 0;
        }



        public void Advance(int bytes)
        {
            lock (SyncObj)
            {
                if ((uint)bytes > (uint)writingHeadMemory.Length)
                {
                    Trace.WriteLine("bytes : out of range");
                }

                if (readComplete)
                {
                    return;
                }

                AdvanceCore(bytes);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AdvanceCore(int bytesWritten)
        {
            unflushedBytes += bytesWritten;
            writingHeadBytesBuffered += bytesWritten;
            writingHeadMemory = writingHeadMemory.Slice(bytesWritten);
        }

        public void AdvanceToEnd()
        {
            SequencePosition endPosition = ReadResult().Buffer.Value.End;
            AdvanceTo(endPosition, endPosition);
        }
        public void AdvanceTo(SequencePosition endPosition)
        {
            AdvanceTo(endPosition, endPosition);
        }
        public void AdvanceTo(SequencePosition startPosition, SequencePosition endPosition)
        {
            if (readComplete)
            {
                Trace.WriteLine("No Reading Allowed");
            }

            AdvanceReader((BufferSegment?)startPosition.GetObject(), startPosition.GetInteger(), 
                (BufferSegment?)endPosition.GetObject(), endPosition.GetInteger());
        }
        private void AdvanceReader(BufferSegment? consumedSegment, int consumedIndex, BufferSegment? examinedSegment, int examinedIndex)
        {
            // Throw if examined < consumed
            if (consumedSegment != null && examinedSegment != null && BufferSegment.GetLength(consumedSegment, consumedIndex, examinedSegment, examinedIndex) < 0)
            {
                Trace.WriteLine("InvalidExaminedOrConsumedPosition");
            }

            BufferSegment? returnStart = null;
            BufferSegment? returnEnd = null;

            lock (SyncObj)
            {
                var examinedEverything = false;
                if (examinedSegment == readTail)
                {
                    examinedEverything = examinedIndex == readTailIndex;
                }

                if (examinedSegment != null && lastExaminedIndex >= 0)
                {
                    long examinedBytes = BufferSegment.GetLength(lastExaminedIndex, examinedSegment, examinedIndex);
                    long oldLength = unconsumedBytes;

                    if (examinedBytes < 0)
                    {
                        Trace.WriteLine("InvalidExaminedPosition");
                    }

                    unconsumedBytes -= examinedBytes;

                    // Store the absolute position
                    lastExaminedIndex = examinedSegment.RunningIndex + examinedIndex;

                    Debug.Assert(unconsumedBytes >= 0, "Length has gone negative");

                    if (oldLength >= options.ResumeWriterThreshold &&
                        unconsumedBytes < options.ResumeWriterThreshold)
                    {
                        //_writerAwaitable.Complete(out completionData);
                        writeAwait = false;
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
                        BufferSegment? nextBlock = returnEnd!.NextSegment;
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
                        else if (writingHeadBytesBuffered == 0 && !operationState.IsWritingActive)
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
                if (examinedEverything && !writeComplete)
                {
                    Debug.Assert(writeComplete, "PipeWriter.FlushAsync is isn't completed and will deadlock");

                    readAwait = true;
                }

                while (returnStart != null && returnStart != returnEnd)
                {
                    BufferSegment? next = returnStart.NextSegment;
                    returnStart.ResetMemory();

                    Debug.Assert(returnStart != readHead, "Returning _readHead segment that's in use!");
                    Debug.Assert(returnStart != readTail, "Returning _readTail segment that's in use!");
                    Debug.Assert(returnStart != writingHead, "Returning _writingHead segment that's in use!");

                    if (bufferSegmentPool.Count < options.MaxSegmentPoolSize)
                    {
                        bufferSegmentPool.Push(returnStart);
                    }

                    returnStart = next;
                }

                operationState.EndRead();
            }

            //TrySchedule(WriterScheduler, completionData);
        }

        public void BeginCompleteReader(Exception exception = null)
        {
            throw new NotImplementedException();
        }

        public void BeginCompleteWriter(Exception exception = null)
        {
            throw new NotImplementedException();
        }

        public bool TryRead(out StateResult result)
        {
            throw new NotImplementedException();
        }

        public void CompleteReader(Exception exception = null)
        {
            throw new NotImplementedException();
        }

        public void CompleteWriter(Exception exception = null)
        {
            throw new NotImplementedException();
        }

        public StateResult Flush()
        {
            throw new NotImplementedException();
        }

        public bool FlushAsync()
        {
            lock (SyncObj)
            {
                var wasEmpty = CommitUnsynchronized();

                operationState.BeginFlush();

                if (!writeAwait)
                {
                    UpdateFlushResult();            // cancel 갱신 안하면 계속 취소 상태 (후처리 필요)
                }
                else
                {
                    // async 처리
                }

                if (!wasEmpty)
                {
                    readAwait = false;
                }

                Debug.Assert(!writeAwait || !readAwait);
            }

            return !readAwait;
        }

        internal bool CommitUnsynchronized()
        {
            operationState.EndWrite();

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
            if (options.PauseWriterThreshold > 0 &&
                oldLength < options.PauseWriterThreshold &&
                unconsumedBytes >= options.PauseWriterThreshold &&
                !readComplete)
            {
                //writerAwaitable.SetUncompleted();
                writeAwait = true;
            }

            unflushedBytes = 0;
            writingHeadBytesBuffered = 0;

            return false;
        }

        public void BlockingFlush()
        {
            while(FlushAsync()){}
        }
        public StateResult FlushResult()
        {
            bool isCanceled = flushCanceled;
            flushCanceled = false;
            return new StateResult(isCanceled, readComplete);
        }

        private void UpdateFlushResult()
        {
            operationState.EndFlush();
        }

        public long GetPosition()
        {
            throw new NotImplementedException();
        }

        public Memory<byte> GetWriterMemory(int sizeHint = 0)
        {
            if (writeComplete)
            {
                Trace.WriteLine("NoWritingAllowed");
            }

            if (sizeHint < 0)
            {
                Trace.WriteLine("OutOfRange");
            }

            if (!operationState.IsWritingActive || writingHeadMemory.Length == 0 || writingHeadMemory.Length < sizeHint)
            {
                AllocateWriteHeadSynchronized(sizeHint);        // 세그먼트가 없다면 만들어서 쓰기용 구획을 준비해 두기
            }

            return writingHeadMemory;  
        }
        private void AllocateWriteHeadSynchronized(int sizeHint)
        {
            lock (SyncObj)
            {
                operationState.BeginWrite();

                if (writingHead == null)
                {
                    BufferSegment newSegment = AllocateSegment(sizeHint);

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

                        BufferSegment newSegment = AllocateSegment(sizeHint);

                        writingHead.SetNext(newSegment);
                        writingHead = newSegment;
                    }
                }
            }
        }
        private BufferSegment AllocateSegment(int sizeHint)
        {
            Debug.Assert(sizeHint >= 0);
            BufferSegment newSegment = CreateSegmentUnsynchronized();

            MemoryPool<byte>? pool = null;
            int maxSize = -1;

            if (!options.IsDefaultSharedMemoryPool)
            {
                pool = options.Pool;
                maxSize = pool.MaxBufferSize;
            }

            if (sizeHint <= maxSize)
            {
                newSegment.SetOwnedMemory(pool!.Rent(Math.Max(options.MinimumSegmentSize, sizeHint)));
            }
            else
            {
                int sizeToRequest = Math.Min(int.MaxValue, Math.Max(options.MinimumSegmentSize, sizeHint));
                newSegment.SetOwnedMemory(ArrayPool<byte>.Shared.Rent(sizeToRequest));
            }

            writingHeadMemory = newSegment.AvailableMemory;

            return newSegment;
        }
        private BufferSegment CreateSegmentUnsynchronized()
        {
            if (bufferSegmentPool.TryPop(out BufferSegment? segment))
            {
                return segment;
            }

            return new BufferSegment();
        }


        public Span<byte> GetWriterSpan(int sizeHint = 0)
        {
            throw new NotImplementedException();
        }
        public Span<byte> GetReaderSpan()           // byte 배열 제공용도
        {
            throw new NotImplementedException();
        }

        public bool Read()
        {
            throw new NotImplementedException();
        }

        public bool ReadAsync()
        {
            if (readComplete)
            {
                Trace.WriteLine("NoReadingAllowed");
            }

            lock (SyncObj)
            {
                readerRunning = true;

                
            }

            return !readAwait;
        }
        public StateResult BlockingRead()
        {
            while (ReadAsync()) { }

            return ReadResult();
        }
        public StateResult ReadResult()
        {
            var readOnlySequence = readHead == null ? default : 
                new ReadOnlySequence<byte>(readHead, readHeadIndex, readTail, readTailIndex);
            return new StateResult(writeComplete, readCanceled, readOnlySequence);
        }

        private void UpdateReadResult()
        {
            if (readCanceled)
            {
                operationState.BeginReadTentative();
            }
            else
            {
                operationState.BeginRead();
            }
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }

        public bool Write()
        {
            throw new NotImplementedException();
        }
        public byte Write(ReadOnlySpan<byte> span)
        {
            throw new NotImplementedException();
        }

        public bool WriteAsync(object obj)
        {
            throw new NotImplementedException();
        }

        public bool WriteAsync(ReadOnlyMemory<byte> source)
        {
            if (writeComplete)
            {
                Trace.WriteLine("NoWritingAllowed");
            }

            lock (SyncObj)
            {
                if (!operationState.IsWritingActive || writingHeadMemory.Length == 0 || writingHeadMemory.Length < 0)
                {
                    AllocateWriteHeadSynchronized(0);
                }

                if (source.Length <= writingHeadMemory.Length)
                {
                    source.CopyTo(writingHeadMemory);

                    AdvanceCore(source.Length);
                }
                else
                {
                    WriteMultiSegment(source.Span);
                }

                //PrepareFlush(out completionData, out result, cancellationToken);
                FlushAsync();
            }

            return !writeAwait;
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
                BufferSegment newSegment = AllocateSegment(0);

                writingHead.SetNext(newSegment);
                writingHead = newSegment;

                destination = writingHeadMemory.Span;
            }
        }
        public int BlockingWrite(object obj)
        {
            int bytes = 0;
            while (WriteAsync(obj)) { }
            return bytes;
        }
        public StateResult WriteResult(object? obj = null)
        {
            if (readComplete)
            {
                return new StateResult(false,true);
            }

            return FlushResult();   // 아직 문제의 여지가 존재
        }

        public StateResult WriteEmpty(int bufferSize)
        {
            throw new NotImplementedException();
        }

        public object GetObject() // 세그먼트를 반환하는 용도
        {
            throw new NotImplementedException();
        }
    }
}
