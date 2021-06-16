using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace CustomPipelines
{
    public class CoupleTest
    {
        private const int TESTCOUNT = 1000000;
        private const int SIZEOFINT = 4;

        private CustomPipe pipe;
        private int testNum;
        private int writeNum = TESTCOUNT;
        private int readNum = TESTCOUNT;
        private int playCount = 0;

        private int[] writeArray;
        private int[] readArray;
        private bool writeSet = true;
        private bool readSet = true;

        private bool readLength = true;
        private int targetBytes = 4;

        public void CoupleThreadTest()
        {
            this.pipe = new CustomPipe();

            this.testNum = 1;
            this.writeNum = TESTCOUNT;
            this.readNum = TESTCOUNT;
            this.writeSet = true;
            this.readSet = true;

            this.writeArray = new int[TESTCOUNT];
            this.readArray = new int[TESTCOUNT];

            this.readLength = true;
            this.targetBytes = 4;
            var writeThread = new Thread(WriteWorker);
            var readThread = new Thread(ReadWorker);

            readThread.Start();
            
            writeThread.Start();

            while (testNum > 0)
            {
                this.testNum = this.writeNum + this.readNum;
            }

            var i = 0;
            for (i = 0; i < TESTCOUNT; ++i)
            {
                if (this.readArray[i] != this.writeArray[i])
                {
                    break;
                }
            }

            if (i == TESTCOUNT)
            {
                Console.WriteLine("정상 실행 완료");
            }
            else
            {
                Console.WriteLine("스레드 충돌");
            }
        }

        public void ReadWorker()
        {
            while (this.readNum > 0)
            {
                if (this.readNum == 999940)
                {
                    Console.WriteLine("Read Sleep For 1 Second");
                    Thread.Sleep(1000);
                }
                if (this.readSet)
                {
                    this.readSet = false;
                    ReadAsync();
                }
            }
        }

        public void TargetBytesProcess(ReadResult result)
        {
            int writeCount = TESTCOUNT - this.writeNum + 1;
            int readCount = TESTCOUNT - this.readNum + 1;
            this.playCount++;
            Console.WriteLine($"writeCount : {(writeCount).ToString()}, readCount : {readCount.ToString()}, processCount : {this.playCount.ToString()}");
            using (StreamWriter processFile = new StreamWriter(@"..\processDump.txt", true))
            {
                processFile.WriteLine($"writeCount : {(writeCount).ToString()}, readCount : {readCount.ToString()}, processCount : {this.playCount.ToString()}");
                //Console.WriteLine($"read : {targetBytes.ToString()} + 4");
            }


            if (writeCount < readCount)
            {
                int i = 0;
            }

            var buffer = result.Buffer.Value;

            var bufferLength = buffer.Length;
            var oldLength = bufferLength;
            
            while (true)
            {
                if (this.readLength)
                {
                    if (this.targetBytes != SIZEOFINT)
                    {
                        if (bufferLength < this.targetBytes)
                        {
                            break;
                        }

                        this.targetBytes -= SIZEOFINT;
                        this.readLength = false;
                        continue;
                    }

                    var sequenceReader = new SequenceReader<byte>(buffer);
                    var readArray = new byte[4];
                    for (int i = 0; i < 4; ++i)
                    {
                        if (sequenceReader.TryRead(out var item))
                        {
                            readArray[i] = item;
                        }
                        else
                        {
                            throw new Exception();
                        }
                    }

                    //Console.WriteLine($"스팬 : {buffer.Length}");
                    this.targetBytes = MemoryMarshal.Read<int>(readArray);

                    if (bufferLength < this.targetBytes + SIZEOFINT)
                    {
                        this.targetBytes = this.targetBytes + SIZEOFINT;
                        break;
                    }

                    this.readLength = false;
                }
                else
                {
                    using (StreamWriter readFile = new StreamWriter(@"..\readDump.txt", true))
                    {
                        readFile.WriteLine($"{TESTCOUNT - readNum} : {this.targetBytes.ToString()}");
                        //Console.WriteLine($"read : {targetBytes.ToString()} + 4");
                    }

                    this.readArray[TESTCOUNT - readNum] = this.targetBytes;
                    --this.readNum;

                    bufferLength -= SIZEOFINT + this.targetBytes;
                    buffer = buffer.Slice(SIZEOFINT + this.targetBytes);

                    this.readLength = true;
                    this.targetBytes = SIZEOFINT;
                    if (bufferLength < SIZEOFINT)
                    {

                        break;
                    }
                }
            }

            var consumePosition = oldLength - bufferLength;
            var oldBufferLength = this.pipe.Buffer.Length;
            this.pipe.AdvanceTo(result.Buffer.Value.GetPosition(consumePosition));
            Console.WriteLine($"AdvanceTo : {consumePosition.ToString()} ( {oldBufferLength.ToString()} => {this.pipe.Buffer.Length.ToString()} )");

            this.readSet = true;

        }

        public void ReadAsync()
        {
            if (this.readNum <= 0)
            {
                this.pipe.CompleteWriter();
            }
            if (this.pipe.TryRead(out var result, this.targetBytes))
            {
                Console.WriteLine("");
                Console.WriteLine($" [TryRead] : {this.targetBytes.ToString()} / {result.Buffer.Value.Length}");
                this.TargetBytesProcess(result);
            }
            else
            {
                Console.WriteLine("");
                Console.WriteLine(" [Read] ");
                this.pipe.Read(this.targetBytes).Then((results)
                    => { Console.WriteLine($"[ReadThen] : {this.targetBytes.ToString()} / {result.Buffer.Value.Length}");
                        this.TargetBytesProcess(results); });
            }
        }

        public void WriteWorker()
        {
            while (this.writeNum > 0)
            {
                if (this.writeNum == 999980)
                {
                    Console.WriteLine("Write Sleep For 1 Second");
                    Thread.Sleep(1000);
                }

                if (this.writeNum == 999960)
                {
                    Console.WriteLine("Write Sleep For 3 Seconds");
                    Thread.Sleep(3000);
                }

                if (this.writeSet)
                {
                    this.writeSet = false;
                    WriteAsync();
                }
            }
        }

        public void WriteCallback()
        {
            this.writeSet = true;
            --this.writeNum;
        }

        public void WriteAsync()
        {
            while (true)
            {
                if (this.writeNum <= 0)
                {
                    this.pipe.CompleteWriter();
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
                    writeFile.WriteLine($"{TESTCOUNT - writeNum} : {randVal.ToString()}");
                    //Console.WriteLine($"write : {randVal.ToString()} + 4");
                    this.writeArray[TESTCOUNT - writeNum] = randVal;
                }

                // 대충 소켓으로부터 받는 코드
                if (this.pipe.TryAdvance(randVal + 4) == false)
                {
                    this.pipe.Advance(randVal + 4).OnCompleted(this.WriteCallback);
                    
                    break;
                }
                else
                {
                    this.WriteCallback();
                    break;
                }
            }
        }



    }

}
