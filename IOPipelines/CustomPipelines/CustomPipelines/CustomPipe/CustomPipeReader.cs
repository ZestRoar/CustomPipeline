using System;

namespace CustomPipelines
{
    internal class CustomPipeReader
    {
        //private PipeReaderStream? _stream;
        private readonly CustomPipe _pipe;

        public CustomPipeReader(CustomPipe pipe)
        {
            _pipe = pipe;
        }

        public bool TryRead(out StateResult result) => _pipe.TryRead(out result);

        public bool ReadAsync() => _pipe.Read();

        public void AdvanceTo(SequencePosition consumed) => _pipe.AdvanceTo(consumed);

        public void AdvanceTo(SequencePosition consumed, SequencePosition examined) => _pipe.AdvanceTo(consumed, examined);

        public void Complete(Exception? exception = null) => _pipe.CompleteReader(exception);
        
    }
}
