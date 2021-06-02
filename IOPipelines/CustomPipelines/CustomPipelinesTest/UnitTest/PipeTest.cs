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

        internal TestCustomPipe Pipe;

        public PipeTest(int pauseWriterThreshold = MaximumSizeHigh, int resumeWriterThreshold = MaximumSizeLow)
        {
            Pipe = new TestCustomPipe(new CustomPipeOptions(
                    pauseWriterThreshold: pauseWriterThreshold,
                    resumeWriterThreshold: resumeWriterThreshold
                ));
        }

        public void Dispose()
        {
            Pipe.CompleteWriter();
            Pipe.CompleteReader();
        }
    }
}