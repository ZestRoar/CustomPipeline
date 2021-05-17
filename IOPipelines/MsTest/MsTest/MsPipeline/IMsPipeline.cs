using System;
using System.Buffers;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MsPipeline
{
    internal interface IMsPipeline
    {

        public byte Write();
        public byte WriteAsync(object? obj);
        public StateResult Flush();
        public StateResult FlushAsync();
        public StateResult Read(out ReadOnlySequence<byte> buffer);
        public StateResult ReadAsync(out ReadOnlySequence<byte> buffer);


        public Memory<byte> GetWriterMemory(int sizeHint = 0);
        public Span<byte> GetWriterSpan(int sizeHint = 0);
        public long GetPosition();


        public void Advance(int bytes);
        public void AdvanceTo(SequencePosition startPosition, SequencePosition endPosition);
        public void CompleteWriter(Exception? exception = null);
        public void CompleteReader(Exception? exception = null);
        public void BeginCompleteWriter(Exception? exception = null);
        public void BeginCompleteReader(Exception? exception = null);

        public void Reset();
    }

    public readonly struct StateResult
    {
        internal readonly StateFlags flagByte;

        public StateResult(bool isCompleted, bool isCanceled)
        {
            flagByte = StateFlags.None;
            flagByte |= isCompleted ? StateFlags.Completed : StateFlags.None;
            flagByte |= isCanceled ? StateFlags.Canceled : StateFlags.None;
        }
        public bool IsCompleted => (flagByte & StateFlags.Canceled) != 0;
        public bool IsCanceled => (flagByte & StateFlags.Completed) != 0;
        public bool IsEnded => (flagByte & StateFlags.Ended) != 0;

    }
    
    [Flags]
    internal enum StateFlags : byte
    {
        None = 0x0,
        Canceled = 0x1,
        Completed = 0x2,
        Ended = 0x3
    }


}
