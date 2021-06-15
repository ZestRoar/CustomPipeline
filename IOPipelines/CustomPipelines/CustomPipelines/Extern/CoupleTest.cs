using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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

        public void CoupleThreadTest()
        {
            pipe = new CustomPipe();

            testNum = 1;
            writeNum = TESTCOUNT;
            readNum = TESTCOUNT;
            writeSet = true;
            readSet = true;

            writeArray = new int[TESTCOUNT];
            readArray = new int[TESTCOUNT];

            var writeThread = new Thread(WriteWorker);
            var readThread = new Thread(ReadWorker);

            readThread.Start();
            
            writeThread.Start();

            while (testNum > 0)
            {
                testNum = writeNum + readNum;
            }

            int i = 0;
            for (i = 0; i < TESTCOUNT; ++i)
            {
                if (readArray[i] != writeArray[i])
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
            while (readNum > 0)
            {
                if (readSet)
                {
                    readSet = false;
                    ReadAsync(SIZEOFINT, true);
                }
            }
        }

        public void TargetBytesProcess(ref ReadResult result, int readBytes)
        {
            playCount++;
            Console.WriteLine($"{writeNum.ToString()} : {readNum.ToString()} : {playCount.ToString()}");


            var buffer = result.Buffer.Value;

            var bufferLength = buffer.Length;
            var oldLength = bufferLength;
            bool readLength = true;
            int targetBytes = 0;

            while (true)
            {
                if (readLength)
                {
                    bool testBool = true;
                    while (testBool)
                    {
                        testBool = false;
                        try
                        {
                            var readSpan = buffer.FirstSpan;
                            Console.WriteLine($"스팬 : {readSpan.Length.ToString()} / {pipe.Buffer.Length}, {buffer.IsSingleSegment.ToString()}");
                            targetBytes = MemoryMarshal.Read<int>(readSpan);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"버그 : {ex.ToString()}");
                            testBool = true;
                        }
                    }

                    readLength = false;
                    if (bufferLength < targetBytes + SIZEOFINT)
                    {
                        break;
                    }

                }
                else
                {
                    using (StreamWriter readFile = new StreamWriter(@"..\readDump.txt", true))
                    {
                        readFile.WriteLine($"{TESTCOUNT - readNum} : {targetBytes.ToString()}");
                        Console.WriteLine($"read : {targetBytes.ToString()} + 4");
                    }

                    readArray[TESTCOUNT - readNum] = targetBytes;
                    readNum--;

                    bufferLength -= SIZEOFINT + targetBytes;
                    buffer = buffer.Slice(SIZEOFINT + targetBytes);

                    readLength = true;
                    if (bufferLength < SIZEOFINT)
                    {

                        break;
                    }
                }
            }

            var consumePosition = oldLength - bufferLength;
            if (consumePosition > 0)
            {
                var oldBufferLength = pipe.Buffer.Length;
                pipe.AdvanceTo(result.Buffer.Value.GetPosition(consumePosition));
                Console.WriteLine($"AdvanceTo : {consumePosition.ToString()} ( {oldBufferLength.ToString()} => {pipe.Buffer.Length.ToString()} )");
            }

            readSet = true;

        }

        public void ReadAsync(int targetBytes, bool readLength)
        {
            if (readNum <= 0)
            {
                pipe.CompleteWriter();
            }
            if (this.pipe.TryRead(out var result, targetBytes))
            {
                Console.WriteLine($"TryRead: {targetBytes.ToString()} / {result.Buffer.Value.Length}");
               
            }
            else
            {
                this.pipe.Read(targetBytes).Then((results)
                    => { Console.WriteLine($"ReadThen : {targetBytes.ToString()} / {result.Buffer.Value.Length}");
                        this.TargetBytesProcess(ref results, targetBytes); });
            }
        }

        public void WriteWorker()
        {
            while (writeNum > 0)
            {
                if (writeNum == 999980)
                {
                    Thread.Sleep(1000);
                }

                if (writeNum == 999960)
                {
                    Thread.Sleep(3000);
                }

                if (writeSet)
                {
                    writeSet = false;
                    WriteAsync();
                }
            }
        }

        public void WriteCallback()
        {
            writeSet = true;
            writeNum--;
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
                    writeFile.WriteLine($"{TESTCOUNT - writeNum} : {randVal.ToString()}");
                    Console.WriteLine($"write : {randVal.ToString()} + 4");
                    writeArray[TESTCOUNT - writeNum] = randVal;
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
