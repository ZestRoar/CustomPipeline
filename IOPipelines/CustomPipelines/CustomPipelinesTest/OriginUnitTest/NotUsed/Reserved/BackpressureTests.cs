using System;
using System.IO.Pipelines;
using System.IO.Pipes;
using System.Threading.Tasks;
using CustomPipelines;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReadResult = CustomPipelines.ReadResult;

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
            _pipe.WriteEmpty(PauseWriterThreshold);

            _pipe.Flush();

            Assert.IsFalse(_pipe.WriteResult.IsCompleted);

            _pipe.TryRead(out var result);
            SequencePosition consumed = result.Buffer.Value.GetPosition(33);
            _pipe.Reader.AdvanceTo(consumed, consumed);

            Assert.IsTrue(_pipe.WriteResult.IsCompleted);

            //FlushResult flushResult = flushAsync.GetAwaiter().GetResult();

            Assert.IsFalse(_pipe.WriteResult.IsCompleted);
        }

        [TestMethod]
        public void FlushAsyncAwaitableCompletesWhenReaderAdvancesExaminedUnderLow()
        {
            _pipe.WriteEmpty(PauseWriterThreshold);

            _pipe.Flush();

            Assert.IsFalse(_pipe.WriteResult.IsCompleted);

            _pipe.TryRead(out var result);
            SequencePosition examined = result.Buffer.Value.GetPosition(33);
            _pipe.Reader.AdvanceTo(result.Buffer.Value.Start, examined);

            Assert.IsTrue(_pipe.WriteResult.IsCompleted);
            //FlushResult flushResult = flushAsync.GetAwaiter().GetResult();
            Assert.IsFalse(_pipe.WriteResult.IsCompleted);
        }

        [TestMethod]
        public async Task CanBufferPastPauseThresholdButGetPausedEachTime()
        {
            const int loops = 5;

            //async Task WriteLoopAsync()
            {
                for (int i = 0; i < loops; i++)
                {
                    _pipe.WriteEmpty(PauseWriterThreshold);

                    _pipe.Flush();

                    Assert.IsFalse(_pipe.WriteResult.IsCompleted);

                    //await flushTask;
                }

                _pipe.Writer.Complete();
            }

            //Task writingTask = WriteLoopAsync();

            while (true)
            {
                _pipe.TryRead(out var result, 1);

                if (result.IsCompleted)
                {
                    _pipe.Reader.AdvanceTo(result.Buffer.Value.End);

                    Assert.AreEqual(PauseWriterThreshold * loops, result.Buffer.Value.Length);
                    break;
                }

                _pipe.Reader.AdvanceTo(result.Buffer.Value.Start, result.Buffer.Value.End);
            }

            //await writingTask;
        }

        [TestMethod]
        public void FlushAsyncAwaitableDoesNotCompletesWhenReaderAdvancesUnderHight()
        {
            _pipe.WriteEmpty(PauseWriterThreshold);
            _pipe.Flush();

            Assert.IsFalse(_pipe.WriteResult.IsCompleted);

            _pipe.TryRead(out var result, 1);
            SequencePosition consumed = result.Buffer.Value.GetPosition(ResumeWriterThreshold);
            _pipe.Reader.AdvanceTo(consumed, consumed);

            Assert.IsFalse(_pipe.WriteResult.IsCompleted);
        }

        [TestMethod]
        public void FlushAsyncAwaitableResetsOnCommit()
        {
            _pipe.WriteEmpty(PauseWriterThreshold);
            _pipe.Flush();

            Assert.IsFalse(_pipe.WriteResult.IsCompleted);

            _pipe.TryRead(out var result, 1);
            SequencePosition consumed = result.Buffer.Value.GetPosition(33);
            _pipe.Reader.AdvanceTo(consumed, consumed);

            Assert.IsTrue(_pipe.WriteResult.IsCompleted);
            //FlushResult flushResult = flushAsync.GetAwaiter().GetResult();
            Assert.IsFalse(_pipe.WriteResult.IsCompleted);

            _pipe.WriteEmpty(PauseWriterThreshold);
            _pipe.Flush();

            Assert.IsFalse(_pipe.WriteResult.IsCompleted);
        }

        [TestMethod]
        public void FlushAsyncReturnsCompletedIfReaderCompletes()
        {
            _pipe.WriteEmpty(PauseWriterThreshold);
            _pipe.Flush();

            Assert.IsFalse(_pipe.WriteResult.IsCompleted);

            _pipe.Reader.Complete();

            Assert.IsTrue(_pipe.WriteResult.IsCompleted);
            _pipe.Flush();
            Assert.IsTrue(_pipe.WriteResult.IsCompleted);

            _pipe.WriteEmpty(PauseWriterThreshold);
            _pipe.Flush();

            Assert.IsTrue(_pipe.WriteResult.IsCompleted);
            Assert.IsTrue(_pipe.WriteResult.IsCompleted);
        }

        [TestMethod]
        public async Task FlushAsyncReturnsCompletedIfReaderCompletesWithoutAdvance()
        {
            _pipe.WriteEmpty(PauseWriterThreshold);
            _pipe.Flush();

            Assert.IsFalse(_pipe.WriteResult.IsCompleted);

            _pipe.TryRead(out var result, 1);
            _pipe.Reader.Complete();

            Assert.IsTrue(_pipe.WriteResult.IsCompleted);
            _pipe.Flush();
            Assert.IsTrue(_pipe.WriteResult.IsCompleted);

            _pipe.WriteEmpty(PauseWriterThreshold);
            _pipe.Flush();

            Assert.IsTrue(_pipe.WriteResult.IsCompleted);
            Assert.IsTrue(_pipe.WriteResult.IsCompleted);
        }

        [TestMethod]
        public void FlushAsyncReturnsCompletedTaskWhenSizeLessThenLimit()
        {
            _pipe.WriteEmpty(PauseWriterThreshold);
            _pipe.Flush();
            Assert.IsTrue(_pipe.WriteResult.IsCompleted);
            _pipe.Flush();
            Assert.IsFalse(_pipe.WriteResult.IsCompleted);
        }

        [TestMethod]
        public void FlushAsyncReturnsNonCompletedSizeWhenCommitOverTheLimit()
        {
            _pipe.WriteEmpty(PauseWriterThreshold);
            _pipe.Flush();
            Assert.IsFalse(_pipe.WriteResult.IsCompleted);
        }

        [TestMethod]
        public async Task FlushAsyncThrowsIfReaderCompletedWithException()
        {
            _pipe.Reader.Complete(new InvalidOperationException("Reader failed"));

            _pipe.WriteEmpty(PauseWriterThreshold);
            InvalidOperationException invalidOperationException = Assert.ThrowsException<InvalidOperationException>(() => _pipe.Flush());
            Assert.AreEqual("Reader failed", invalidOperationException.Message);
            invalidOperationException = Assert.ThrowsException<InvalidOperationException>(() => _pipe.Flush());
            Assert.AreEqual("Reader failed", invalidOperationException.Message);
        }
    }


}