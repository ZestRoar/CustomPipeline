using System;
using System.IO;

namespace CustomPipelines
{
    internal class CustomPipeWriter
    {
        //private PipeWriterStream? _stream;
        private readonly CustomPipe writerPipe;

        public CustomPipeWriter(CustomPipe pipe)
        {
            this.writerPipe = pipe;
        }

        public bool Write(out Memory<byte>? writeMemory, int sizeHint,
                            Action writeCallback, bool repeat = false)
        {
            this.writerPipe.RegisterWriteCallback(writeCallback, repeat);

            writeMemory = this.writerPipe.GetWriterMemory(sizeHint);
            
            return writeMemory != null;
        }

        // 
        public void Complete(Exception? exception = null) => this.writerPipe.CompleteWriter(exception);

        public void Advance(int bytes) => this.writerPipe.Advance(bytes);

        public Memory<byte>? GetMemory(int sizeHint = 0) => this.writerPipe.GetWriterMemory(sizeHint);

        public bool WriteAsync(ReadOnlyMemory<byte> source) => this.writerPipe.Write(source);

    }
}
