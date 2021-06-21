using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace CustomPipelines
{
    static class DebugManager
    {
        public static Object consoleSync = new ();
        public static bool fileDump = false;
        public static bool consoleDump = false;
    }

    public class CoupleTest
    {
        private const int TESTCOUNT = 1000000;
        private const int SIZEOFINT = 4;

        private CustomPipe pipe;
        private int testNum;
        private int updateNum;
        public int writeNum = TESTCOUNT;
        public int readNum = TESTCOUNT;
        private int playCount = 0;

        private int[] writeArray;
        private int[] readArray;
        private bool writeSet = true;

        private bool readLength = true;
        private int targetBytes = 4;

        private Thread writeThread;

        private delegate void TestMethod();

        private TestMethod readTest;
        private TestMethod writeTest;

        public void SelectTestMethod()
        {
            readTest = this.ReadAsyncNormalTest;
            //readTest = this.ReadAsyncSleepThenTest;

            //writeTest = this.WriteAsyncHonestByte;
            writeTest = this.WriteAsyncPartialByte;
        }


        public void CoupleThreadTest()
        {
            this.SelectTestMethod();

            this.pipe = new CustomPipe();

            this.testNum = 1;
            this.writeNum = TESTCOUNT;
            this.readNum = TESTCOUNT;
            this.writeSet = true;

            this.writeArray = new int[TESTCOUNT];
            this.readArray = new int[TESTCOUNT];

            this.readLength = true;
            this.targetBytes = 4;
            writeThread = new Thread(WriteWorker);
            

            ThreadPool.QueueUserWorkItem(ReadWorker, this);
            
            writeThread.Start();

            while (this.testNum > 0)
            {
                this.testNum = this.writeNum + this.readNum;
                if (DebugManager.consoleDump && this.updateNum != this.testNum)
                {
                    var percentTemp = (10000 - (this.testNum / 200));
                    Console.WriteLine(
                        $"test 진행률 {(percentTemp / 100).ToString()}.{(percentTemp % 100).ToString()}% ({this.writeNum.ToString()}, {this.readNum.ToString()})");
                }
                this.updateNum = this.testNum;
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

        public static void ReadWorker(object? test)
        {
            //Console.WriteLine("read start");
            var castTest = (CoupleTest) test;

            if (castTest.readNum == 999940)
            {
                Console.WriteLine("Read Sleep For 1 Second");
                Thread.Sleep(1000);
            }

            castTest.readTest();

            //Console.WriteLine("read end");

        }

        public void TargetBytesProcess(ReadResult result)
        {
            int writeCount = TESTCOUNT - this.writeNum + 1;
            int readCount = TESTCOUNT - this.readNum + 1;
            this.playCount++;
            
            if (DebugManager.consoleDump)
            {
                Console.WriteLine(
                    $"writeCount : {(writeCount).ToString()}, readCount : {readCount.ToString()}, processCount : {this.playCount.ToString()}");
            }

            if (DebugManager.fileDump)
            {
                using (StreamWriter processFile = new StreamWriter(@"..\processDump.txt", true))
                {
                    processFile.WriteLine(
                        $"writeCount : {(writeCount).ToString()}, readCount : {readCount.ToString()}, processCount : {this.playCount.ToString()}");
                    //Console.WriteLine($"read : {targetBytes.ToString()} + 4");
                }
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
                            ThreadPool.QueueUserWorkItem(ReadWorker, this);
                            return;      // 여기 들어오면 안되는데 들어오는 현상 발생해서 스피닝 처리 해둠
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
                    if (DebugManager.fileDump)
                    {
                        using (StreamWriter readFile = new StreamWriter(@"..\readDump.txt", true))
                        {
                            readFile.WriteLine($"{TESTCOUNT - readNum} : {this.targetBytes.ToString()}");
                            //Console.WriteLine($"read : {targetBytes.ToString()} + 4");
                        }
                    }

                    this.readArray[TESTCOUNT - readNum] = this.targetBytes;
                    --this.readNum;

                    this.targetBytes = SIZEOFINT + this.targetBytes;
                    bufferLength -= this.targetBytes;
                    //Console.WriteLine($"slice : -{this.targetBytes.ToString()}");
                    buffer = buffer.Slice(this.targetBytes);

                    this.readLength = true;
                    this.targetBytes = SIZEOFINT;
                    if (bufferLength < this.targetBytes)
                    {
                        break;
                    }
                }
            }

            var consumePosition = oldLength - bufferLength;
            var oldBufferLength = this.pipe.Buffer.Length;
            this.pipe.AdvanceTo(result.Buffer.Value.GetPosition(consumePosition));
            if (DebugManager.consoleDump)
            {
                Console.WriteLine(
                    $"AdvanceTo : {consumePosition.ToString()} ( {oldBufferLength.ToString()} => {this.pipe.Buffer.Length.ToString()} )");
            }

            ThreadPool.QueueUserWorkItem(ReadWorker, this);

        }

        public void ReadAsyncNormalTest()
        {
            if (this.readNum <= 0)
            {
                this.pipe.CompleteWriter();
            }
            if (this.pipe.TryRead(out var result, this.targetBytes) == false)
            {
                if (DebugManager.consoleDump)
                {
                    Console.WriteLine("");
                    Console.WriteLine(" [Read] ");
                }

                this.pipe.Read(this.targetBytes).Then((results)
                    =>
                {
                    if (DebugManager.consoleDump)
                    {
                        Console.WriteLine(
                            $"[ReadThen] : {this.targetBytes.ToString()} / {results.Buffer.Value.Length}");
                    }

                    this.TargetBytesProcess(results);
                }); 
            }
            else
            {
                if (DebugManager.consoleDump)
                {
                    Console.WriteLine("");
                    Console.WriteLine($" [TryRead] : {this.targetBytes.ToString()} / {result.Buffer.Value.Length}");
                }

                this.TargetBytesProcess(result);
            }
        }
        public void ReadAsyncSleepThenTest()
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

                var sleepFutureTest = this.pipe.Read(this.targetBytes);
                Thread.Sleep(1000);
                sleepFutureTest.Then((results)
                    =>
                {
                    Console.WriteLine($"[ReadThen] : {this.targetBytes.ToString()} / {result.Buffer.Value.Length}");
                    this.TargetBytesProcess(results);
                });

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
                    writeTest();
                }
            }
        }

        public void WriteCallback()
        {
            --this.writeNum;
            this.writeSet = true;
        }
        public void WriteCallbackPartial()
        {
            this.writeSet = true;
        }

        public void WriteAsyncHonestByte()
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

                using (StreamWriter writeFile = new StreamWriter(@"..\writeHonestDump.txt", true))
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
        public void WriteAsyncPartialByte()
        {
            while (true)
            {
                if (this.writeNum <= 0)
                {
                    this.pipe.CompleteWriter();
                    break;
                }

                var rand = new Random();
                var randVal = rand.Next(1, 64)+4;
                
                byte[] arrBytes = new byte[randVal];
                BitConverter.GetBytes(randVal-4).CopyTo(arrBytes.AsSpan());
                var source = new ReadOnlyMemory<byte>(arrBytes);

                var randPart = rand.Next(1, randVal);
                var randRemain = randVal;
                while (true)
                {
                    var memory = this.pipe.GetWriterMemory(randPart);
                    if (memory == null)
                    {
                        throw new InvalidOperationException();
                    }

                    var randPos = randVal - randRemain;
                    source.Slice(randPos, randPart).CopyTo(memory.Value);

                    if (DebugManager.fileDump)
                    {
                        using (StreamWriter writeFile = new StreamWriter(@"..\writePartialDump.txt", true))
                        {
                            writeFile.WriteLine(
                                $"{TESTCOUNT - writeNum} : {randPart.ToString()} ({randPos.ToString()}/{randVal.ToString()}) , total : {this.pipe.Length.ToString()}");
                        }
                    }

                    if (DebugManager.consoleDump)
                    {
                        Console.WriteLine(
                            $"Partial Write : {randPart.ToString()} ({randPos.ToString()} => {(randPos + randPart).ToString()}/{randVal.ToString()})");
                    }

                    this.writeArray[TESTCOUNT - writeNum] = randVal-4;

                    // 대충 소켓으로부터 받는 코드
                    if (this.pipe.TryAdvance(randPart) == false)
                    {
                        this.pipe.Advance(randPart).OnCompleted(this.WriteCallbackPartial);
                    }
                    else
                    {
                        this.WriteCallbackPartial();
                    }

                    while (writeSet == false)
                    {

                    }

                    writeSet = false;

                    randRemain = randRemain - randPart;
                    if (randRemain == 0)
                    {
                        --this.writeNum;
                        break;
                    }
                    randPart = rand.Next(1, randRemain);


                }

            }
        }



    }

}
