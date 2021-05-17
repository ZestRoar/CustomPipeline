using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomPipelines
{
    public sealed class CustomPipe : ICustomPipeline
    {
        internal const int InitialSegmentPoolSize = 16; // 65K
        public const int MaxSegmentPoolSize = 256; // 1MB
        
        private BufferSegmentStack _bufferSegmentPool;

        private readonly CustomPipeOptions _options;
        public readonly int Length;
        private CustomPipeReader _reader;
        private CustomPipeWriter _writer;

        public CustomPipe() : this(CustomPipeOptions.Default)
        {
        }
        public CustomPipe(CustomPipeOptions options)
        {
            if (options == null)
            {
                //ThrowHelper.ThrowArgumentNullException(ExceptionArgument.options);
            }

            _bufferSegmentPool = new BufferSegmentStack(options.InitialSegmentPoolSize);

            _options = options;
            //_reader = new DefaultPipeReader(this);
            //_writer = new DefaultPipeWriter(this);
        }




        public void Advance(int bytes)
        {
            throw new NotImplementedException();
        }

        public void AdvanceToEnd()
        {
            throw new NotImplementedException();    // End 보관이 모양이 좋지 않으면 End 점을 구할 수 있는 법을 만들어야 함
        }
        public void AdvanceTo(SequencePosition endPosition)
        {
            throw new NotImplementedException();
        }
        public void AdvanceTo(SequencePosition startPosition, SequencePosition endPosition)
        {
            throw new NotImplementedException();
        }

        public void BeginCompleteReader(Exception exception = null)
        {
            throw new NotImplementedException();
        }

        public void BeginCompleteWriter(Exception exception = null)
        {
            throw new NotImplementedException();
        }

        public bool TryRead(out StateResult result)
        {
            throw new NotImplementedException();
        }

        public void CompleteReader(Exception exception = null)
        {
            throw new NotImplementedException();
        }

        public void CompleteWriter(Exception exception = null)
        {
            throw new NotImplementedException();
        }

        public StateResult Flush()
        {
            throw new NotImplementedException();
        }

        public bool FlushAsync()
        {
            throw new NotImplementedException();
        }
        public StateResult FlushResult()
        {
            throw new NotImplementedException();
        }

        public long GetPosition()
        {
            throw new NotImplementedException();
        }

        public Memory<byte> GetWriterMemory(int sizeHint = 0)
        {
            throw new NotImplementedException();
        }

        public Span<byte> GetWriterSpan(int sizeHint = 0)
        {
            throw new NotImplementedException();
        }
        public Span<byte> GetReaderSpan()           // byte 배열 제공용도
        {
            throw new NotImplementedException();
        }

        public StateResult Read()
        {
            throw new NotImplementedException();
        }

        public bool ReadAsync()
        {
            throw new NotImplementedException();
        }
        public StateResult ReadResult()
        {
            throw new NotImplementedException();
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }

        public byte Write()
        {
            throw new NotImplementedException();
        }
        public byte Write(ReadOnlySpan<byte> span)
        {
            throw new NotImplementedException();
        }

        public bool WriteAsync(object obj)
        {
            throw new NotImplementedException();
        }
        public byte WriteResult(object obj)
        {
            throw new NotImplementedException();
        }

        public StateResult WriteEmpty(int bufferSize)
        {
            throw new NotImplementedException();
        }

        public object GetObject() // 세그먼트를 반환하는 용도
        {
            throw new NotImplementedException();
        }
    }
}
