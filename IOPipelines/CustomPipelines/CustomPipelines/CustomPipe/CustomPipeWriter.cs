using System;
using System.IO;
using Mad.Core.Concurrent.Synchronization;

namespace CustomPipelines
{
    internal interface ICustomPipeWriter
    {
#nullable enable
        public void CompleteWriter(Exception? exception = null);
        public bool TryAdvance(int bytes);
        public Signal Advance(int bytes);
        public Memory<byte>? GetWriterMemory(int sizeHint = 0);
        public bool Write(ReadOnlyMemory<byte> source);
    }
}
