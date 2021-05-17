using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MsTest
{
    class PipeExtension
    {
        private readonly Pipe actorPipe;
        private readonly PipeReader readerPipe;
        private readonly PipeWriter writerPipe;
        private readonly PipeOptions optionsOfPipe;

        public PipeExtension()
        {
            optionsOfPipe = PipeOptions.Default;
            actorPipe = new Pipe(optionsOfPipe);
            readerPipe = actorPipe.Reader;
            writerPipe = actorPipe.Writer;
        }
        public PipeExtension(PipeOptions options)
        {
            optionsOfPipe = options;
            actorPipe = new Pipe(optionsOfPipe);
            readerPipe = actorPipe.Reader;
            writerPipe = actorPipe.Writer;
        }

        public int MinimumMemorySize { get; set; } = 1;

        public static ReadOnlySequence<byte> SliceBuffer(ReadResult result, SequencePosition position)
        {
            ReadOnlySequence<byte> retval = result.Buffer.Slice(0, position);
            result.Buffer.Slice(result.Buffer.GetPosition(1, position));
            return retval;
        }

        public Memory<byte> GetWriterMemory => writerPipe.GetMemory(MinimumMemorySize);
        public bool TryAdvance(int bytes) => bytes!=0 && Advance(bytes);
        public bool Advance(int bytes)
        {
            writerPipe.Advance(bytes);
            return true;
        }

        public FlushResult TryFlushTask()
        {
            var flushTask = Task.Run(() => writerPipe.FlushAsync());
            Task.WhenAll(flushTask);
            return flushTask.Result.Result;
        }

        public void CompleteWriter() => writerPipe.Complete();
        public void CompleteWriterAsync() => writerPipe.CompleteAsync();

        public bool TryRead(out ReadResult result)
        {
            return readerPipe.TryRead(out result);
        }
        public ReadResult TryReadTask()
        {
            var readTask = Task.Run(() => readerPipe.ReadAsync());
            Task.WhenAll(readTask);
            return readTask.Result.Result;
        }
        public void AdvanceTo(SequencePosition bytesStart, SequencePosition bytesEnd) => readerPipe.AdvanceTo(bytesStart, bytesEnd);
        public void CompleteReader() => readerPipe.Complete();
        public void CompleteReaderAsync() => readerPipe.CompleteAsync();

    }
}
