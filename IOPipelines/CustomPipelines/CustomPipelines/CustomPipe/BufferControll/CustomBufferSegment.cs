using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace CustomPipelines
{
    internal sealed class CustomBufferSegment : ReadOnlySequenceSegment<byte>
    {
        private IMemoryOwner<byte>? memoryOwner;
        private byte[]? array;
        private CustomBufferSegment? next;
        private int end;

        /// <summary>
        /// 사용 중인 바이트 범위의 끝 지점을 나타낸다. (활성화 범위의 끝)
        /// 메모리를 빌리는 순간엔 End == Start
        /// Start는 0부터 버퍼 길이 사이의 값이며 End보다 크면 안됨
        /// </summary>
        public int End
        {
            get => this.end;
            set
            {
                Debug.Assert(value <= AvailableMemory.Length);

                this.end = value;
                Memory = AvailableMemory.Slice(0, value);
            }
        }

        /// <summary>
        /// 사용 중인 메모리 전반이 여러 블록에 걸쳐있을 경우 블록을 이어주는 프로퍼티, 빌리는 순간에는 next == null
        /// Start, End, Next 요소들은 불연속적인 메모리의 연결 리스트를 생성하는데에 쓰인다.
        /// 메모리 블록 안에서의 시작과 끝 지점의 변화, 그리고 다음 메모리 블록을 풀에서 할당하거나 블록을 해제하는 경우에 활성 메모리의 증감이 일어난다.
        /// </summary>
        public CustomBufferSegment? NextSegment
        {
            get => this.next;
            set
            {
                Next = value;
                this.next = value;
            }
        }

        public void SetOwnedMemory(IMemoryOwner<byte> memoryOwner)
        {
            this.memoryOwner = memoryOwner;
            AvailableMemory = memoryOwner.Memory;
        }

        public void SetOwnedMemory(byte[] arrayPoolBuffer)
        {
            this.array = arrayPoolBuffer;
            AvailableMemory = arrayPoolBuffer;
        }

        public void ResetMemory()
        {
            IMemoryOwner<byte>? memoryOwner = this.memoryOwner;
            if (memoryOwner != null)
            {
                this.memoryOwner = null;
                memoryOwner.Dispose();
            }
            else
            {
                Debug.Assert(this.array != null);
                ArrayPool<byte>.Shared.Return(this.array);
                this.array = null;
            }

            Next = null;
            RunningIndex = 0;
            Memory = default;
            this.next = null;
            this.end = 0;
            AvailableMemory = default;
        }

        internal object? MemoryOwner => (object?)this.memoryOwner ?? this.array;

        public Memory<byte> AvailableMemory { get; private set; }

        public int Length => End;

        public int WritableBytes
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => AvailableMemory.Length - End;
        }

        public void SetNext(CustomBufferSegment segment)
        {
            Debug.Assert(segment != null);
            Debug.Assert(Next == null);

            NextSegment = segment;

            segment = this;

            while (segment.Next != null)
            {
                Debug.Assert(segment.NextSegment != null);
                segment.NextSegment.RunningIndex = segment.RunningIndex + segment.Length;
                segment = segment.NextSegment;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static long GetLength(CustomBufferSegment startSegment, int startIndex, CustomBufferSegment endSegment, int endIndex)
        {
            return (endSegment.RunningIndex + (uint)endIndex) - (startSegment.RunningIndex + (uint)startIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static long GetLength(long startPosition, CustomBufferSegment endSegment, int endIndex)
        {
            return (endSegment.RunningIndex + (uint)endIndex) - startPosition;
        }
    }
}
