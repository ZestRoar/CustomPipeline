using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomPipelines
{
    public class CustomPipeWriter
    {
        //private PipeWriterStream? _stream;
        private readonly CustomPipe _pipe;

        public CustomPipeWriter(CustomPipe pipe)
        {
            _pipe = pipe;
        }
        // 
        public void Complete(Exception? exception = null) => _pipe.CompleteWriter(exception);

        public StateResult FlushAsync() => _pipe.Flush();

        public void Advance(int bytes) => _pipe.Advance(bytes);

        public Memory<byte> GetMemory(int sizeHint = 0) => _pipe.GetWriterMemory(sizeHint);

        public bool WriteAsync(ReadOnlyMemory<byte> source) => _pipe.Write(source);

        protected internal bool CopyFromAsync(Stream source)
        {
            while (true)
            {
                Memory<byte> buffer = GetMemory();
                //


            }
        }
    }
}
