using System;
using System.IO.Pipelines;
using System.Threading.Tasks;
using CustomPipelines;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CustomPipelinesTest
{
    [TestClass]
    public class PipeLengthTests : IDisposable
    {
        private const int MaxBufferSize = 4096;
        public PipeLengthTests()
        {
            _pipe = new TestCustomPipe(new CustomPipeOptions(0,0, 100));
            originPipe = new Pipe(new PipeOptions(default,null,null,0,0, 100));
        }

        public void Dispose()
        {
            _pipe.CompleteWriter();
            _pipe.CompleteReader();
        }


        private readonly TestCustomPipe _pipe;
        private readonly Pipe originPipe;

        [TestMethod]
        public void CombinedByteByByteTest()
        {
            ByteByByteTestOriginSync();
            ByteByByteTestZ();
            ByteByByteTestOrigin();
            int i = 0;
        }

        [TestMethod]
        public async Task ByteByByteTestOrigin()
        {
            for (var i = 1; i <= 1024 * 1024; i++)
            {
                originPipe.Writer.GetMemory(100);
                originPipe.Writer.Advance(1);
                await originPipe.Writer.FlushAsync();

                //Assert.AreEqual(i, originPipe.Length);
            }
            
            await originPipe.Writer.FlushAsync();

            for (int i = 1024 * 1024 - 1; i >= 0; i--)
            {
                var result = await originPipe.Reader.ReadAsync();
                var consumed = result.Buffer.Slice(1).Start;

                //Assert.AreEqual(i + 1, result.Buffer.Length);

                originPipe.Reader.AdvanceTo(consumed, consumed);

                //Assert.AreEqual(i, originPipe.Length);
            }
        }
        [TestMethod]
        public void ByteByByteTestOriginSync()
        {
            for (var i = 1; i <= 1024 * 1024; i++)
            {
                originPipe.Writer.GetMemory(100);
                originPipe.Writer.Advance(1);
                originPipe.Writer.FlushAsync();

                //Assert.AreEqual(i, originPipe.Length);
            }
            
            originPipe.Writer.FlushAsync();

            for (int i = 1024 * 1024 - 1; i >= 0; i--)
            {
                var result = originPipe.Reader.ReadAsync().Result;
                var consumed = result.Buffer.Slice(1).Start;

                //Assert.AreEqual(i + 1, result.Buffer.Length);

                originPipe.Reader.AdvanceTo(consumed, consumed);

                //Assert.AreEqual(i, originPipe.Length);
            }
        }


        [TestMethod]
        public void ByteByByteTestZ()                       
        {
            for (var i = 1; i <= 1024 * 1024; ++i)
            {
                _pipe.GetWriterMemory(100);
                _pipe.Advance(1);
                _pipe.FlushAsync();

                //Assert.AreEqual(i, _pipe.Length);
            }
            
            _pipe.FlushAsync();
            
            for (int i = 1024 * 1024 - 1; i >= 0; --i)
            {
                _pipe.TryRead(out var result, 1);
                var consumed = result.Buffer.Value.Slice(1).Start;

                //Assert.AreEqual(i + 1, _pipe.Buffer.Length);

                _pipe.AdvanceTo(consumed, consumed);

                //Assert.AreEqual(i, _pipe.Length);
            }

        }

        [TestMethod]
        public void LengthCorrectAfterAlloc0AdvanceFlush()
        {
            _pipe.GetWriterMemory(0);
            _pipe.WriteEmpty(10);
            _pipe.FlushAsync();

            Assert.AreEqual(10, _pipe.Length);
        }
        [TestMethod]
        public void LengthCorrectAfterAllocAdvanceFlush()
        {
            _pipe.WriteEmpty(10);
            _pipe.FlushAsync();

            Assert.AreEqual(10, _pipe.Length);
        }
        [TestMethod]
        public void LengthDecreasedAfterReadAdvanceExamined()
        {
            _pipe.GetWriterMemory(100);
            _pipe.Advance(10);
            _pipe.FlushAsync();

            _pipe.Read();
            SequencePosition consumed = _pipe.Buffer.Slice(5).Start;
            _pipe.AdvanceTo(consumed, consumed);

            Assert.AreEqual(5, _pipe.Length);
        }
        [TestMethod]
        public void LengthDoesNotChangeIfExamineDoesNotChange()
        {
            _pipe.WriteEmpty(10);
            _pipe.FlushAsync();
            _pipe.Read();
            _pipe.AdvanceTo(_pipe.Buffer.Start);

            Assert.AreEqual(10, _pipe.Length);
        }
        [TestMethod]
        public void LengthChangesIfExaminedChanges()
        {
            _pipe.WriteEmpty(10);
            _pipe.FlushAsync();
            _pipe.Read();
            _pipe.AdvanceTo(_pipe.Buffer.Start, _pipe.Buffer.End);

            Assert.AreEqual(0, _pipe.Length);
        }
        [TestMethod]
        public void LengthIsBasedOnPreviouslyExamined()
        {
            for (int i = 0; i < 5; ++i)
            {
                _pipe.WriteEmpty(10);
                _pipe.FlushAsync();
                _pipe.Read();
                _pipe.AdvanceTo(_pipe.Buffer.Start, _pipe.Buffer.End);

                Assert.AreEqual(0, _pipe.Length);
            }
        }
        [TestMethod]
        public void PooledSegmentsDontAffectLastExaminedSegment()
        {
            _pipe.WriteEmpty(MaxBufferSize);
            _pipe.WriteEmpty(MaxBufferSize);
            _pipe.FlushAsync();

            _pipe.Read();

            SequencePosition position = _pipe.Buffer.Slice(_pipe.Buffer.Start, MaxBufferSize).End;

            _pipe.AdvanceTo(position);

            Assert.AreEqual(4096, _pipe.Length);

            _pipe.WriteEmpty(MaxBufferSize);
            _pipe.FlushAsync();

            _pipe.Read();
            _pipe.AdvanceTo(_pipe.Buffer.Start);

            Assert.AreEqual(8192, _pipe.Length);

            _pipe.Read();
            _pipe.AdvanceTo(_pipe.Buffer.End);

            Assert.AreEqual(0, _pipe.Length);
        }
        [TestMethod]
        public void PooledSegmentsDontAffectLastExaminedSegmentEmptyGapWithDifferentBlocks()
        {
            _pipe.WriteEmpty(MaxBufferSize);
            _pipe.WriteEmpty(MaxBufferSize);
            _pipe.FlushAsync();

            _pipe.Read();

            SequencePosition endOfFirstBlock = _pipe.Buffer.Slice(_pipe.Buffer.Start, MaxBufferSize).End;
            SequencePosition startOfSecondBlock = _pipe.Buffer.GetPosition(MaxBufferSize);

            Assert.AreNotSame(endOfFirstBlock.GetObject(), startOfSecondBlock.GetObject());

            _pipe.AdvanceTo(startOfSecondBlock, endOfFirstBlock);

            Assert.AreEqual(4096, _pipe.Length);

            _pipe.WriteEmpty(MaxBufferSize);
            _pipe.FlushAsync();

            _pipe.Read();
            _pipe.AdvanceTo(_pipe.Buffer.Start);

            Assert.AreEqual(8192, _pipe.Length);

            _pipe.Read();
            _pipe.AdvanceTo(_pipe.Buffer.End);

            Assert.AreEqual(0, _pipe.Length);

        }
        [TestMethod]
        public void ExaminedAtSecondLastBlockWorks()
        {
            _pipe.WriteEmpty(MaxBufferSize);
            _pipe.WriteEmpty(MaxBufferSize);
            _pipe.WriteEmpty(MaxBufferSize);
            _pipe.FlushAsync();

            _pipe.Read();

            SequencePosition position = _pipe.Buffer.Slice(_pipe.Buffer.Start, MaxBufferSize).End;

            _pipe.AdvanceTo(position, _pipe.Buffer.GetPosition(MaxBufferSize*2));

            Assert.AreEqual(4096, _pipe.Length);

            _pipe.WriteEmpty(MaxBufferSize);
            _pipe.FlushAsync();

            _pipe.Read();
            _pipe.AdvanceTo(_pipe.Buffer.End);

            Assert.AreEqual(0, _pipe.Length);
        }
        [TestMethod]
        public void ExaminedLessThanBeforeThrows()
        {
            _pipe.WriteEmpty(10);
            _pipe.FlushAsync();

            _pipe.Read();
            _pipe.AdvanceTo(_pipe.Buffer.Start, _pipe.Buffer.End);

            Assert.AreEqual(0, _pipe.Length);

            _pipe.WriteEmpty(10);
            _pipe.FlushAsync();

            _pipe.Read();
            Assert.ThrowsException<InvalidOperationException>(() =>
                _pipe.AdvanceTo(_pipe.Buffer.Start, _pipe.Buffer.Start));
        }
        [TestMethod]
        public void ConsumedGreaterThanExaminedThrows()
        {
            _pipe.WriteEmpty(10);
            _pipe.FlushAsync();

            _pipe.Read();
            Assert.ThrowsException<InvalidOperationException>(() =>
                _pipe.AdvanceTo(_pipe.Buffer.End, _pipe.Buffer.Start));
        }
        [TestMethod]
        public void NullConsumedOrExaminedNoops()
        {
            _pipe.WriteEmpty(10);
            _pipe.FlushAsync();

            _pipe.Read();
            _pipe.AdvanceTo(default, _pipe.Buffer.End);
        }
        [TestMethod]
        public void NullExaminedNoops()
        {
            _pipe.WriteEmpty(10);
            _pipe.FlushAsync();

            _pipe.Read();
            _pipe.AdvanceTo(_pipe.Buffer.Start, default);
        }
        [TestMethod]
        public void NullExaminedAndConsumedNoops()
        {
            _pipe.WriteEmpty(10);
            _pipe.FlushAsync();

            _pipe.Read();
            _pipe.AdvanceTo(default, default);
        }

    }
}