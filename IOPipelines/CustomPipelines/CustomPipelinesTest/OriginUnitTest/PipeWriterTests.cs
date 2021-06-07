using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CustomPipelines;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CustomPipelinesTest
{
    [TestClass]
    public class PipeWriterTests : PipeTest
    {

        public PipeWriterTests() : base(0, 0)
        {
        }

        private byte[] Read()
        {
            Pipe.Flush();
            Pipe.Read();
            var data = Pipe.Buffer.ToArray();
            Pipe.AdvanceToEnd();
            return data;
        }

        [TestMethod]
        [DataRow(3,-1,0)]
        [DataRow(3,0,-1)]
        [DataRow(3,0,4)]
        [DataRow(3,4,0)]
        [DataRow(3,-1,-1)]
        [DataRow(3,4,4)]
        public void ThrowsForInvalidParameters(int arrayLength, int offset, int length)
        {
            var array = new byte[arrayLength];
            for (var i = 0; i < array.Length; i++)
            {
                array[i] = (byte)(i + 1);
            }

            Pipe.Write(new Span<byte>(array, 0, 0));
            Pipe.Write(new Span<byte>(array, array.Length, 0));

            try
            {
                Pipe.Write(new Span<byte>(array, offset, length));
                Assert.IsTrue(false);
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex is ArgumentOutOfRangeException);
            }

            Pipe.Write(new Span<byte>(array, 0, array.Length));
            CollectionAssert.AreEqual(array, Read());
        }

        [TestMethod]
        [DataRow(0, 3)]
        [DataRow(1, 1)]
        [DataRow(1, 2)]
        [DataRow(2, 1)]

        public void CanWriteWithOffsetAndLength(int offset, int length)
        {
            var array = new byte[] { 1, 2, 3 };

            Pipe.Write(new Span<byte>(array, offset, length));

            CollectionAssert.AreEqual(array.Skip(offset).Take(length).ToArray(), Read());
        }

        [TestMethod]
        public void CanWriteIntoHeadlessBuffer()
        {
            Pipe.Write(new byte[] { 1, 2, 3 });
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, Read());
        }

        [TestMethod]
        public void CanWriteMultipleTimes()
        {
            Pipe.Write(new byte[] { 1 });
            Pipe.Write(new byte[] { 2 });
            Pipe.Write(new byte[] { 3 });

            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, Read());
        }

        [TestMethod]
        public async Task CanWriteOverTheBlockLength()
        {
            Memory<byte> memory = Pipe.GetWriterMemory().Value;
           
            IEnumerable<byte> source = Enumerable.Range(0, memory.Length).Select(i => (byte)i);
            byte[] expectedBytes = source.Concat(source).Concat(source).ToArray();

            Pipe.Write(expectedBytes);

            CollectionAssert.AreEqual(expectedBytes, Read());
        }

        [TestMethod]
        public void EnsureAllocatesSpan()
        {
            var span = Pipe.GetWriterMemory(10).Value.Span;

            Assert.IsTrue(span.Length >= 10);
            // 0 byte Flush would not complete the reader so we complete.
            Pipe.CompleteWriter();
            CollectionAssert.AreEqual(new byte[] { }, Read());
        }

        [TestMethod]
        public void SlicesSpanAndAdvancesAfterWrite()
        {
            int initialLength = Pipe.GetWriterMemory(3).Value.Span.Length;

            Pipe.Write(new byte[] { 1, 2, 3 });
            Span<byte> span = Pipe.GetWriterMemory().Value.Span;

            Assert.AreEqual(initialLength - 3, span.Length);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, Read());
        }

        [TestMethod]
        [DataRow(5)]
        [DataRow(50)]
        [DataRow(500)]
        [DataRow(5000)]
        [DataRow(50000)]

        public void WriteLargeDataBinary(int length)
        {
            var data = new byte[length];
            new Random(length).NextBytes(data);
            Pipe.Write(data);
            Pipe.Read();
            ReadOnlySequence<byte>? input = Pipe.Buffer;
            CollectionAssert.AreEqual(data, Pipe.Buffer.ToArray());
            Pipe.AdvanceToEnd();
        }

        [TestMethod]
        public void CanWriteNothingToBuffer()
        {
            Pipe.GetWriterMemory(0);
            Pipe.Advance(0); // doing nothing, the hard way
            Pipe.Flush();
        }

        [TestMethod]
        public void EmptyWriteDoesNotThrow()
        {
            Pipe.Write(new byte[0]);
        }

        [TestMethod]
        public void ThrowsOnAdvanceOverMemorySize()
        {
            Memory<byte> buffer = Pipe.GetWriterMemory(1).Value;
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => Pipe.Advance(buffer.Length + 1));
        }

        [TestMethod]
        public void ThrowsOnAdvanceWithNoMemory()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => Pipe.Advance(1));
        }



        [TestMethod]
        public void WritesUsingGetSpanWorks()
        {
            var bytes = Encoding.ASCII.GetBytes("abcdefghijklmnopqrstuvwzyz");
            var pipe = new TestCustomPipe( new CustomPipeOptions(minimumSegmentSize: 1));
            
            for (int i = 0; i < bytes.Length; i++)
            {
                Pipe.GetWriterMemory().Value.Span[0] = bytes[i];
                Pipe.Advance(1);
            }

            Pipe.Flush(); 
            Pipe.CompleteWriter();
            Pipe.Read();
            CollectionAssert.AreEqual(bytes, Pipe.Buffer.ToArray());
            pipe.AdvanceToEnd();

            pipe.CompleteReader();
        }

        [TestMethod]
        public void WritesUsingGetMemoryWorks()
        {
            var bytes = Encoding.ASCII.GetBytes("abcdefghijklmnopqrstuvwzyz");
            var pipe = new TestCustomPipe( new CustomPipeOptions(minimumSegmentSize: 1));
           
            for (int i = 0; i < bytes.Length; i++)
            {
                Pipe.GetWriterMemory().Value.Span[0] = bytes[i];
                Pipe.Advance(1);
            }

            Pipe.Flush(); 
            Pipe.CompleteWriter();
            Pipe.Read();
            CollectionAssert.AreEqual(bytes, Pipe.Buffer.ToArray());
            pipe.AdvanceToEnd();

            pipe.CompleteReader();
        }

        // ======================================= 아래는 pool 검사 모드 제작 완료 시 진행 =================================================

        //[TestMethod]
        //public void WriteAsyncWithACompletedReaderNoops()
        //{
        //    var pool = new DisposeTrackingBufferPool();
        //    var pipe = new TestCustomPipe(pool, new CustomPipeOptions());
        //    pipe.CompleteReader();

        //    byte[] writeBuffer = new byte[100];
        //    for (var i = 0; i < 10000; i++)
        //    {
        //        pipe.Write(writeBuffer);
        //    }

        //    Assert.AreEqual(0, pool.CurrentlyRentedBlocks);
        //}

        //[TestMethod]
        //public void GetMemoryFlushWithACompletedReaderNoops()
        //{
        //    var pool = new DisposeTrackingBufferPool();
        //    var pipe = new TestCustomPipe(pool, new CustomPipeOptions());
        //    pipe.CompleteReader();

        //    for (var i = 0; i < 10000; i++)
        //    {
        //        var mem = pipe.GetWriterMemory();
        //        pipe.Advance(mem.Value.Length);
        //        pipe.Flush();
        //    }

        //    Assert.AreEqual(1, pool.CurrentlyRentedBlocks);
        //    pipe.CompleteWriter();
        //    Assert.AreEqual(0, pool.CurrentlyRentedBlocks);
        //}

        // ======================================= 쓰레드 사용 시 체크 =================================================


        //[TestMethod]
        //public void CompleteWithLargeWriteThrows()              // 아직 미완성
        //{
        //    var pipe = new CustomPipe();
        //    pipe.CompleteReader();

        //    int iSleep = 0;
        //    while (iSleep < 1000000)
        //    {
        //        ++iSleep;
        //    }
        //    pipe.CompleteWriter();

        //    try
        //    {
        //        for (int i = 0; i < 1000; i++)
        //        {
        //            var buffer = new byte[10000000];
        //            Pipe.Write(buffer);
        //        }
        //    }
        //    catch (InvalidOperationException)
        //    {
        //        // Complete while writing
        //    }

        //    //await task;
        //    pipe.CompleteWriter();

        //    Assert.AreEqual(999, 1);

        //}
    }
}