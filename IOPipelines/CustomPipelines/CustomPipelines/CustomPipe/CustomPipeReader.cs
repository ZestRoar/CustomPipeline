using System;
using System.Buffers;
using System.Threading;
using Mad.Core.Concurrent.Synchronization;

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

        

        public bool TryRead(out ReadResult result, int targetBytes) => this.readerPipe.TryRead(out result, targetBytes);

        public Future<ReadResult> Read(int targetBytes) => this.readerPipe.Read(targetBytes);

        public void AdvanceTo(SequencePosition consumed) => this.readerPipe.AdvanceTo(consumed);

        public void AdvanceTo(SequencePosition consumed, SequencePosition examined) => this.readerPipe.AdvanceTo(consumed, examined);

        public void Complete(Exception? exception = null) => this.readerPipe.CompleteReader(exception);
        
    }
}
