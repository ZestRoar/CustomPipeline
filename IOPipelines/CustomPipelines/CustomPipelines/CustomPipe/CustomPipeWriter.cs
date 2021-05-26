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


        public void CompleteAsync(Exception? exception = null) => _pipe.BeginCompleteWriter(exception);

        public bool FlushAsync() => _pipe.FlushAsync();

        public StateResult FlushResult() => _pipe.FlushResult();

        public void Advance(int bytes) => _pipe.Advance(bytes);

        public Memory<byte> GetMemory(int sizeHint = 0) => _pipe.GetWriterMemory(sizeHint);

        public Span<byte> GetSpan(int sizeHint = 0) => _pipe.GetWriterSpan(sizeHint);

        public bool WriteAsync(ReadOnlyMemory<byte> source) => _pipe.WriteAsync(source);

        public StateResult WriteResult() => _pipe.WriteResult();   // write의 flush_result가 필요할 수도 있음


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
