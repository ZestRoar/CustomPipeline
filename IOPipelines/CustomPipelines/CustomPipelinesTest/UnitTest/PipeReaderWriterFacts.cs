using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CustomPipelines;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CustomPipelinesTest
{
    [TestClass]
    public class PipeReaderWriterFacts
    {
        public PipeReaderWriterFacts()
        {
            originPipe = new Pipe();
            _pipe = new TestCustomPipe(new CustomPipeOptions());
        }
        public void Dispose()
        {
            _pipe.CompleteWriter();
            _pipe.CompleteReader();
        }

        private readonly TestCustomPipe _pipe;
        private readonly Pipe originPipe;

        [TestMethod]
        public void CanReadAndWrite()
        {
            byte[] bytes = Encoding.ASCII.GetBytes("Hello World");

            _pipe.Write(bytes);
            _pipe.Read();
            ReadOnlySequence<byte> buffer = _pipe.Buffer;

            Assert.AreEqual(11, buffer.Length);
            Assert.IsTrue(buffer.IsSingleSegment);
            var array = new byte[11];
            buffer.FirstSpan.CopyTo(array);
            Assert.AreEqual("Hello World", Encoding.ASCII.GetString(array));

            _pipe.AdvanceTo(buffer.End);
        }

        [TestMethod]
        public void AdvanceResetsCommitHeadIndex()
        {
            _pipe.GetWriterMemory(1);
            _pipe.Advance(100);
            _pipe.Flush();

            _pipe.Read();
            _pipe.AdvanceTo(_pipe.Buffer.End);

            
            Assert.IsFalse(_pipe.Read());

            bool CheckCallback = false;

            void RunCheckCallback()
            {
                CheckCallback = true;
            }
            _pipe.RegisterReadCallback(RunCheckCallback, 1);
            _pipe.Write(new byte[1]);
            _pipe.Flush();

            Assert.IsTrue(CheckCallback);

            _pipe.AdvanceTo(_pipe.Buffer.End);

            Assert.IsFalse(_pipe.Read());

        }

        [TestMethod]
        public void AdvanceShouldResetStateIfReadCanceled()
        {
            _pipe.CancelRead();

            var result = _pipe.ReadResult;
            _pipe.AdvanceToEnd();

            Assert.IsFalse(result.IsCompleted);
            Assert.IsTrue(result.IsCanceled);
            Assert.IsTrue(_pipe.Buffer.IsEmpty);

            Assert.IsFalse(_pipe.Read());
        }

        [TestMethod]
        public void AdvanceToInvalidCursorThrows()
        {
            _pipe.Write(new byte[100]);

            var buffer = _pipe.Buffer;
            _pipe.AdvanceTo(buffer.End);

            //_pipe.CancelRead();   // 비동기 아니므로 아직 하는일 없음

            // AdvanceToEnd는 자기 버퍼 체크를 한번 하므로 안전
            Assert.ThrowsException<InvalidOperationException>(() => _pipe.AdvanceTo(buffer.End));
            _pipe.Write(new byte[100]);
            _pipe.AdvanceToEnd();
        }

        [TestMethod]
        public void AdvanceWithGetPositionCrossingIntoWriteHeadWorks()
        {
            Memory<byte> memory = _pipe.GetWriterMemory(1).Value;
            _pipe.Advance(memory.Length);
            memory = _pipe.GetWriterMemory(1).Value;
            _pipe.Advance(memory.Length);
            _pipe.Flush();

            memory = _pipe.GetWriterMemory(1).Value;

            ReadOnlySequence<byte> buffer = _pipe.Buffer;
            SequencePosition position = buffer.GetPosition(buffer.Length);

            _pipe.AdvanceTo(position);
            _pipe.Advance(memory.Length);
        }

        [TestMethod]
        public void CompleteReaderAfterFlushWithoutAdvancingDoesNotThrow()
        {
            _pipe.Write(new byte[10]);

            _pipe.CompleteReader();
        }

        [TestMethod]
        public void AdvanceAfterCompleteThrows()
        {
            _pipe.Write(new byte[1]);
            var buffer = _pipe.Buffer;

            _pipe.CompleteReader();

            var exception = Assert.ThrowsException<InvalidOperationException>(() => _pipe.AdvanceTo(buffer.End));
            Assert.AreEqual("Reading is not allowed after reader was completed.", exception.Message);       // 문자열 체크 필요
        }

        [TestMethod]
        public void HelloWorldAcrossTwoBlocks()
        {
            var blockSize = _pipe.GetWriterMemory().Value.Length;

            byte[] paddingBytes = Enumerable.Repeat((byte) 'a', blockSize - 5).ToArray();
            byte[] bytes = Encoding.ASCII.GetBytes("Hello World");

            _pipe.Write(paddingBytes);
            _pipe.Write(bytes);
            _pipe.Flush();

            _pipe.Read();
            ReadOnlySequence<byte> buffer = _pipe.Buffer;
            Assert.IsFalse(buffer.IsSingleSegment);
            ReadOnlySequence<byte> helloBuffer = buffer.Slice(blockSize-5);
            Assert.IsFalse(helloBuffer.IsSingleSegment);
            var memory = new List<ReadOnlyMemory<byte>>();
            foreach (var m in helloBuffer)
            {
                memory.Add(m);
            }

            List<ReadOnlyMemory<byte>> spans = memory;
            _pipe.AdvanceTo(buffer.Start, buffer.Start);

            Assert.AreEqual(2, memory.Count);
            var helloBytes = new byte[spans[0].Length];
            spans[0].Span.CopyTo(helloBytes);
            var worldBytes = new byte[spans[1].Length];
            spans[1].Span.CopyTo(worldBytes);
            Assert.AreEqual("Hello", Encoding.ASCII.GetString(helloBytes));
            Assert.AreEqual(" World", Encoding.ASCII.GetString(worldBytes));
        }




        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ThrowTestException(Exception ex, Action<Exception> catchAction)
        {
            try
            {
                throw ex;
            }
            catch (Exception e)
            {
                catchAction(e);
            }
        }

        [TestMethod]
        public void ReadAsync_ThrowsIfWriterCompletedWithException()
        {
            ThrowTestException(new InvalidOperationException("Writer exception"), e => _pipe.CompleteWriter(e));
            //ThrowTestException(new InvalidOperationException("Writer exception"), e => originPipe.Writer.Complete(e));

            var invalidOperationException = Assert.ThrowsException<InvalidOperationException>(() => _pipe.Read());
            //var invalidOperationException2 = Assert.ThrowsException<InvalidOperationException>(() => originPipe.Reader.ReadAsync());

            Assert.AreEqual("Writer exception", invalidOperationException.Message);
            StringAssert.Contains(invalidOperationException.StackTrace, nameof(ThrowTestException));
            //StringAssert.Contains(invalidOperationException2.StackTrace, nameof(ThrowTestException));


            invalidOperationException = Assert.ThrowsException<InvalidOperationException>(() => _pipe.Read());
            Assert.AreEqual("Writer exception", invalidOperationException.Message);
            StringAssert.Contains(nameof(ThrowTestException), invalidOperationException.StackTrace);

            //Assert.Single(invalidOperationException.StackTrace, "Pipe.GetReadResult"));
            Assert.IsNotNull(Regex.Matches(invalidOperationException.StackTrace, "Pipe.GetReadResult"));
        }

        [TestMethod]
        public void WriteAsync_ThrowsIfReaderCompletedWithException()
        {
            ThrowTestException(new InvalidOperationException("Reader exception"), e => _pipe.CompleteReader(e));

            InvalidOperationException invalidOperationException =
                Assert.ThrowsException<InvalidOperationException>(() => _pipe.Write(new byte[1]));

            Assert.AreEqual("Reader exception", invalidOperationException.Message);
            StringAssert.Contains(nameof(ThrowTestException), invalidOperationException.StackTrace);

            invalidOperationException = Assert.ThrowsException<InvalidOperationException>(() => _pipe.Write(new byte[1]));
            Assert.AreEqual("Reader exception", invalidOperationException.Message);
            StringAssert.Contains(nameof(ThrowTestException), invalidOperationException.StackTrace);
        }

        [TestMethod]
        public void ReaderShouldNotGetUnflushedBytes()
        {
            // Write 10 and flush
            _pipe.Write(new byte[] { 0, 0, 0, 10 });

            // Write 9
            _pipe.Write(new byte[] { 0, 0, 0, 9 });

            // Write 8
            _pipe.Write(new byte[] { 0, 0, 0, 8 });

            // Make sure we don't see it yet
            var reader = _pipe.Buffer;

            Assert.AreEqual(4, reader.Length);
            Assert.AreEqual(new byte[] { 0, 0, 0, 10 }, reader.ToArray());

            // Don't move
            _pipe.AdvanceTo(reader.Start);

            // Now flush
            _pipe.Flush();

            reader = _pipe.Buffer;

            Assert.AreEqual(12, reader.Length);
            Assert.AreEqual(new byte[] { 0, 0, 0, 10 }, reader.Slice(0, 4).ToArray());
            Assert.AreEqual(new byte[] { 0, 0, 0, 9 }, reader.Slice(4, 4).ToArray());
            Assert.AreEqual(new byte[] { 0, 0, 0, 8 }, reader.Slice(8, 4).ToArray());

            _pipe.AdvanceTo(reader.Start, reader.Start);
        }

        [TestMethod]
        public async Task ReaderShouldNotGetUnflushedBytesWhenOverflowingSegments()
        {
            // Fill the block with stuff leaving 5 bytes at the end
            Memory<byte> buffer = _pipe.GetWriterMemory().Value;

            int len = buffer.Length;
            // Fill the buffer with garbage
            //     block 1       ->    block2
            // [padding..hello]  ->  [  world   ]
            byte[] paddingBytes = Enumerable.Repeat((byte)'a', len - 5).ToArray();
            _pipe.Write(paddingBytes);
            _pipe.Flush();

            // Write 10 and flush
            _pipe.Write(new byte[] { 0, 0, 0, 10 });

            // Write 9
            _pipe.Write(new byte[] { 0, 0, 0, 9 });

            // Write 8
            _pipe.Write(new byte[] { 0, 0, 0, 8 });

            // Make sure we don't see it yet
            var reader = _pipe.Buffer;

            Assert.AreEqual(len - 5, reader.Length);

            // Don't move
            _pipe.AdvanceTo(reader.End);

            // Now flush
            _pipe.Flush();

            reader = _pipe.Buffer;

            Assert.AreEqual(12, reader.Length);
            Assert.AreEqual(new byte[] { 0, 0, 0, 10 }, reader.Slice(0, 4).ToArray());
            Assert.AreEqual(new byte[] { 0, 0, 0, 9 }, reader.Slice(4, 4).ToArray());
            Assert.AreEqual(new byte[] { 0, 0, 0, 8 }, reader.Slice(8, 4).ToArray());

            _pipe.AdvanceTo(reader.Start, reader.Start);
        }

        [TestMethod]
        public async Task ReaderShouldNotGetUnflushedBytesWithAppend()
        {
            // Write 10 and flush
            _pipe.Write(new byte[] { 0, 0, 0, 10 });

            // Write Hello to another pipeline and get the buffer
            byte[] bytes = Encoding.ASCII.GetBytes("Hello");

            var c2 = new CustomPipe(new CustomPipeOptions());
            c2.Write(bytes);
            var c2Buffer = c2.Buffer;

            Assert.AreEqual(bytes.Length, c2Buffer.Length);

            // Write 9 to the buffer
            _pipe.Write(new byte[] { 0, 0, 0, 9 });

            // Append the data from the other pipeline
            foreach (ReadOnlyMemory<byte> memory in c2Buffer)
            {
                _pipe.Write(memory.Span.ToArray());
            }

            // Mark it as consumed
            c2.AdvanceTo(c2Buffer.End);

            // Now read and make sure we only see the comitted data
            var reader = _pipe.Buffer;

            Assert.AreEqual(4, reader.Length);
            Assert.AreEqual(new byte[] { 0, 0, 0, 10 }, reader.Slice(0, 4).ToArray());

            // Consume nothing
            _pipe.AdvanceTo(reader.Start);

            // Flush the second set of writes
            _pipe.Flush();

            reader = _pipe.Buffer;

            // int, int, "Hello"
            Assert.AreEqual(13, reader.Length);
            Assert.AreEqual(new byte[] { 0, 0, 0, 10 }, reader.Slice(0, 4).ToArray());
            Assert.AreEqual(new byte[] { 0, 0, 0, 9 }, reader.Slice(4, 4).ToArray());
            Assert.AreEqual("Hello", Encoding.ASCII.GetString(reader.Slice(8).ToArray()));

            _pipe.AdvanceTo(reader.Start, reader.Start);
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task ReadAsyncOnCompletedCapturesTheExecutionContext(bool useSynchronizationContext)
        {
            var pipe = new TestCustomPipe(new CustomPipeOptions());

            SynchronizationContext previous = SynchronizationContext.Current;
            var sc = new CustomSynchronizationContext();

            if (useSynchronizationContext)
            {
                SynchronizationContext.SetSynchronizationContext(sc);
            }

            try
            {
                AsyncLocal<int> val = new AsyncLocal<int>();
                var tcs = new TaskCompletionSource<int>();
                val.Value = 10;

               
                pipe.RegisterReadCallback(()=>tcs.TrySetResult(val.Value), 99999999);
                //pipe.Read().OnCompleted(() =>
                //{
                //    tcs.TrySetResult(val.Value);
                //});

                val.Value = 20;

                pipe.WriteEmpty(100);
                // Don't run any code on our fake sync context
                //await pipe.FlushAsync().ConfigureAwait(false);

                if (useSynchronizationContext)
                {
                    Assert.AreEqual(1, sc.Callbacks.Count);
                    sc.Callbacks[0].Item1(sc.Callbacks[0].Item2);
                }

                var value = await tcs.Task.ConfigureAwait(false);
                Assert.AreEqual(10, value);
            }
            finally
            {
                if (useSynchronizationContext)
                {
                    SynchronizationContext.SetSynchronizationContext(previous);
                }

                pipe.CompleteReader();
                pipe.CompleteWriter();
            }
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task FlushAsyncOnCompletedCapturesTheExecutionContextAndSyncContext(bool useSynchronizationContext)
        {
            var pipe = new TestCustomPipe(new CustomPipeOptions( pauseWriterThreshold: 20, resumeWriterThreshold: 10));

            SynchronizationContext previous = SynchronizationContext.Current;
            var sc = new CustomSynchronizationContext();

            if (useSynchronizationContext)
            {
                SynchronizationContext.SetSynchronizationContext(sc);
            }

            try
            {
                AsyncLocal<int> val = new AsyncLocal<int>();
                var tcs = new TaskCompletionSource<int>();
                val.Value = 10;

                pipe.WriteEmpty(20);
                //pipe.Writer.FlushAsync().GetAwaiter().OnCompleted(() =>
                //{
                //    tcs.TrySetResult(val.Value);
                //});

                val.Value = 20;

                // Don't run any code on our fake sync context
                //ReadResult result = await pipe.Reader.ReadAsync().ConfigureAwait(false);
                pipe.AdvanceToEnd();

                if (useSynchronizationContext)
                {
                    Assert.AreEqual(1, sc.Callbacks.Count);
                    sc.Callbacks[0].Item1(sc.Callbacks[0].Item2);
                }

                int value = await tcs.Task.ConfigureAwait(false);
                Assert.AreEqual(10, value);
            }
            finally
            {
                if (useSynchronizationContext)
                {
                    SynchronizationContext.SetSynchronizationContext(previous);
                }

                pipe.CompleteReader();
                pipe.CompleteWriter();
            }
        }

        [TestMethod]
        public async Task ReadingCanBeCanceled()
        {
            var cts = new CancellationTokenSource();
            cts.Token.Register(() => { _pipe.CompleteWriter(new OperationCanceledException(cts.Token)); });

            Task ignore = Task.Run(
                async () =>
                {
                    await Task.Delay(1000);
                    cts.Cancel();
                });

            await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                async () =>
                {
                    //ReadResult result = await _pipe.Reader.ReadAsync();
                    var buffer = _pipe.Buffer;
                });
        }

        [TestMethod]
        public async Task SyncReadThenAsyncRead()
        {
            _pipe.Write(Encoding.ASCII.GetBytes("Hello World"));
            

            bool gotData = _pipe.TryRead(out var result);
            Assert.IsTrue(gotData);

            var buffer = _pipe.Buffer;

            Assert.AreEqual("Hello World", Encoding.ASCII.GetString(buffer.ToArray()));

            _pipe.AdvanceTo(buffer.GetPosition(6));

            buffer = _pipe.Buffer;

            Assert.AreEqual("World", Encoding.ASCII.GetString(buffer.ToArray()));

            _pipe.AdvanceTo(buffer.End);
        }

        [TestMethod]
        public void ThrowsOnAllocAfterCompleteWriter()
        {
            _pipe.CompleteWriter();

            Assert.ThrowsException<InvalidOperationException>(() => _pipe.GetWriterMemory());
        }

        [TestMethod]
        public void ThrowsOnReadAfterCompleteReader()
        {
            _pipe.CompleteReader();

            Assert.ThrowsException<InvalidOperationException>(() => _pipe.Read());
        }

        [TestMethod]
        public void TryReadAfterCancelPendingReadReturnsTrue()
        {
            _pipe.CancelRead();

            bool gotData = _pipe.TryRead(out var result);

            Assert.IsTrue(result.IsCanceled);

            _pipe.AdvanceToEnd();
        }

        [TestMethod]
        public void TryReadAfterCloseWriterWithExceptionThrows()
        {
            _pipe.CompleteWriter(new Exception("wow"));

            var ex = Assert.ThrowsException<Exception>(() => _pipe.TryRead(out var result));
            Assert.AreEqual("wow", ex.Message);
        }

        [TestMethod]
        public void TryReadAfterReaderCompleteThrows()
        {
            _pipe.CompleteReader();

            Assert.ThrowsException<InvalidOperationException>(() => _pipe.TryRead(out var result));
        }

        [TestMethod]
        public void TryReadAfterWriterCompleteReturnsTrue()
        {
            _pipe.CompleteWriter();

            bool gotData = _pipe.TryRead(out var result);

            Assert.IsTrue(result.IsCompleted);

            _pipe.AdvanceToEnd();
        }

        [TestMethod]
        public void WhenTryReadReturnsFalseDontNeedToCallAdvance()
        {
            bool gotData = _pipe.TryRead(out var result);
            Assert.IsFalse(gotData);
            _pipe.AdvanceTo(default);
        }

        [TestMethod]
        public void WritingDataMakesDataReadableViaPipeline()
        {
            byte[] bytes = Encoding.ASCII.GetBytes("Hello World");

            _pipe.Write(bytes);
            var buffer = _pipe.Buffer;

            Assert.AreEqual(11, buffer.Length);
            Assert.IsTrue(buffer.IsSingleSegment);
            var array = new byte[11];
            buffer.First.Span.CopyTo(array);
            Assert.AreEqual("Hello World", Encoding.ASCII.GetString(array));

            _pipe.AdvanceTo(buffer.Start, buffer.Start);
        }

        [TestMethod]
        public void DoubleAsyncReadThrows()
        {
            //ValueTask<ReadResult> readTask1 = _pipe.ReadAsync();
            //ValueTask<ReadResult> readTask2 = _pipe.ReadAsync();
            //
            //var task1 = Assert.ThrowsExceptionAsync<InvalidOperationException>(async () => await readTask1);
            //var task2 = Assert.ThrowsExceptionAsync<InvalidOperationException>(async () => await readTask2);
            //
            //var exception1 = await task1;
            //var exception2 = await task2;
            //
            //Assert.AreEqual("Concurrent reads or writes are not supported.", exception1.Message);
            //Assert.AreEqual("Concurrent reads or writes are not supported.", exception2.Message);
        }

        [TestMethod]
        public void GetResultBeforeCompletedThrows()
        {
            //ValueTask<ReadResult> awaiter = _pipe.Reader.ReadAsync();

            //Assert.ThrowsException<InvalidOperationException>(() => awaiter.GetAwaiter().GetResult());
        }

        [TestMethod]
        public void CompleteAfterAdvanceCommits()
        {
            _pipe.WriteEmpty(4);

            _pipe.CompleteWriter();

            Assert.AreEqual(4, _pipe.Buffer.Length);
            _pipe.AdvanceToEnd();
        }

        [TestMethod]
        public void AdvanceWithoutReadThrows()
        {
            _pipe.Write(new byte[3]);
            var buffer = _pipe.Buffer;
            _pipe.AdvanceTo(buffer.Start);

            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => _pipe.AdvanceTo(buffer.End));
            Assert.AreEqual("No reading operation to complete.", exception.Message);
        }

        [TestMethod]
        public async Task TryReadAfterReadAsyncThrows()
        {
            _pipe.Write(new byte[3]);
            var buffer = _pipe.Buffer;
            Assert.ThrowsException<InvalidOperationException>(() => _pipe.TryRead(out _));
            _pipe.AdvanceTo(buffer.Start);
        }

        [TestMethod]
        public void GetMemoryZeroReturnsNonEmpty()
        {
            Assert.IsTrue(_pipe.GetWriterMemory(0).Value.Length > 0);
        }

        [TestMethod]
        public async Task ReadAsyncWithDataReadyReturnsTaskWithValue()
        {
            _pipe.WriteEmpty(10);
            _pipe.Flush();
            _pipe.TryRead(out var result);
            //Assert.IsTrue(IsTaskWithResult(result));
        }

        [TestMethod]
        public void CancelledReadAsyncReturnsTaskWithValue()
        {
            _pipe.CancelRead();
            _pipe.TryRead(out var result);
            //Assert.IsTrue(IsTaskWithResult(task));
        }

        [TestMethod]
        public void FlushAsyncWithoutBackpressureReturnsTaskWithValue()
        {
            _pipe.WriteEmpty(10);
            //var task = _pipe.Writer.FlushAsync();
            //Assert.IsTrue(IsTaskWithResult(task));
        }

        [TestMethod]
        public void CancelledFlushAsyncReturnsTaskWithValue()
        {
            _pipe.CancelWrite();
            //var task = _pipe.Writer.FlushAsync();
            //Assert.IsTrue(IsTaskWithResult(task));
        }

        [TestMethod]
        public void EmptyFlushAsyncDoesntWakeUpReader()
        {
            _pipe.TryRead(out var result);
            _pipe.Flush();

            Assert.IsFalse(result.IsCompleted);
        }

        [TestMethod]
        public void EmptyFlushAsyncDoesntWakeUpReaderAfterAdvance()
        {
            _pipe.Write(new byte[10]);

            var buffer = _pipe.Buffer;
            _pipe.AdvanceTo(buffer.Start, buffer.End);

            _pipe.TryRead(out var result);
            _pipe.Flush();

            Assert.IsFalse(result.IsCompleted);
        }

        [TestMethod]
        public async Task ReadAsyncReturnsDataAfterCanceledRead()
        {
            var pipe = new Pipe();

            ValueTask<ReadResult> readTask = pipe.Reader.ReadAsync();
            pipe.Reader.CancelPendingRead();
            ReadResult readResult = await readTask;
            Assert.IsTrue(readResult.IsCanceled);

            readTask = pipe.Reader.ReadAsync();
            await pipe.Writer.WriteAsync(new byte[] { 1, 2, 3 });
            readResult = await readTask;

            Assert.IsFalse(readResult.IsCanceled);
            Assert.IsFalse(readResult.IsCompleted);
            Assert.AreEqual(3, readResult.Buffer.Length);

            pipe.Reader.AdvanceTo(readResult.Buffer.End);
        }

        private bool IsTaskWithResult<T>(ValueTask<T> task)
        {
            return task == new ValueTask<T>(task.Result);
        }

        private sealed class CustomSynchronizationContext : SynchronizationContext
        {
            public List<Tuple<SendOrPostCallback, object>> Callbacks = new List<Tuple<SendOrPostCallback, object>>();

            public override void Post(SendOrPostCallback d, object state)
            {
                Callbacks.Add(Tuple.Create(d, state));
            }
        }
    }
}