using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CustomPipelines;
using CustomPipelinesTest;

namespace CustomPipelines
{
    internal class TestCustomPipe : CustomPipe
    {

        public TestCustomPipe(CustomPipeOptions options) : base(options)
        {

        }

        public bool WriteEmpty(int writeBytes)
        {
            this.GetWriterMemory(writeBytes)?.Span[..writeBytes].Clear();
            Advance(writeBytes);
            return true;
        }

        public Span<byte> GetReaderSpan()
        {
            return new Span<byte>();
        }
        public ReadResult Flush()
        {
            return new ReadResult(false, CommitWrittenBytes());
        }
    }
}
