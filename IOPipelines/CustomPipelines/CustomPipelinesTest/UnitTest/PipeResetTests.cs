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
            _pipe = new CustomPipe(new CustomPipeOptions(_pool));
        }
        public void Dispose()
        {
            _pipe.CompleteWriter();
            _pipe.CompleteReader();
            _pool?.Dispose();
        }

        private readonly TestMemoryPool _pool;
        private readonly CustomPipe _pipe;

        [TestMethod]
        public void ReadsAndWritesAfterReset()
        {
            var source = new byte[] { 1, 2, 3 };

            while (_pipe.WriteAsync(source)) { }
            while (_pipe.ReadAsync()) { }

            StateResult result = _pipe.ReadResult();

            Assert.AreEqual(source, _pipe.GetReaderSpan().ToArray());
            _pipe.AdvanceToEnd();

            _pipe.CompleteReader();
            _pipe.CompleteWriter();

            _pipe.Reset();

            while (_pipe.WriteAsync(source)) { }
            while (_pipe.ReadAsync()) { }

            result = _pipe.ReadResult();

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