using System;
using System.Buffers;
using System.Threading;
using Mad.Core.Concurrent.Synchronization;

namespace CustomPipelines
{
    internal interface ICustomPipeReader
    {
#nullable enable
        public bool TryRead(out ReadResult result, int targetBytes);
        public Future<ReadResult> Read(int targetBytes);
        public void AdvanceTo(SequencePosition consumed);
        public void AdvanceTo(SequencePosition consumed, SequencePosition examined);
        public void CompleteReader(Exception? exception = null);
    }
}
