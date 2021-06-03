using System;
using System.IO.Pipelines;
using Mad.Core.Concurrent.Synchronization;

namespace CustomPipelines
{
    interface IPipelineReference
    {
        public Memory<byte> GetMemory(int hintSize);
        public bool TryAdvance(int length);
        public Signal Advance(int length);
        public bool TryRead(System.IO.Pipelines.ReadResult result);
        public Future<System.IO.Pipelines.ReadResult> Read();
        public void AdvanceTo(SequencePosition position);
    }
}
