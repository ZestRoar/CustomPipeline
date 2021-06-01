using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CustomPipelines;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CustomPipelinesTest
{
    [TestClass]
    public class PipeReaderWriterFacts
    {
        public PipeReaderWriterFacts()
        {
            _pool = new TestMemoryPool();
            _pipe = new TestCustomPipe(_pool, new CustomPipeOptions());
        }
        public void Dispose()
        {
            _pipe.CompleteWriter();
            _pipe.CompleteReader();
            _pool?.Dispose();
        }

        private readonly TestCustomPipe _pipe;

        private readonly TestMemoryPool _pool;
        [TestMethod]
        public void CanReadAndWrite()
        {
            byte[] bytes = Encoding.ASCII.GetBytes("Hello World");

            _pipe.Write(bytes);
            _pipe.Read();
            ReadOnlySequence<byte> buffer = _pipe.Buffer;

            Assert.AreEqual(11, buffer.Length);
            Assert.IsTrue(buffer.IsSingleSegment);
            var array = new byte[11];
            buffer.FirstSpan.CopyTo(array);
            Assert.AreEqual("Hello World", Encoding.ASCII.GetString(array));

            _pipe.AdvanceTo(buffer.End);
        }

        [TestMethod]
        public void AdvanceResetsCommitHeadIndex()
        {
            _pipe.GetWriterMemory(1);
            _pipe.Advance(100);
            _pipe.Flush();

            _pipe.Read();
            _pipe.AdvanceTo(_pipe.Buffer.End);

            _pipe.Read();
            //Assert.IsFalse(_pipe.ReadResult().IsCompleted);

            _pipe.Write(new byte[1]);
            _pipe.Flush();

            //Assert.IsTrue(_pipe.ReadResult().IsCompleted);

            _pipe.Read();
            _pipe.AdvanceTo(_pipe.Buffer.End);

            _pipe.Read();
           // Assert.IsFalse(_pipe.ReadResult().IsCompleted);

        }

        [TestMethod]
        public void AdvanceShouldResetStateIfReadCanceled()
        {
            // 캔슬 테스트임 나중에 참고
        }

        [TestMethod]
        public void AdvanceToInvalidCursorThrows()
        {
            // 캔슬 테스트임 나중에 참고
        }

        [TestMethod]
        public void AdvanceWithGetPositionCrossingIntoWriteHeadWorks()
        {
            Memory<byte> memory = _pipe.GetWriterMemory(1).Value;
            _pipe.Advance(memory.Length);
            memory = _pipe.GetWriterMemory(1).Value;
            _pipe.Advance(1);
            _pipe.Flush();

            _pipe.Read();

            memory = _pipe.GetWriterMemory(1).Value;

            ReadOnlySequence<byte> buffer = _pipe.Buffer;
            SequencePosition position = buffer.GetPosition(buffer.Length);

            _pipe.AdvanceTo(position);
            _pipe.Advance(memory.Length);
        }

        [TestMethod]
        public void CompleteReaderAfterFlushWithoutAdvancingDoesNotThrow()
        {
            _pipe.Write(new byte[10]);
            _pipe.Read();
            ReadOnlySequence<byte> buffer = _pipe.Buffer;

            _pipe.CompleteReader();
        }

        [TestMethod]
        public void AdvanceAfterCompleteThrows()
        {
            _pipe.Write(new byte[1]);
            _pipe.Read();
            ReadOnlySequence<byte> buffer = _pipe.Buffer;

            _pipe.CompleteReader();

            var exception = Assert.ThrowsException<InvalidOperationException>(() => _pipe.AdvanceTo(buffer.End));
            Assert.AreEqual("Reading is not allowed after reader was completed.", exception.Message);       // 문자열 체크 필요
        }

        [TestMethod]
        public void HelloWorldAcrossTwoBlocks()
        {
            var blockSize = _pipe.GetWriterMemory().Value.Length;

            byte[] paddingBytes = Enumerable.Repeat((byte) 'a', blockSize - 5).ToArray();
            byte[] bytes = Encoding.ASCII.GetBytes("Hello World");

            _pipe.Write(paddingBytes);
            _pipe.Write(bytes);
            _pipe.Flush();

            _pipe.Read();
            ReadOnlySequence<byte> buffer = _pipe.Buffer;
            Assert.IsFalse(buffer.IsSingleSegment);
            ReadOnlySequence<byte> helloBuffer = buffer.Slice(blockSize-5);
            Assert.IsFalse(helloBuffer.IsSingleSegment);
            var memory = new List<ReadOnlyMemory<byte>>();
            foreach (var m in helloBuffer)
            {
                memory.Add(m);
            }

            List<ReadOnlyMemory<byte>> spans = memory;
            _pipe.AdvanceTo(buffer.Start, buffer.Start);

            Assert.AreEqual(2, memory.Count);
            var helloBytes = new byte[spans[0].Length];
            spans[0].Span.CopyTo(helloBytes);
            var worldBytes = new byte[spans[1].Length];
            spans[1].Span.CopyTo(worldBytes);
            Assert.AreEqual("Hello", Encoding.ASCII.GetString(helloBytes));
            Assert.AreEqual(" World", Encoding.ASCII.GetString(worldBytes));
        }



    }
}