using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CustomPipelines;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CustomPipelinesTest.UnitTest
{
    [TestClass]
    public class FuturePromiseTests
    {
        private CustomPipe pipeline = new();
        private int count = 5;

        [TestMethod]
        public void FuturePromiseTest()
        {
            bool isPromiseSet = false;
            void ReadCallback()
            {
                isPromiseSet = true;
            }

            TestCustomPipe pipe = new TestCustomPipe(new CustomPipeOptions());
            
            if (pipe.Reader.TryRead(out var result, 5))
            {
                 ReadCallback();
            }
            else
            {
                var buffer = result.Buffer.Value;
                pipe.Reader.Read(5)
                    .Then((readResult) =>
                    {
                        ReadCallback();
                    });
            }

            Assert.IsFalse(isPromiseSet);

            pipe.GetWriterMemory(5);
            pipe.TryAdvance(5);
            Assert.IsTrue(isPromiseSet);
        }

        [TestMethod]
        public void ProcessSend()
        {
            if (--count < 0)
                return;


            this.pipeline.GetWriterMemory(5);
            this.pipeline.TryAdvance(5);

            if (this.pipeline.Reader.TryRead(out var result, 5))
            {
                this.SendToSocket(result.Buffer.Value);
            }
            else
            {
                var buffer = result.Buffer.Value;
                this.pipeline.Reader.Read(5)
                    .Then((result) => { this.SendToSocket(buffer); });
            }

        }

        // result 로 pipe가 완료되었는지 캔슬되었는지 등등 검사 후 
        //스트림 내용 그대로 소켓에 Send 하는 작업
        public void SendToSocket(ReadOnlySequence<byte> buffer)
        {
            this.pipeline.Reader.AdvanceTo(buffer.End);
            this.ProcessSend();
        }



    }
}
