using System;
using CustomPipelines;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CustomPipelinesTest
{
    [TestClass]
    public class PipeTest : IDisposable
    {
        protected const int MaximumSizeHigh = 65;

        protected const int MaximumSizeLow = 6;

        private readonly TestMemoryPool _pool;

        internal CustomPipe Pipe;

        public PipeTest(int pauseWriterThreshold = MaximumSizeHigh, int resumeWriterThreshold = MaximumSizeLow)
        {
            _pool = new TestMemoryPool();
            Pipe = new CustomPipe(
                new CustomPipeOptions(
                    _pool,
                    pauseWriterThreshold: pauseWriterThreshold,
                    resumeWriterThreshold: resumeWriterThreshold
                ));
        }

        public void Dispose()
        {
            Pipe.CompleteWriter();
            Pipe.CompleteReader();
            _pool.Dispose();
        }
    }
}