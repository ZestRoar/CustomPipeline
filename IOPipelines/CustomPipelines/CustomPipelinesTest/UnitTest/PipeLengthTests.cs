using System;
using System.Threading.Tasks;
using CustomPipelines;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CustomPipelinesTest
{
    [TestClass]
    public class PipeLengthTests : IDisposable
    {
        public PipeLengthTests()
        {
            _pool = new TestMemoryPool();
            _pipe = new CustomPipe(new CustomPipeOptions(_pool,0,0));
        }

        public void Dispose()
        {
            _pipe.CompleteWriter();
            _pipe.CompleteReader();
            _pool?.Dispose();
        }

        private readonly TestMemoryPool _pool;

        private readonly CustomPipe _pipe;

        [TestMethod]
        public void ByteByByteTest()
        {
            for (var i = 1; i <= 1024 * 1024; ++i)
            {
                _pipe.GetWriterMemory(100);
                _pipe.Advance(1);
                _pipe.BlockingFlush();

                Assert.AreEqual(i, _pipe.Length);
            }

            _pipe.BlockingFlush();

            for (int i = 1024 * 1024 - 1; i >= 0; --i)
            {

                StateResult result = _pipe.BlockingRead();
                SequencePosition consumed = result.Buffer.Value.Slice(1).Start;

                Assert.AreEqual(i + 1, result.Buffer.Value.Length);

                _pipe.AdvanceTo(consumed, consumed);

                Assert.AreEqual(i, _pipe.Length);
            }
        
        }

        [TestMethod]
        public void LengthCorrectAfterAlloc0AdvanceFlush()
        {
            _pipe.GetWriterMemory(0);
            _pipe.WriteEmpty(10);
            _pipe.BlockingFlush();

            Assert.AreEqual(10, _pipe.Length);
        }
        [TestMethod]
        public void LengthCorrectAfterAllocAdvanceFlush()
        {
            _pipe.WriteEmpty(10);
            _pipe.BlockingFlush();

            Assert.AreEqual(10, _pipe.Length);
        }
        [TestMethod]
        public void LengthDecreasedAfterReadAdvanceExamined()
        {
            _pipe.GetWriterMemory(100);
            _pipe.Advance(10);
            _pipe.BlockingFlush();

            StateResult result = _pipe.BlockingRead();
            SequencePosition consumed = result.Buffer.Value.Slice(5).Start;
            _pipe.AdvanceTo(consumed, consumed);

            Assert.AreEqual(5, _pipe.Length);
        }
        [TestMethod]
        public void LengthDoesNotChangeIfExamineDoesNotChange()
        {
            _pipe.WriteEmpty(10);
            _pipe.BlockingFlush();
            StateResult result = _pipe.BlockingRead();
            _pipe.AdvanceTo(result.Buffer.Value.Start);

            Assert.AreEqual(10, _pipe.Length);
        }
        [TestMethod]
        public void LengthChangesIfExaminedChanges()
        {
            _pipe.WriteEmpty(10);
            _pipe.BlockingFlush();
            StateResult result = _pipe.BlockingRead();
            _pipe.AdvanceTo(result.Buffer.Value.Start, result.Buffer.Value.End);

            Assert.AreEqual(0, _pipe.Length);
        }
        [TestMethod]
        public void LengthIsBasedOnPreviouslyExamined()
        {
            for (int i = 0; i < 5; ++i)
            {
                _pipe.WriteEmpty(10);
                _pipe.BlockingFlush();
                StateResult result = _pipe.BlockingRead();
                _pipe.AdvanceTo(result.Buffer.Value.Start, result.Buffer.Value.End);

                Assert.AreEqual(0, _pipe.Length);
            }
        }
        [TestMethod]
        public void PooledSegmentsDontAffectLastExaminedSegment()
        {
            _pipe.WriteEmpty(_pool.MaxBufferSize);
            _pipe.WriteEmpty(_pool.MaxBufferSize);
            _pipe.BlockingFlush();

            StateResult result = _pipe.BlockingRead();

            SequencePosition position = result.Buffer.Value.Slice(result.Buffer.Value.Start, _pool.MaxBufferSize).End;

            _pipe.AdvanceTo(position);

            Assert.AreEqual(4096, _pipe.Length);

            _pipe.WriteEmpty(_pool.MaxBufferSize);
            _pipe.BlockingFlush();

            result = _pipe.BlockingRead();
            _pipe.AdvanceTo(result.Buffer.Value.Start);

            Assert.AreEqual(8192, _pipe.Length);

            result = _pipe.BlockingRead();
            _pipe.AdvanceTo(result.Buffer.Value.End);

            Assert.AreEqual(0, _pipe.Length);
        }
        [TestMethod]
        public void PooledSegmentsDontAffectLastExaminedSegmentEmptyGapWithDifferentBlocks()
        {
            _pipe.WriteEmpty(_pool.MaxBufferSize);
            _pipe.WriteEmpty(_pool.MaxBufferSize);
            _pipe.BlockingFlush();

            StateResult result = _pipe.BlockingRead();

            SequencePosition endOfFirstBlock = result.Buffer.Value.Slice(result.Buffer.Value.Start, _pool.MaxBufferSize).End;
            SequencePosition startOfSecondBlock = result.Buffer.Value.GetPosition(_pool.MaxBufferSize);

            Assert.AreNotSame(endOfFirstBlock.GetObject(), startOfSecondBlock.GetObject());

            _pipe.AdvanceTo(startOfSecondBlock, endOfFirstBlock);

            Assert.AreEqual(4096, _pipe.Length);

            _pipe.WriteEmpty(_pool.MaxBufferSize);
            _pipe.BlockingFlush();

            result = _pipe.BlockingRead();
            _pipe.AdvanceTo(result.Buffer.Value.Start);

            Assert.AreEqual(8192, _pipe.Length);

            result = _pipe.BlockingRead();
            _pipe.AdvanceTo(result.Buffer.Value.End);

            Assert.AreEqual(0, _pipe.Length);

        }
        [TestMethod]
        public void ExaminedAtSecondLastBlockWorks()
        {
            _pipe.WriteEmpty(_pool.MaxBufferSize);
            _pipe.WriteEmpty(_pool.MaxBufferSize);
            _pipe.WriteEmpty(_pool.MaxBufferSize);
            _pipe.BlockingFlush();

            StateResult result = _pipe.BlockingRead();

            SequencePosition position = result.Buffer.Value.Slice(result.Buffer.Value.Start, _pool.MaxBufferSize).End;

            _pipe.AdvanceTo(position, result.Buffer.Value.GetPosition(_pool.MaxBufferSize*2));

            Assert.AreEqual(4096, _pipe.Length);

            _pipe.WriteEmpty(_pool.MaxBufferSize);
            _pipe.BlockingFlush();

            result = _pipe.BlockingRead();
            _pipe.AdvanceTo(result.Buffer.Value.End);

            Assert.AreEqual(0, _pipe.Length);
        }
        [TestMethod]
        public void ExaminedLessThanBeforeThrows()
        {
            _pipe.WriteEmpty(10);
            _pipe.BlockingFlush();

            StateResult result = _pipe.BlockingRead();
            _pipe.AdvanceTo(result.Buffer.Value.Start, result.Buffer.Value.End);

            Assert.AreEqual(0, _pipe.Length);

            _pipe.WriteEmpty(10);
            _pipe.BlockingFlush();

            result = _pipe.BlockingRead();
            Assert.ThrowsException<InvalidOperationException>(() =>
                _pipe.AdvanceTo(result.Buffer.Value.Start, result.Buffer.Value.Start));
        }
        [TestMethod]
        public void ConsumedGreatherThanExaminedThrows()
        {
            _pipe.WriteEmpty(10);
            _pipe.BlockingFlush();

            StateResult result = _pipe.BlockingRead();
            Assert.ThrowsException<InvalidOperationException>(() =>
                _pipe.AdvanceTo(result.Buffer.Value.End, result.Buffer.Value.Start));
        }
        [TestMethod]
        public void NullConsumedOrExaminedNoops()
        {
            _pipe.WriteEmpty(10);
            _pipe.BlockingFlush();

            StateResult result = _pipe.BlockingRead();
            _pipe.AdvanceTo(default, result.Buffer.Value.End);
        }
        [TestMethod]
        public void NullExaminedNoops()
        {
            _pipe.WriteEmpty(10);
            _pipe.BlockingFlush();

            StateResult result = _pipe.BlockingRead();
            _pipe.AdvanceTo(result.Buffer.Value.Start, default);
        }
        [TestMethod]
        public void NullExaminedAndConsumedNoops()
        {
            _pipe.WriteEmpty(10);
            _pipe.BlockingFlush();

            StateResult result = _pipe.BlockingRead();
            _pipe.AdvanceTo(default, default);
        }

    }
}