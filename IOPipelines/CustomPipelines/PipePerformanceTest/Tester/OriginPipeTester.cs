using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PipePerformanceTest
{
    public class OriginPipeTester
    {
        private Pipe originPipe;

        public OriginPipeTester()
        {
            originPipe = new Pipe();
        }

        public Memory<byte> GetWriterMemory(int bytes)
        {
            return this.originPipe.Writer.GetMemory(bytes);
        }

        public void Advance(int bytes)
        {
            this.originPipe.Writer.Advance(bytes);

            this.originPipe.Writer.FlushAsync(); 
        }

        public void Read(FileStream fileStream, int bytes)
        {
            if (this.originPipe.Reader.TryRead(out var result) == true)
            {
                ProcessCopy(fileStream, result, bytes);
            }
            else
            {
                _ = ReadTask(fileStream, bytes);
            }
        }

        public async Task ReadTask(FileStream fileStream, int bytes)
        {
            ReadResult result = await this.originPipe.Reader.ReadAsync();;

            ProcessCopy(fileStream, result, bytes);
        }

        public void ProcessCopy(FileStream fileStream, ReadResult results, int bytes)
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

            this.originPipe.Reader.AdvanceTo(results.Buffer.GetPosition(bytes));
        }

        public void CompleteWriter()
        {
            this.originPipe.Writer.Complete();
        }
        public void CompleteReader()
        {
            this.originPipe.Reader.Complete();
        }

    }
}
