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
            _pipe = new CustomPipe(new CustomPipeOptions(_pool));
        }
        public void Dispose()
        {
            _pipe.CompleteWriter();
            _pipe.CompleteReader();
            _pool?.Dispose();
        }

        private readonly CustomPipe _pipe;

        private readonly TestMemoryPool _pool;
        [TestMethod]
        public void CanReadAndWrite()
        {
            byte[] bytes = Encoding.ASCII.GetBytes("Hello World");

            _pipe.BlockingWrite(bytes);
            StateResult result = _pipe.BlockingRead();
            ReadOnlySequence<byte> buffer = result.Buffer.Value;

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
            _pipe.BlockingFlush();

            StateResult readResult = _pipe.BlockingRead();
            _pipe.AdvanceTo(readResult.Buffer.Value.End);

            _pipe.ReadAsync();
            Assert.IsFalse(_pipe.ReadResult().IsCompleted);

            _pipe.Write(new byte[1]);
            _pipe.BlockingFlush();

            Assert.IsTrue(_pipe.ReadResult().IsCompleted);

            readResult = _pipe.BlockingRead();
            _pipe.AdvanceTo(readResult.Buffer.Value.End);

            _pipe.ReadAsync();
            Assert.IsFalse(_pipe.ReadResult().IsCompleted);

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
            Memory<byte> memory = _pipe.GetWriterMemory(1);
            _pipe.Advance(memory.Length);
            memory = _pipe.GetWriterMemory(1);
            _pipe.Advance(1);
            _pipe.BlockingFlush();

            StateResult readResult = _pipe.BlockingRead();

            memory = _pipe.GetWriterMemory(1);

            ReadOnlySequence<byte> buffer = readResult.Buffer.Value;
            SequencePosition position = buffer.GetPosition(buffer.Length);

            _pipe.AdvanceTo(position);
            _pipe.Advance(memory.Length);
        }

        [TestMethod]
        public void CompleteReaderAfterFlushWithoutAdvancingDoesNotThrow()
        {
            _pipe.BlockingWrite(new byte[10]);
            StateResult result = _pipe.BlockingRead();
            ReadOnlySequence<byte> buffer = result.Buffer.Value;

            _pipe.CompleteReader();
        }

        [TestMethod]
        public void AdvanceAfterCompleteThrows()
        {
            _pipe.BlockingWrite(new byte[1]);
            StateResult result = _pipe.BlockingRead();
            ReadOnlySequence<byte> buffer = result.Buffer.Value;

            _pipe.CompleteReader();

            var exception = Assert.ThrowsException<InvalidOperationException>(() => _pipe.AdvanceTo(buffer.End));
            Assert.AreEqual("Reading is not allowed after reader was completed.", exception.Message);       // 문자열 체크 필요
        }

        [TestMethod]
        public void HelloWorldAcrossTwoBlocks()
        {
            var blockSize = _pipe.GetWriterMemory().Length;

            byte[] paddingBytes = Enumerable.Repeat((byte) 'a', blockSize - 5).ToArray();
            byte[] bytes = Encoding.ASCII.GetBytes("Hello World");

            _pipe.Write(paddingBytes);
            _pipe.Write(bytes);
            _pipe.BlockingFlush();

            StateResult result = _pipe.BlockingRead();
            ReadOnlySequence<byte> buffer = result.Buffer.Value;
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