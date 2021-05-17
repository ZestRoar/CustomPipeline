using CustomPipelines;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CustomPipelinesTest
{
    [TestClass]
    public class UnitTestBase
    {
        [TestMethod]
        public void TestMethodBase()
        {
        }

        [TestMethod]
        public void PipeTestMethod()
        {
            PipeTest test = new PipeTest();
            test.Dispose();
        }
    }
}
