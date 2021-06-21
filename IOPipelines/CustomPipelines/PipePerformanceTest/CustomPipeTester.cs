using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CustomPipelines;

namespace PipePerformanceTest
{
    public class CustomPipeTester
    {
        private CustomPipe customPipe;
        private bool writeSet = false;
        private bool readSet = false;

        private int writeCount = 0;
        private int readCount = 0;

        private Object sync = new object();

        public CustomPipeTester()
        {
            customPipe = new CustomPipe();
        }

        public Memory<byte> GetWriterMemory(int bytes)
        {
            return this.customPipe.GetWriterMemory(bytes).Value;
        }

        public void Advance(int bytes)
        {
            lock (sync)
            {
                if (this.customPipe.TryAdvance(bytes) == false)
                {
                   
                    this.customPipe.Advance(bytes).OnCompleted(this.WriteCallback);
                }
                else
                {
                    this.WriteCallback();
                }
            }

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
            Console.WriteLine($"Advance : {writeCount.ToString()}");
            this.writeSet = true;
        }

        public void Read(FileStream fileStream, int bytes)
        {
            lock (sync)
            {
                if (this.customPipe.TryRead(out var result, bytes))
                {
                    ReadCallback(fileStream, result);
                }
                else
                {
                    this.customPipe.Read(bytes).Then((results) => { ReadCallback(fileStream, results); });
                }
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
        public void ReadCallback(FileStream fileStream, ReadResult results)
        {
            foreach (var segment in results.Buffer)
            {
                fileStream.Write(segment.Span);
            }

            ++readCount;
            Console.WriteLine($"AdvanceTo : {readCount.ToString()}");
            lock (sync)
            {
                this.customPipe.AdvanceTo(this.customPipe.Buffer.GetPosition(256));
            }

            this.readSet = true;
        }
    }
}
