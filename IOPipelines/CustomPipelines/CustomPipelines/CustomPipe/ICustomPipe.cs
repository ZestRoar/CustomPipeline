using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomPipelines
{
    internal interface ICustomPipeline
    {
        public bool Write(byte[] buffer, int offset, int count);
        public bool WriteAsync(Stream? obj);
        public StateResult WriteResult();
        public StateResult Flush();
        public bool FlushAsync();
        public StateResult FlushResult();
        public bool Read();
        public bool ReadAsync();
        public StateResult ReadResult();


        public Memory<byte> GetWriterMemory(int sizeHint = 0);
        public Span<byte> GetWriterSpan(int sizeHint = 0);
        public long GetPosition();


        public void Advance(int bytes);
        public void AdvanceTo(SequencePosition endPosition);
        public void AdvanceTo(SequencePosition startPosition, SequencePosition endPosition);
        public void CompleteWriter(Exception? exception = null);
        public void CompleteReader(Exception? exception = null);
        public void BeginCompleteWriter(Exception? exception = null);
        public void BeginCompleteReader(Exception? exception = null);

        public void Reset();
    }
}
