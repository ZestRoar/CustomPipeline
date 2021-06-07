using System;
using System.Buffers;
using System.IO.Pipes;
using CustomPipelines;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CustomPipelinesTest
{
    [TestClass]
    public class PipeResetTests : IDisposable
    {
        public PipeResetTests()
        {
            _pipe = new TestCustomPipe(new CustomPipeOptions());
        }
        public void Dispose()
        {
            _pipe.CompleteWriter();
            _pipe.CompleteReader();
        }

        private readonly TestCustomPipe _pipe;
        
        [TestMethod]
        public void ReadsAndWritesAfterReset()
        {
            var source = new byte[] { 1, 2, 3 };

            _pipe.WriteAndCommit(source);
            _pipe.Read();
            CollectionAssert.AreEqual(source, _pipe.Buffer.ToArray());
            _pipe.AdvanceToEnd();

            _pipe.CompleteReader();
            _pipe.CompleteWriter();

            _pipe.Reset();

            _pipe.WriteAndCommit(source);
            _pipe.Read();

            CollectionAssert.AreEqual(source, _pipe.Buffer.ToArray());
            _pipe.AdvanceToEnd();
        }

        [TestMethod]
        public void ResetThrowsIfReaderNotCompleted()
        {
            _pipe.CompleteWriter();
            Assert.ThrowsException<InvalidOperationException>(() => _pipe.Reset());
        }

        [TestMethod]
        public void ResetThrowsIfWriterNotCompleted()
        {
            _pipe.CompleteReader();
            Assert.ThrowsException<InvalidOperationException>(() => _pipe.Reset());
        }

        [TestMethod]
        public void ResetResetsReaderAwaitable()
        {
            _pipe.CompleteReader();
            _pipe.CompleteWriter();

            _pipe.Reset();

            Assert.IsFalse(_pipe.TryRead(out ReadResult result,1));
        }
    }
}