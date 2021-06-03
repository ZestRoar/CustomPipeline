using System;
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
        private const int MaxBufferSize = 4096;
        private readonly TestCustomPipe _pipe;

        public BufferSegmentPoolTests()
        {
            _pipe = new TestCustomPipe(new CustomPipeOptions(pauseWriterThreshold: 0, resumeWriterThreshold: 0));
        }

        public void Dispose()
        {
            _pipe.CompleteWriter();
            _pipe.CompleteReader();
        }
        [TestMethod]
        public void BufferSegmentsAreReused()
        {
            _pipe.WriteEmpty(MaxBufferSize);
            _pipe.Flush();
            _pipe.Read();

            var oldSegment = _pipe.Buffer.End.GetObject();

            // 첫번째 세그먼트를 반환해야 함
            _pipe.AdvanceToEnd();

            // 읽기 대기 중인 버퍼가 존재하지 않아야 함
            Assert.AreEqual(0, _pipe.Length);

            // 반환된 세그먼트가 사용되야 함
            _pipe.WriteEmpty(MaxBufferSize);
            _pipe.Flush();
            _pipe.Read();

            var newSegment = _pipe.Buffer.End.GetObject();
            _pipe.AdvanceToEnd();

            // 동일한 세그먼트를 사용
            Assert.AreSame(oldSegment, newSegment);

            // 읽기 대기 중인 버퍼가 존재하지 않아야 함
            Assert.AreEqual(0, _pipe.Length);
        }
        [TestMethod]
        public void BufferSegmentsPooledUpToThreshold()
        {
            int blockCount = CustomPipe.MaxSegmentPoolSize + 1;

            // TryWrite 256 blocks to ensure they get reused
            for (int i = 0; i < blockCount; i++)
            {
                _pipe.WriteEmpty(MaxBufferSize);
            }
            _pipe.Flush();
            _pipe.Read();

            List<ReadOnlySequenceSegment<byte>> oldSegments = GetSegments(_pipe.Buffer);

            Assert.AreEqual(blockCount, oldSegments.Count);

            // 모든 세그먼트를 풀에 반환하기, Pool 사이즈가 256이므로 257번째는 사라질거임
            _pipe.AdvanceToEnd();
            for (int i = 0; i < blockCount; i++)
            {
                _pipe.WriteEmpty(MaxBufferSize);
            }

            _pipe.Flush();
            _pipe.Read();

            List<ReadOnlySequenceSegment<byte>> newSegments = GetSegments(_pipe.Buffer);

            Assert.AreEqual(blockCount, newSegments.Count);

             _pipe.AdvanceToEnd();

            // 풀이 스택 모양이므로 넣었다 뺐을 때 순서가 거꾸로 동일한 세그먼트
            for (int i = 0; i < CustomPipe.MaxSegmentPoolSize; i++)
            {
                Assert.AreSame(oldSegments[i], newSegments[CustomPipe.MaxSegmentPoolSize - i - 1]);
            }

            // 257번째 세그먼트가 존재하면 안됨
            CollectionAssert.DoesNotContain(newSegments, oldSegments[256]);
        }

        
        private static List<ReadOnlySequenceSegment<byte>> GetSegments(ReadOnlySequence<byte>? result)
        {
            SequenceMarshal.TryGetReadOnlySequenceSegment(
                           result ?? throw new ArgumentNullException(),
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