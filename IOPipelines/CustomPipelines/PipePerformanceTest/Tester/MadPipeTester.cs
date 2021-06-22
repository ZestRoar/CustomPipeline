using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MadPipeline;

namespace PipePerformanceTest
{
    class MadPipeTester
    {
        private Madline madPipe;
        private bool writeSet = false;
        private bool readSet = false;
        private int writeCount = 0;
        private int readCount = 0;
        public MadPipeTester()
        {
            madPipe = new Madline(MadlineOptions.Default);
        }

        public Memory<byte> GetWriterMemory(int bytes)
        {
            return this.madPipe.GetMemory(bytes);
        }

        public void Advance(int bytes)
        {
            this.madPipe.Advance(bytes);

            this.madPipe.WriteSignal().OnCompleted(this.WriteCallback);

            while (true)
            {
                if (this.writeSet)
                {
                    this.writeSet = false;
                    break;
                }
            }
        }
        public void WriteCallback()
        {
            ++writeCount;
            //Console.WriteLine($"Advance : {writeCount.ToString()}");
            this.writeSet = true;
        }

        public void Read(FileStream fileStream, int bytes)
        {
            if (this.madPipe.TryRead(out var result) == 0)
            {
                this.madPipe.DoRead().Then((results)=>{ ReadCallback(fileStream, results, bytes); });
            }
            else
            {
                ReadCallback(fileStream, result, bytes);
            }

            while (true)
            {
                if (this.readSet)
                {
                    this.readSet = false;
                    break;
                }
            }
        }
        public void ReadCallback(FileStream fileStream, ReadOnlySequence<byte> results, int bytes)
        {
            var remains = bytes;
            foreach (var segment in results)
            {
                var length = segment.ToArray().Length;
                if (remains > length)
                {
                    remains -= length;
                    fileStream.Write(segment.ToArray(), 0, length);
                    continue;
                }
                else
                {
                    fileStream.Write(segment.ToArray(), 0, remains);
                    break;
                }
            }

            ++readCount;
            //Console.WriteLine($"AdvanceTo : {readCount.ToString()}");
            this.madPipe.AdvanceTo(results.GetPosition(bytes));

            this.readSet = true;
        }

        public void CompleteWriter()
        {
            this.madPipe.CompleteWriter();
        }
        public void CompleteReader()
        {
            this.madPipe.CompleteReader();
        }
    }
}
