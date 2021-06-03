using System;
using System.Net.Sockets;
using CustomPipelines;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CustomPipelinesTest
{
    [TestClass]
    public class SignalWriteTests
    {
        private CustomPipe pipeline = new();
        private SocketAsyncEventArgs receiveArgs = new();
        private Socket socket;

        [TestMethod]
        public void DirectSignalTest()
        {
            bool isSignalSet;
            void SignalCallback()
            {
                isSignalSet = true;
            }

            CustomPipe pipe = new CustomPipe(new CustomPipeOptions(128, 64));
            pipe.Writer.GetMemory(127);

            isSignalSet = false;
            
            var signal = pipe.Writer.Advance(127);
            signal.OnCompleted(SignalCallback);

            Assert.IsTrue(isSignalSet);
            
            pipe.AdvanceToEnd();

            pipe.Writer.GetMemory(128);

            isSignalSet = false;

            signal = pipe.Writer.Advance(128);      // threshold 대비 대용량 주의! 
            signal.OnCompleted(SignalCallback);          // 실제로 이렇게 쓰면 127개 다 사라지고 1개만 쓰거나 127개 기록해두고 1개 나중에 붙여서 써야함

            Assert.IsFalse(isSignalSet);

            pipe.AdvanceToEnd();

            Assert.IsTrue(isSignalSet);
        }

        [TestMethod]
        public void IndirectSignalTest()
        {
            bool isSignalSet;
            void SignalCallback()
            {
                isSignalSet = true;
            }

            CustomPipe pipe = new CustomPipe(new CustomPipeOptions(128, 64));
            pipe.Writer.GetMemory(127);

            isSignalSet = false;

            if (pipe.TryAdvance(127) == false)
            {
                pipe.Advance(127).OnCompleted(SignalCallback);
            }
            else
            {
                SignalCallback();
            }
            

            Assert.IsTrue(isSignalSet);

            pipe.AdvanceToEnd();

            pipe.Writer.GetMemory(128);

            isSignalSet = false;

            if (pipe.TryAdvance(128) == false)
            {
                pipe.Advance(128).OnCompleted(SignalCallback);
            }
            else
            {
                SignalCallback();
            }

            Assert.IsFalse(isSignalSet);

            pipe.AdvanceToEnd();

            Assert.IsTrue(isSignalSet);
        }

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
