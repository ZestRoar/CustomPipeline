using System;
using System.Buffers;
using System.Threading;

namespace CustomPipelines
{
    internal class CustomPipeReader
    {
        //private PipeReaderStream? _stream;
        private readonly CustomPipe readerPipe;

        public CustomPipeReader(CustomPipe pipe)
        {
            this.readerPipe = pipe;
        }

        

        public bool TryRead(out StateResult result) => this.readerPipe.TryRead(out result);

        public bool ReadAsync() => this.readerPipe.Read();

        public void AdvanceTo(SequencePosition consumed) => this.readerPipe.AdvanceTo(consumed);

        public void AdvanceTo(SequencePosition consumed, SequencePosition examined) => this.readerPipe.AdvanceTo(consumed, examined);

        public void Complete(Exception? exception = null) => this.readerPipe.CompleteReader(exception);
        
    }
}
