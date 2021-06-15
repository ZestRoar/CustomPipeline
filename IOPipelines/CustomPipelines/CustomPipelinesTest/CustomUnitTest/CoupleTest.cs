using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CustomPipelines;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CustomPipelinesTest
{

    [TestClass]
    public class CoupleTest
    {
        private CustomPipe pipe;
        private int testNum = 10000;
        private int writeNum = 10000;
        private int readNum = 10000;

        [TestMethod]
        public void SingleThreadTest()
        {
            pipe = new CustomPipe();

            testNum = 10000;
            writeNum = 10000;
            readNum = 10000;

            var writeThread = new Thread(WriteWorker);
            var readThread = new Thread(ReadWorker);
            
            writeThread.Start();
            readThread.Start();

            while (testNum>0)
            {
                testNum = writeNum + readNum;
            }

        }

        public void ReadWorker()
        {
            ReadAsync(4, true);
        }

        public void TargetBytesProcess(ReadOnlySequence<byte> buffer, bool readLength, int readBytes)
        {
            if (readLength)
            {
                int targetBytes = BitConverter.ToInt32(buffer.Slice(0, readBytes).ToArray());
                pipe.AdvanceTo(buffer.GetPosition(readBytes));

                ReadAsync(targetBytes, false);
            }
            else
            {
                using (StreamWriter readFile = new StreamWriter(@"..\readDump.txt", true))
                {
                    readFile.WriteLine(readBytes.ToString());
                }
                pipe.AdvanceTo(buffer.GetPosition(readBytes));
                readNum--;
            }
        }

        public void ReadAsync(int targetBytes, bool readLength)
        {
            if (readNum <= 0)
            {
                pipe.CompleteWriter();
            }
            if (this.pipe.TryRead(out var result, targetBytes))
            {
                this.TargetBytesProcess(result.Buffer.Value, readLength, targetBytes);
            }
            else
            {
                var buffer = result.Buffer.Value;
                this.pipe.Read(targetBytes)
                    .Then((result) => { this.TargetBytesProcess(buffer, readLength, targetBytes); });
            }
        }

        public void WriteWorker()
        {
            WriteAsync();
        }

        public void WriteAsync()
        {
            while (true)
            {
                if (writeNum <= 0)
                {
                    pipe.CompleteWriter();
                    break;
                }

                var rand = new Random();
                var randVal = rand.Next(1, 64);
                var memory = this.pipe.GetWriterMemory(randVal + 4);
                if (memory == null)
                {
                    throw new InvalidOperationException();
                }


                byte[] arrBytesInfo = BitConverter.GetBytes(randVal);
                byte[] arrBytes = new byte[randVal];
                ReadOnlyMemory<byte> sourceLength = new ReadOnlyMemory<byte>(arrBytesInfo);
                ReadOnlyMemory<byte> source = new ReadOnlyMemory<byte>(arrBytes);
                sourceLength.CopyTo(memory.Value);
                memory = memory.Value[4..];
                source.CopyTo(memory.Value);

                using (StreamWriter writeFile = new StreamWriter(@"..\writeDump.txt", true))
                {
                    writeFile.WriteLine(randVal.ToString());
                }

                // 대충 소켓으로부터 받는 코드
                if (this.pipe.TryAdvance(randVal + 4) == false)
                {
                    this.pipe.Advance(randVal + 4).OnCompleted(this.WriteAsync);
                    writeNum--;
                    break;
                }
                else
                {
                    this.WriteAsync();
                    break;
                }
            }
        }

       

    }
}
