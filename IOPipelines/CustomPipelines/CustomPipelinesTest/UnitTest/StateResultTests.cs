using System.Buffers;
using CustomPipelines;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CustomPipelinesTest
{
    [TestClass]
    public class StateResultTests
    {
        [TestMethod]
        public void ResultSequenceCanBeConstructed()
        {
            var buffer = new ReadOnlySequence<byte>(new byte[] { 1, 2, 3 });
            var result = new ReadResult( false, true);

            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, buffer.ToArray());
        }
        public void StateResultCanBeConstructed(bool cancelled, bool completed)
        {
            var result = new ReadResult(cancelled, completed);

            Assert.AreEqual(cancelled, result.IsCanceled);
            Assert.AreEqual(completed, result.IsCompleted);
        }
        [TestMethod]
        public void TestAll()
        {
            StateResultCanBeConstructed(true, true);
            StateResultCanBeConstructed(false, true);
            StateResultCanBeConstructed(true, false);
            StateResultCanBeConstructed(false, false);
            ResultSequenceCanBeConstructed();
        }

    }
}