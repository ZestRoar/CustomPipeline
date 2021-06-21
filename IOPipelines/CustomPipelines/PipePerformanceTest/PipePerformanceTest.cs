using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using CustomPipelines;
using ReadResult = CustomPipelines.ReadResult;

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


        private long TargetBytes { get; set; }

        public PipePerformanceTest()
        {
            targetFile = srcFileName;
        }

        public void InitializeTargetPipe(PipeBrand testTarget)
        {
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
            this.targetFile = filename;
            this.TargetBytes = (new FileInfo(this.targetFile)).Length;

            this.writeThread.Start(this);
            this.readThread.Start(this);

            this.writeThread.Join();
            this.readThread.Join();
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
                    destReadBytes = srcFile.ReadByte();
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
            using (StreamWriter readFile = new StreamWriter(@"..\DumpLog.txt", true))
            {
                readFile.WriteLine($"{this.targetFile}");
                readFile.WriteLine($"time : ");
                readFile.WriteLine($"cpu  : ");
                readFile.WriteLine($"ram  : ");
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
                var memory = testPipe.GetWriterMemory(256);
                var advanceBytes = srcFile.Read(memory.ToArray(),0,256);
                testPipe.Advance(advanceBytes);
                readBytes -= advanceBytes;
            }

            srcFile.Close();
        }
        public static void PipeReaderWork(object? test)
        {
            var testPipe = (PipePerformanceTest) test;
            var destFile = testPipe.OpenDestFile();

            var writeBytes = testPipe.TargetBytes;

            while (writeBytes > destFile.Length)
            {
                testPipe.Read(destFile);
            }

            destFile.Close();
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

       

        public void Read(FileStream fileStream)
        {
            switch (this.brand)
            {
                case PipeBrand.CUSTOM:
                    this.customTester.Read(fileStream, 256);
                    break;
                case PipeBrand.MAD:
                    this.madTester.Read(fileStream, 256);
                    break;
                default:
                    this.originTester.Read(fileStream, 256);
                    break;
            }
        }

    }
}
