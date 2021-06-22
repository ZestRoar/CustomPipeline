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
        } 

        public void Read(FileStream fileStream, int bytes)
        {
            if (this.originPipe.Reader.TryRead(out var result) == false)
            {
                this.originPipe.Reader.ReadAsync();
            }
            else
            {
                this.originPipe.Reader.AdvanceTo(result.Buffer.GetPosition(256));
            }

        }

    }
}
