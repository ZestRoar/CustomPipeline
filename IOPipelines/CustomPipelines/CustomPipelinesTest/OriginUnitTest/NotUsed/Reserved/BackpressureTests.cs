﻿using System;
using System.IO.Pipes;
using System.Threading.Tasks;
using CustomPipelines;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CustomPipelinesTest
{
    [TestClass]

    public class BackpressureTests : IDisposable
    {
        private const int PauseWriterThreshold = 64;
        private const int ResumeWriterThreshold = 32;

        public BackpressureTests()
        {
            _pipe = new TestCustomPipe( new CustomPipeOptions(resumeWriterThreshold: ResumeWriterThreshold,
                pauseWriterThreshold: PauseWriterThreshold));
        }

        public void Dispose()
        {
            _pipe.CompleteWriter();
            _pipe.CompleteReader();
        }


        private readonly TestCustomPipe _pipe;

        [TestMethod]
        public void FlushAsyncAwaitableCompletesWhenReaderAdvancesUnderLow()
        {
            CustomPipeWriter writableBuffer = _pipe.Writer.WriteEmpty(PauseWriterThreshold);

            ValueTask<FlushResult> flushAsync = writableBuffer.FlushAsync();

            Assert.IsFalse(flushAsync.IsCompleted);

            ReadResult result = _pipe.Reader.ReadAsync().GetAwaiter().GetResult();
            SequencePosition consumed = result.Buffer.GetPosition(33);
            _pipe.Reader.AdvanceTo(consumed, consumed);

            Assert.IsTrue(flushAsync.IsCompleted);
            FlushResult flushResult = flushAsync.GetAwaiter().GetResult();
            Assert.IsFalse(flushResult.IsCompleted);
        }

        [TestMethod]
        public void FlushAsyncAwaitableCompletesWhenReaderAdvancesExaminedUnderLow()
        {
            PipeWriter writableBuffer = _pipe.Writer.WriteEmpty(PauseWriterThreshold);
            ValueTask<FlushResult> flushAsync = writableBuffer.FlushAsync();

            Assert.IsFalse(flushAsync.IsCompleted);

            ReadResult result = _pipe.Reader.ReadAsync().GetAwaiter().GetResult();
            SequencePosition examined = result.Buffer.GetPosition(33);
            _pipe.Reader.AdvanceTo(result.Buffer.Start, examined);

            Assert.IsTrue(flushAsync.IsCompleted);
            FlushResult flushResult = flushAsync.GetAwaiter().GetResult();
            Assert.IsFalse(flushResult.IsCompleted);
        }

        [TestMethod]
        public async Task CanBufferPastPauseThresholdButGetPausedEachTime()
        {
            const int loops = 5;

            async Task WriteLoopAsync()
            {
                for (int i = 0; i < loops; i++)
                {
                    _pipe.Writer.WriteEmpty(PauseWriterThreshold);

                    ValueTask<FlushResult> flushTask = _pipe.Writer.FlushAsync();

                    Assert.IsFalse(flushTask.IsCompleted);

                    await flushTask;
                }

                _pipe.Writer.Complete();
            }

            Task writingTask = WriteLoopAsync();

            while (true)
            {
                ReadResult result = await _pipe.Reader.ReadAsync();

                if (result.IsCompleted)
                {
                    _pipe.Reader.AdvanceTo(result.Buffer.End);

                    Assert.AreEqual(PauseWriterThreshold * loops, result.Buffer.GetUnconsumedBytes);
                    break;
                }

                _pipe.Reader.AdvanceTo(result.Buffer.Start, result.Buffer.End);
            }

            await writingTask;
        }

        [TestMethod]
        public void FlushAsyncAwaitableDoesNotCompletesWhenReaderAdvancesUnderHight()
        {
            PipeWriter writableBuffer = _pipe.Writer.WriteEmpty(PauseWriterThreshold);
            ValueTask<FlushResult> flushAsync = writableBuffer.FlushAsync();

            Assert.IsFalse(flushAsync.IsCompleted);

            ReadResult result = _pipe.Reader.ReadAsync().GetAwaiter().GetResult();
            SequencePosition consumed = result.Buffer.GetPosition(ResumeWriterThreshold);
            _pipe.Reader.AdvanceTo(consumed, consumed);

            Assert.IsFalse(flushAsync.IsCompleted);
        }

        [TestMethod]
        public void FlushAsyncAwaitableResetsOnCommit()
        {
            PipeWriter writableBuffer = _pipe.Writer.WriteEmpty(PauseWriterThreshold);
            ValueTask<FlushResult> flushAsync = writableBuffer.FlushAsync();

            Assert.IsFalse(flushAsync.IsCompleted);

            ReadResult result = _pipe.Reader.ReadAsync().GetAwaiter().GetResult();
            SequencePosition consumed = result.Buffer.GetPosition(33);
            _pipe.Reader.AdvanceTo(consumed, consumed);

            Assert.IsTrue(flushAsync.IsCompleted);
            FlushResult flushResult = flushAsync.GetAwaiter().GetResult();
            Assert.IsFalse(flushResult.IsCompleted);

            writableBuffer = _pipe.Writer.WriteEmpty(PauseWriterThreshold);
            flushAsync = writableBuffer.FlushAsync();

            Assert.IsFalse(flushAsync.IsCompleted);
        }

        [TestMethod]
        public void FlushAsyncReturnsCompletedIfReaderCompletes()
        {
            PipeWriter writableBuffer = _pipe.Writer.WriteEmpty(PauseWriterThreshold);
            ValueTask<FlushResult> flushAsync = writableBuffer.FlushAsync();

            Assert.IsFalse(flushAsync.IsCompleted);

            _pipe.Reader.Complete();

            Assert.IsTrue(flushAsync.IsCompleted);
            FlushResult flushResult = flushAsync.GetAwaiter().GetResult();
            Assert.IsTrue(flushResult.IsCompleted);

            writableBuffer = _pipe.Writer.WriteEmpty(PauseWriterThreshold);
            flushAsync = writableBuffer.FlushAsync();
            flushResult = flushAsync.GetAwaiter().GetResult();

            Assert.IsTrue(flushResult.IsCompleted);
            Assert.IsTrue(flushAsync.IsCompleted);
        }

        [TestMethod]
        public async Task FlushAsyncReturnsCompletedIfReaderCompletesWithoutAdvance()
        {
            PipeWriter writableBuffer = _pipe.Writer.WriteEmpty(PauseWriterThreshold);
            ValueTask<FlushResult> flushAsync = writableBuffer.FlushAsync();

            Assert.IsFalse(flushAsync.IsCompleted);

            ReadResult result = await _pipe.Reader.ReadAsync();
            _pipe.Reader.Complete();

            Assert.IsTrue(flushAsync.IsCompleted);
            FlushResult flushResult = flushAsync.GetAwaiter().GetResult();
            Assert.IsTrue(flushResult.IsCompleted);

            writableBuffer = _pipe.Writer.WriteEmpty(PauseWriterThreshold);
            flushAsync = writableBuffer.FlushAsync();
            flushResult = flushAsync.GetAwaiter().GetResult();

            Assert.IsTrue(flushResult.IsCompleted);
            Assert.IsTrue(flushAsync.IsCompleted);
        }

        [TestMethod]
        public void FlushAsyncReturnsCompletedTaskWhenSizeLessThenLimit()
        {
            PipeWriter writableBuffer = _pipe.Writer.WriteEmpty(ResumeWriterThreshold);
            ValueTask<FlushResult> flushAsync = writableBuffer.FlushAsync();
            Assert.IsTrue(flushAsync.IsCompleted);
            FlushResult flushResult = flushAsync.GetAwaiter().GetResult();
            Assert.IsFalse(flushResult.IsCompleted);
        }

        [TestMethod]
        public void FlushAsyncReturnsNonCompletedSizeWhenCommitOverTheLimit()
        {
            PipeWriter writableBuffer = _pipe.Writer.WriteEmpty(PauseWriterThreshold);
            ValueTask<FlushResult> flushAsync = writableBuffer.FlushAsync();
            Assert.IsFalse(flushAsync.IsCompleted);
        }

        [TestMethod]
        public async Task FlushAsyncThrowsIfReaderCompletedWithException()
        {
            _pipe.Reader.Complete(new InvalidOperationException("Reader failed"));

            PipeWriter writableBuffer = _pipe.Writer.WriteEmpty(PauseWriterThreshold);
            InvalidOperationException invalidOperationException =
                await Assert.ThrowsAsync<InvalidOperationException>(async () => await writableBuffer.FlushAsync());
            Assert.AreEqual("Reader failed", invalidOperationException.Message);
            invalidOperationException =
                await Assert.ThrowsAsync<InvalidOperationException>(async () => await writableBuffer.FlushAsync());
            Assert.AreEqual("Reader failed", invalidOperationException.Message);
        }
    }


}