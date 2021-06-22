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
            
                if (this.customPipe.TryAdvance(bytes) == false)
                {
                    this.customPipe.Advance(bytes).OnCompleted(this.WriteCallback);
                }
                else
                {
                    this.WriteCallback();
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
            //Console.WriteLine($"Advance : {writeCount.ToString()}");
            this.writeSet = true;
        }

        public void Read(FileStream fileStream, int bytes)
        {

            if (this.customPipe.TryRead(out var result, bytes))
            {
                ReadCallback(fileStream, result, bytes);
            }
            else
            {
                this.customPipe.Read(bytes).Then((results) => { ReadCallback(fileStream, results, bytes); });
                this.customPipe.RequestRead();
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

        public void ReadCallback(FileStream fileStream, ReadResult results, int bytes)
        {
            var remains = bytes;
            foreach (var segment in results.Buffer)
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
            this.customPipe.AdvanceTo(this.customPipe.Buffer.GetPosition(bytes));

            this.readSet = true;
        }

        public void CompleteWriter()
        {
            this.customPipe.CompleteWriter();
        }
        public void CompleteReader()
        {
            this.customPipe.CompleteReader();
        }
    }
}
