﻿using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using CustomPipelines;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CustomPipelinesTest
{
    [TestClass]
    public class BufferSegmentPoolTests : IDisposable
    {
        private readonly TestMemoryPool _pool;
        private readonly CustomPipe _pipe;

        public BufferSegmentPoolTests()
        {
            _pool = new TestMemoryPool();
            _pipe = new CustomPipe(new CustomPipeOptions(_pool, pauseWriterThreshold: 0, resumeWriterThreshold: 0));
        }

        public void Dispose()
        {
            _pipe.CompleteWriter();
            _pipe.CompleteReader();
            _pool?.Dispose();
        }
        [TestMethod]
        public void BufferSegmentsAreReused()
        {
            _pipe.WriteEmpty(_pool.MaxBufferSize);

            while (_pipe.FlushAsync()) { }
            while (_pipe.ReadAsync()) { }
            StateResult result = _pipe.ReadResult();

            //object oldSegment = result.Buffer.End.GetObject();
            object oldSegment = _pipe.GetObject();

            // 첫번째 세그먼트를 반환해야 함
            //_pipe.Reader.AdvanceTo(result.Buffer.End);
            _pipe.AdvanceToEnd();

            // 1 블록 남음
            Assert.AreEqual(0, _pipe.Length);

            // 반환된 세그먼트가 사용되야 함
            _pipe.WriteEmpty(_pool.MaxBufferSize);

            while (_pipe.FlushAsync()) { }
            while (_pipe.ReadAsync()) { }
            result = _pipe.ReadResult();

            //object newSegment = result.Buffer.End.GetObject();
            object newSegment = _pipe.GetObject();
            //_pipe.AdvanceTo(result.Buffer.End);
            _pipe.AdvanceToEnd();

            Assert.AreSame(oldSegment, newSegment);

            Assert.AreEqual(0, _pipe.Length);
        }
        [TestMethod]
        public void BufferSegmentsPooledUpToThreshold()
        {
            int blockCount = CustomPipe.MaxSegmentPoolSize + 1;

            // Write 256 blocks to ensure they get reused
            for (int i = 0; i < blockCount; i++)
            {
                _pipe.WriteEmpty(_pool.MaxBufferSize);
            }

            while (_pipe.FlushAsync()) { }
            while (_pipe.ReadAsync()) { }
            StateResult result = _pipe.ReadResult();

            List<ReadOnlySequenceSegment<byte>> oldSegments = GetSegments(result);

            Assert.AreEqual(blockCount, oldSegments.Count);

            // This should return them all to the segment pool (256 blocks, the last block will be discarded)
            //_pipe.AdvanceTo(result.Buffer.End);
            _pipe.AdvanceToEnd();
            for (int i = 0; i < blockCount; i++)
            {
                _pipe.WriteEmpty(_pool.MaxBufferSize);
            }

            while (_pipe.FlushAsync()) { }
            while (_pipe.ReadAsync()) { }
            result = _pipe.ReadResult();

            List<ReadOnlySequenceSegment<byte>> newSegments = GetSegments(result);

            Assert.AreEqual(blockCount, newSegments.Count);

            //_pipe.AdvanceTo(result.Buffer.End);
            _pipe.AdvanceToEnd();

            // Assert Pipe.MaxSegmentPoolSize pooled segments
            for (int i = 0; i < CustomPipe.MaxSegmentPoolSize; i++)
            {
                Assert.AreSame(oldSegments[i], newSegments[CustomPipe.MaxSegmentPoolSize - i - 1]);
            }

            // The last segment shouldn't exist in the new list of segments at all (it should be new)
            CollectionAssert.DoesNotContain(newSegments, oldSegments[256]);
        }

        private static List<ReadOnlySequenceSegment<byte>> GetSegments(StateResult result)
        {
            SequenceMarshal.TryGetReadOnlySequenceSegment(
                           result.Buffer ?? throw new ArgumentNullException(),    
                           out ReadOnlySequenceSegment<byte> start,
                           out int startIndex,
                           out ReadOnlySequenceSegment<byte> end,
                           out int endIndex);

            var segments = new List<ReadOnlySequenceSegment<byte>>();

            while (start != end.Next)
            {
                segments.Add(start);
                start = start.Next;
            }

            return segments;
        }
    }
}