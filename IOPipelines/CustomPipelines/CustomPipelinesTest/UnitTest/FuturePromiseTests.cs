using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CustomPipelines;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CustomPipelinesTest.UnitTest
{
    class FuturePromiseTests
    {
        private CustomPipe pipeline = new();

        [TestMethod]
        public void ProcessSend()
        {
            if (this.pipeline.TryRead(out var result, 5))
            {
                this.SendToSocket(result.Buffer.Value);
            }
            else
            {
                var buffer = result.Buffer.Value;
                this.pipeline.Read(5)
                    .Then((result) =>
                    {
                        this.SendToSocket(buffer);
                    });
            }
        }
        // result 로 pipe가 완료되었는지 캔슬되었는지 등등 검사 후 
        //스트림 내용 그대로 소켓에 Send 하는 작업
        public void SendToSocket(ReadOnlySequence<byte> buffer)
        {
            this.pipeline.AdvanceTo(buffer.End);
            this.ProcessSend();
        }



    }
}
