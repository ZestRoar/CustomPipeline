using System;
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
            _pool = new TestMemoryPool();
            _pipe = new TestCustomPipe(_pool, new CustomPipeOptions());
        }
        public void Dispose()
        {
            _pipe.CompleteWriter();
            _pipe.CompleteReader();
            _pool?.Dispose();
        }

        private readonly TestMemoryPool _pool;
        private readonly TestCustomPipe _pipe;
        
        [TestMethod]
        public void ReadsAndWritesAfterReset()
        {
            var source = new byte[] { 1, 2, 3 };

            _pipe.Write(source);
            _pipe.Read();

            Assert.AreEqual(source, _pipe.GetReaderSpan().ToArray());
            _pipe.AdvanceToEnd();

            _pipe.CompleteReader();
            _pipe.CompleteWriter();

            _pipe.Reset();

            _pipe.Write(source);
            _pipe.Read();

            Assert.AreEqual(source, _pipe.GetReaderSpan().ToArray());
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

            Assert.IsFalse(_pipe.TryRead(out StateResult result));
        }
    }
}