using System;
using System.Buffers;

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

        public bool ReadTrigger(Action readCallback, int targetBytes, bool repeat = false)
        {
            readerPipe.RegisterReadCallback(readCallback, targetBytes, repeat);

            if (readerPipe.Length < targetBytes)
            {
                return true;
            }

            this.readerPipe.Read();
            
            return false;
        }
        public bool ReadTrigger(out ReadOnlySequence<byte> readBuffer, 
            Action readCallback, int targetBytes, bool repeat = false)
        {
            readerPipe.RegisterReadCallback(readCallback, targetBytes, repeat);

            if (readerPipe.Length < targetBytes)
            {
                readBuffer = readerPipe.Buffer;
                return true;
            }

            this.readerPipe.Read();

            readBuffer = default;

            return false;
        }

        public bool TryRead(out StateResult result) => this.readerPipe.TryRead(out result);

        public bool ReadAsync() => this.readerPipe.Read();

        public void AdvanceTo(SequencePosition consumed) => this.readerPipe.AdvanceTo(consumed);

        public void AdvanceTo(SequencePosition consumed, SequencePosition examined) => this.readerPipe.AdvanceTo(consumed, examined);

        public void Complete(Exception? exception = null) => this.readerPipe.CompleteReader(exception);
        
    }
}
