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
        private readonly MemoryPool<byte>? _pool;

        public TestCustomPipe(CustomPipeOptions options) : base(options)
        {

        }
        public TestCustomPipe(TestMemoryPool testPool, CustomPipeOptions options)
        {
            _pool = testPool;
        }
        public TestCustomPipe(HeapBufferPool testPool, CustomPipeOptions options)
        {
            _pool = testPool;
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
        public StateResult Flush()
        {
            return new StateResult(false, CommitWrittenBytes());
        }
    }
}
