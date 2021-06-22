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
            testHelper.StartTimer();

            this.targetFile = filename;
            this.TargetBytes = (new FileInfo(this.targetFile)).Length;

            this.writeThread.Start(this);
            this.readThread.Start(this);

            while (readEnd == false || writeEnd == false)
            {
                this.testHelper.CheckAllUsage();
                Thread.Sleep(1000);
            }

            testHelper.StopTimer();
        }

        public bool CheckFile()
        {
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
            using (StreamWriter readFile = new StreamWriter(@"..\..\..\DumpLog.txt", true))
            {
                switch (this.brand)
                {
                    case PipeBrand.CUSTOM:
                        readFile.WriteLine($"test : CustomPipelines");
                        break;
                    case PipeBrand.MAD:
                        readFile.WriteLine($"test : MadPipelines");
                        break;
                    case PipeBrand.ORIGIN:
                        readFile.WriteLine($"test : System.IO.Pipelines");
                        break;
                }
                readFile.WriteLine($"target : {this.targetFile}");
                readFile.WriteLine($"time : {testHelper.ElapsedToStringFormat()}");
                readFile.WriteLine($"cpu rate : {testHelper.GetCPUInfo()}");
                readFile.WriteLine($"ram usage : {testHelper.GetMemoryInfo()}");
                readFile.WriteLine($"diskIO : {testHelper.GetDiskIOInfo()}");
                readFile.WriteLine("");
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
                Interlocked.Exchange(ref testPipe.writtenBytes, testPipe.writtenBytes + advanceBytes);
                //Console.WriteLine($"advance : {advanceBytes.ToString()}");
                testPipe.Advance(advanceBytes);
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

            var writeBytes = testPipe.TargetBytes;

            while (writeBytes > destFile.Length)
            {
                var readBytes = Interlocked.Exchange(ref testPipe.writtenBytes, 0);
                if (readBytes == 0)
                {
                    continue;
                }

                if (readBytes > (int)(writeBytes - destFile.Length))
                {
                    readBytes = (int)(writeBytes - destFile.Length);
                }

                //Console.WriteLine($"advanceTo : {readBytes.ToString()}, {destFile.Length.ToString()}");
                testPipe.Read(destFile, readBytes);
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
                    //this.madTester.CompleteWriter();
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
                    //this.madTester.CompleteReader();
                    break;
                default:
                    this.originTester.CompleteReader();
                    break;
            }
        }



    }
}
