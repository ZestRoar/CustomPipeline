using System;
using System.IO;
using Mad.Core.Concurrent.Synchronization;

namespace CustomPipelines
{
    internal class CustomPipeWriter
    {
#nullable enable
        private readonly CustomPipe writerPipe;
        public CustomPipeWriter(CustomPipe pipe)
        {
            this.writerPipe = pipe;
        }
        public void Complete(Exception? exception = null) => this.writerPipe.CompleteWriter(exception);
        public bool TryAdvance(int bytes) => this.writerPipe.TryAdvance(bytes);
        public Signal Advance(int bytes) => this.writerPipe.Advance(bytes);
        public Memory<byte>? GetMemory(int sizeHint = 0) => this.writerPipe.GetWriterMemory(sizeHint);
        public bool WriteAsync(ReadOnlyMemory<byte> source) => this.writerPipe.Write(source);
    }
}
