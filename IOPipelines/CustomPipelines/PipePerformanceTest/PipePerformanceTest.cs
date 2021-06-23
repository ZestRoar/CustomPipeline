using System;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Linq;

namespace PipePerformanceTest
{
    public enum PipeBrand {CUSTOM, MAD, ORIGIN}

    public class PipePerformanceTest
    {
        private const string srcFileName = "../../../testFile.tmp";
        private const string destFileName = "../../../destFile.tmp";

        private PipeBrand brand;

        private Thread writeThread;
        private Thread readThread;
        private String targetFile;

        private OriginPipeTester originTester;
        private CustomPipeTester customTester;
        private MadPipeTester madTester;

        private PerformanceHelper testHelper;

        private int writtenBytes = 0;

        private bool writeEnd = false;
        private bool readEnd = false;

        private long TargetBytes { get; set; }

        public PipePerformanceTest()
        {
            targetFile = srcFileName;
        }

        public void InitializeSelection()
        {
            Console.WriteLine("테스트 파이프라인을 선택해주세요.");
            Console.WriteLine("1. CustomPipe  2. System.IO.Pipe  3. Madline");


            while (true)
            {
                ConsoleKeyInfo keyInfo = Console.ReadKey(false);

                switch (keyInfo.Key)
                {
                    case ConsoleKey.D1:
                        InitializeTargetPipe(PipeBrand.CUSTOM);
                        return;
                    case ConsoleKey.D2:
                        InitializeTargetPipe(PipeBrand.ORIGIN);
                        return;
                    case ConsoleKey.D3:
                        InitializeTargetPipe(PipeBrand.MAD);
                        return;
                }
            }

        }

        public void InitializeTargetPipe(PipeBrand testTarget)
        {
            testHelper = new PerformanceHelper();
            
            this.brand = testTarget;

            this.writeThread = new Thread(PipeWriterWork);
            this.readThread = new Thread(PipeReaderWork);

            switch (this.brand)
            {
                case PipeBrand.CUSTOM:
                    this.customTester = new CustomPipeTester();
                    break;
                case PipeBrand.MAD:
                    this.madTester = new MadPipeTester();
                    break;
                default:
                    this.originTester = new OriginPipeTester();
                    break;
            }
        }

        public void RunFileCopy(String filename = srcFileName)
        {
            Console.Clear();
            Console.WriteLine("테스트를 진행합니다.");


            testHelper.StartTimer();

            this.targetFile = filename;
            this.TargetBytes = (new FileInfo(this.targetFile)).Length;

            this.readThread.Start(this);
            this.writeThread.Start(this);

            while (readEnd == false || writeEnd == false)
            {
                this.testHelper.CheckAllUsage();
                Thread.Sleep(500);
            }

            testHelper.StopTimer();

            Console.WriteLine("테스트가 종료되었습니다.");

        }

        public bool CheckFile()
        {
            Console.WriteLine("파일이 제대로 복사되었는지 검사합니다.");

            var srcFile = new FileStream(this.targetFile, FileMode.Open);
            var destFile = new FileStream(destFileName, FileMode.Open);

            if (srcFile.Length == destFile.Length)
            {
                int srcReadBytes = 0;
                int destReadBytes = 0;
                do
                {
                    srcReadBytes = srcFile.ReadByte();
                    destReadBytes = destFile.ReadByte();
                } while ((srcReadBytes == destReadBytes) && (srcReadBytes != -1));

                srcFile.Close();
                destFile.Close();

                if (srcReadBytes - destReadBytes == 0)
                {
                    return true;
                }
                return false;
            }
            else
            {
                return false;
            }
        }
        public void DumpResult()
        {
            Console.WriteLine("");
            Console.WriteLine("======== 테스트 결과 ========");
            using (StreamWriter readFile = new StreamWriter(@"..\..\..\DumpLog.txt", true))
            {
                switch (this.brand)
                {
                    case PipeBrand.CUSTOM:
                        readFile.WriteLine($"test : CustomPipelines");
                        Console.WriteLine($"대상 : CustomPipelines");
                        break;
                    case PipeBrand.MAD:
                        readFile.WriteLine($"test : MadPipelines");
                        Console.WriteLine($"대상 : MadPipelines");
                        break;
                    case PipeBrand.ORIGIN:
                        readFile.WriteLine($"test : System.IO.Pipelines");
                        Console.WriteLine($"대상 : System.IO.Pipelines");
                        break;
                }
                readFile.WriteLine($"target : {this.targetFile}");
                var size = new FileInfo(this.targetFile).Length;
                size = size / 1024 / 1024;
                readFile.WriteLine($"size : {size.ToString()} MB");
                readFile.WriteLine($"time : {this.testHelper.ElapsedToStringFormat()}");
                readFile.WriteLine($"cpu rate : {this.testHelper.GetCPUInfo()}");
                readFile.WriteLine($"ram usage : {this.testHelper.GetMemoryInfo()}");
                readFile.WriteLine($"diskIO : {this.testHelper.GetDiskIOInfo()}");
                readFile.WriteLine("");
                Console.WriteLine($"대상 파일 : {this.targetFile}");
                Console.WriteLine($"복사 크기 : {size.ToString()} MB");
                Console.WriteLine($"걸린 시간 : {this.testHelper.ElapsedToStringFormat()}");
                Console.WriteLine($"CPU 사용률 : {this.testHelper.GetCPUInfo()}");
                Console.WriteLine($"RAM 사용량 : {this.testHelper.GetMemoryInfo()}");
                Console.WriteLine($"디스크 입출력 : {this.testHelper.GetDiskIOInfo()}");
                Console.WriteLine("");
            }
        }

        public static void PipeWriterWork(object? test)
        {
            var testPipe = (PipePerformanceTest) test;
            var srcFile = testPipe.OpenSrcFile();

            var readBytes = srcFile.Length;

            while (readBytes > 0)
            {
                var memory = testPipe.GetWriterMemory(4096);
                var advanceBytes = srcFile.Read(memory.Span);
                readBytes -= advanceBytes;
                //Console.WriteLine($"advance : {advanceBytes.ToString()}");
                testPipe.Advance(advanceBytes);
                Interlocked.Add(ref testPipe.writtenBytes, advanceBytes);
            }

            srcFile.Close();
            Console.WriteLine("write finished");
            testPipe.CompleteWriter();
            testPipe.writeEnd = true;
        }
        public static void PipeReaderWork(object? test)
        {
            var testPipe = (PipePerformanceTest) test;
            var destFile = testPipe.OpenDestFile();

            var remainBytes = (int) (testPipe.TargetBytes - destFile.Length);
            while (remainBytes > 0)
            {
                //Console.WriteLine($"consume : {testPipe.writtenBytes.ToString()}");
                var readBytes = Interlocked.Exchange(ref testPipe.writtenBytes, 0);
                if (readBytes == 0)
                {
                    continue;
                }

                if (readBytes > remainBytes)
                {
                    readBytes = remainBytes;
                }

                //Console.WriteLine($"advanceTo : {readBytes.ToString()}, {fileLength.ToString()}");
                testPipe.Read(destFile, readBytes);
                remainBytes = (int)(testPipe.TargetBytes - destFile.Length);
            }

            destFile.Close();
            Console.WriteLine("read finished");
            testPipe.CompleteReader();
            testPipe.readEnd = true;
        }

        public FileStream OpenSrcFile()
        {
            return new FileStream(this.targetFile, FileMode.Open);
        }
        public FileStream OpenDestFile()
        {
            return new FileStream(destFileName, FileMode.Create);
        }

        public Memory<byte> GetWriterMemory(int bytes)
        {
            switch (this.brand)
            {
                case PipeBrand.CUSTOM:
                    return customTester.GetWriterMemory(bytes);
                case PipeBrand.MAD:
                    return madTester.GetWriterMemory(bytes);
                case PipeBrand.ORIGIN:
                    return originTester.GetWriterMemory(bytes);
            }

            return null;
        }

        public void Advance(int bytes)
        {
            switch (this.brand)
            {
                case PipeBrand.CUSTOM:
                    this.customTester.Advance(bytes);
                    break;
                case PipeBrand.MAD:
                    this.madTester.Advance(bytes);
                    break;
                default:
                    this.originTester.Advance(bytes);
                    break;
            }
        }

       

        public void Read(FileStream fileStream, int bytes)
        {
            switch (this.brand)
            {
                case PipeBrand.CUSTOM:
                    this.customTester.Read(fileStream, bytes);
                    break;
                case PipeBrand.MAD:
                    this.madTester.Read(fileStream, bytes);
                    break;
                default:
                    this.originTester.Read(fileStream, bytes);
                    break;
            }
        }

        public void CompleteWriter()
        {
            switch (this.brand)
            {
                case PipeBrand.CUSTOM:
                    this.customTester.CompleteWriter();
                    break;
                case PipeBrand.MAD:
                    this.madTester.CompleteWriter();
                    break;
                default:
                    this.originTester.CompleteWriter();
                    break;
            }
        }

        public void CompleteReader()
        {
            switch (this.brand)
            {
                case PipeBrand.CUSTOM:
                    this.customTester.CompleteReader();
                    break;
                case PipeBrand.MAD:
                    this.madTester.CompleteReader();
                    break;
                default:
                    this.originTester.CompleteReader();
                    break;
            }
        }



    }
}
