using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MsPipeline;

namespace MsTest.MsPipeline
{
    public class MsPipeline : IMsPipeline
    {
        public void Advance(int bytes)
        {
            throw new NotImplementedException();
        }

        public void AdvanceTo(SequencePosition startPosition, SequencePosition endPosition)
        {
            throw new NotImplementedException();
        }

        public void BeginCompleteReader(Exception exception = null)
        {
            throw new NotImplementedException();
        }

        public void BeginCompleteWriter(Exception exception = null)
        {
            throw new NotImplementedException();
        }

        public void CompleteReader(Exception exception = null)
        {
            throw new NotImplementedException();
        }

        public void CompleteWriter(Exception exception = null)
        {
            throw new NotImplementedException();
        }

        public StateResult Flush()
        {
            throw new NotImplementedException();
        }

        public StateResult FlushAsync()
        {
            throw new NotImplementedException();
        }

        public long GetPosition()
        {
            throw new NotImplementedException();
        }

        public Memory<byte> GetWriterMemory(int sizeHint = 0)
        {
            throw new NotImplementedException();
        }

        public Span<byte> GetWriterSpan(int sizeHint = 0)
        {
            throw new NotImplementedException();
        }

        public StateResult Read()
        {
            throw new NotImplementedException();
        }

        public StateResult Read(out ReadOnlySequence<byte> buffer)
        {
            throw new NotImplementedException();
        }

        public StateResult ReadAsync(out ReadOnlySequence<byte> buffer)
        {
            throw new NotImplementedException();
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }

        public byte Write()
        {
            throw new NotImplementedException();
        }

        public byte WriteAsync(object obj)
        {
            throw new NotImplementedException();
        }
    }
}
