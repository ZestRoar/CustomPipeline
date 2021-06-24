namespace PipePerformanceTest
{
    using System;
    using System.IO;
    using System.Threading;
    using MadPipeline;
    using System.Buffers;

    public partial class PipePerformanceTest
    {
        private Madline madline;
        private IMadlineWriter madWriter;
        private IMadlineReader madReader;

        private FileStream srcFile;
        private FileStream destFile;

        private long writeRemainBytes;
        private long readRemainBytes;

        public void RunMadFileCopy(String filename = srcFileName)
        {
            var malineOptions = new MadlineOptions();
            this.madline = new Madline(malineOptions);
            this.madWriter = this.madline;
            this.madReader = this.madline;

            Console.Clear();
            Console.WriteLine("테스트를 진행합니다.");


            testHelper.StartTimer();

            this.targetFile = filename;
            this.srcFile = new FileStream(filename, FileMode.Open);
            this.destFile = new FileStream(destFileName, FileMode.Create);
            this.TargetBytes = (new FileInfo(this.targetFile)).Length;
            this.readRemainBytes = this.TargetBytes;
            this.writeRemainBytes = this.TargetBytes;

            this.writeThread = new Thread(MadStartWrite);
            this.readThread = new Thread(MadStartRead);
            this.writeThread.Start();
            this.readThread.Start();

            while (this.readEnd == false || this.writeEnd == false)
            {
                this.testHelper.CheckAllUsage();
                Thread.Sleep(500);
            }

            writeThread.Join();
            readThread.Join();
            this.testHelper.CheckAllUsage();

            testHelper.StopTimer();

            Console.WriteLine("테스트가 종료되었습니다.");

        }

        public void MadStartWrite()
        {
            while (this.writeRemainBytes > 0)
            {
                if (this.madline.State.IsWritingPaused == false)
                {
                    this.WriteProcess();
                }
            }
            srcFile.Close();
            Console.WriteLine("write finished");
            this.madline.CompleteWriter();
            this.writeEnd = true;
        }

        public void MadStartRead()
        {
            do
            {
                if (this.madline.State.IsReadingPaused == false)
                {
                    this.ReadProcess();
                }
            } while (this.readRemainBytes > 0);

            destFile.Close();
            Console.WriteLine("read finished");
            this.madline.CompleteReader();
            this.readEnd = true;
        }

        public void WriteProcess()
        {
            if (this.madline.WriteCheck())
            {
                var memory = this.madWriter.GetMemory(4096);
                var advanceBytes = srcFile.Read(memory.Span);
                this.writeRemainBytes -= advanceBytes;
                this.madWriter.Advance(advanceBytes);
                this.madWriter.Flush();
            }
            else
            {
                this.madWriter.WriteSignal().OnCompleted(
                    () =>
                    {
                        this.WriteProcess();
                    });
            }
        }

        public void ReadProcess()
        {
            if (this.madReader.TryRead(out var result, 0))
            {
                this.SendToFile(in result);
            }
            else
            {
                this.madReader.DoRead().Then(
                    readResult =>
                    {
                        this.SendToFile(in readResult);
                    });
            }
        }
        public void SendToFile(in ReadOnlySequence<byte> result)
        {
            var remains = result.Length;
            foreach (var segment in result)
            {
                var length = segment.ToArray().Length;
                this.readRemainBytes -= length;
                if (remains > length)
                {
                    this.destFile.Write(segment.ToArray(), 0, length);
                    continue;
                }
                else
                {
                    this.destFile.Write(segment.ToArray(), 0, (int)remains);
                    break;
                }
            }
            this.madReader.AdvanceTo(result.End);
        }

    }
}
