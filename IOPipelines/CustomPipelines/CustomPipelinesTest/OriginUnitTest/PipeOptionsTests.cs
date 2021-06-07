using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.Serialization;
using CustomPipelines;


namespace CustomPipelinesTest
{
    [TestClass]
    public class PipeOptionsTests
    {
        
        public static IEnumerable<object[]> Default_ExpectedValues_MemberData()
        {
            yield return new object[] { CustomPipeOptions.Default };
            yield return new object[] { new CustomPipeOptions() };
            yield return new object[] { new CustomPipeOptions(  -1, -1, -1) };
        }

        public static T Throws<T>(string expectedParamName, Action action)
            where T : ArgumentException
        {
            T exception = Assert.ThrowsException<T>(action);

            Assert.AreEqual(expectedParamName, exception.ParamName);

            return exception;
        }

        [TestMethod]
        public void Default_ExpectedValues()
        {
            CustomPipeOptions options = CustomPipeOptions.Default;
            Assert.AreEqual(65536, options.PauseWriterThreshold);
            Assert.AreEqual(32768, options.ResumeWriterThreshold);
            Assert.AreEqual(4096, options.MinimumSegmentSize);

            options = new CustomPipeOptions();
            Assert.AreEqual(65536, options.PauseWriterThreshold);
            Assert.AreEqual(32768, options.ResumeWriterThreshold);
            Assert.AreEqual(4096, options.MinimumSegmentSize);

        }

        [TestMethod]
        public void InvalidArgs_Throws()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new CustomPipeOptions(pauseWriterThreshold: -2));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new CustomPipeOptions(resumeWriterThreshold: -2));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new CustomPipeOptions(pauseWriterThreshold: 50, resumeWriterThreshold: 100));
        }

        [TestMethod]
        [DataRow(-100)]
        [DataRow(-1)]
        [DataRow(0)]
        [DataRow(1)]
        public void InvalidArgs_NoThrow(int minimumSegmentSize)
        {
            // There's currently no validation performed on PipeOptions.MinimumSegmentSize.
            new CustomPipeOptions(minimumSegmentSize: minimumSegmentSize);
            new CustomPipeOptions(minimumSegmentSize: minimumSegmentSize);
            new CustomPipeOptions(minimumSegmentSize: minimumSegmentSize);
            new CustomPipeOptions(minimumSegmentSize: minimumSegmentSize);
        }
    }
}