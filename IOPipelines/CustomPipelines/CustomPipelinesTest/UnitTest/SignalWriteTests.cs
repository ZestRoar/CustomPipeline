using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using CustomPipelines;
using Mad.Core.Concurrent.Synchronization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CustomPipelinesTest
{
    class SignalWriteTests
    {
        private CustomPipe pipeline = new();
        private SocketAsyncEventArgs receiveArgs = new();
        private Socket socket;

        [TestMethod]
        public void ProcessReceive()
        {
            

            var memory = this.pipeline.Writer.GetMemory(1);
            if (memory == null)
            {
                throw new InvalidOperationException();
            }

            this.receiveArgs.SetBuffer(memory.Value);
            this.socket.ReceiveAsync(this.receiveArgs);
            var received = this.receiveArgs.Count;
            // 대충 소켓으로부터 받는 코드
            var signal = this.pipeline.Writer.Advance(received);
            signal.OnCompleted(() =>
            {
                this.ProcessReceive();
            });
        }

        [TestMethod]
        public void ProcessReceive2()
        {
            var memory = this.pipeline.Writer.GetMemory(1);
            if (memory == null)
            {
                throw new InvalidOperationException();
            }
            this.receiveArgs.SetBuffer(memory.Value);
            this.socket.ReceiveAsync(this.receiveArgs);
            var received = this.receiveArgs.Count;
            // 대충 소켓으로부터 받는 코드
            if (this.pipeline.TryAdvance(received) == false)
            {
                this.pipeline.Advance(received)
                    .OnCompleted(() =>
                    {
                        this.ProcessReceive2();
                    });
            }
            else
            {
                this.ProcessReceive2();
            }
        }
    }
}
