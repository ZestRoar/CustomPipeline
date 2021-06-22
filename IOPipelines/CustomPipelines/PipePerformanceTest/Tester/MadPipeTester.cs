using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PipePerformanceTest
{
    class MadPipeTester
    {
        //private MadPipe madPipe;

        public MadPipeTester()
        {
            //madPipe = new MadPipe();
        }

        public Memory<byte> GetWriterMemory(int bytes)
        {
            //return this.madPipe.Writer.GetMemory(bytes);
            return null;
        }

        public void Advance(int bytes)
        {
            //this.madPipe.Writer.Advance(bytes);
        }

        public void Read(FileStream fileStream, int bytes)
        {
            //if (this.madPipe.Reader.TryRead(out var result) == false)
            //{
            //    this.madPipe.Reader.ReadAsync();
            //}
            //else
            //{
            //    this.madPipe.Reader.AdvanceTo(result.Buffer.GetPosition(256));
            //}

        }

    }
}
