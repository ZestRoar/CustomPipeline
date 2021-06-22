using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace CustomPipelines
{
    internal struct CustomBufferSegmentStack
    {
        private SegmentAsValueType[] array;
        private int size;
        private readonly int maxSize;

        public CustomBufferSegmentStack(CustomPipeOptions pipeOptions)
        {
            this.array = new SegmentAsValueType[pipeOptions.InitialSegmentPoolSize];
            this.size = 0;
            this.maxSize = pipeOptions.MaxSegmentPoolSize;
        }
        
        public CustomBufferSegment TryPop()     // 세그먼트 하나 할당 (풀에서 하나 제거)
        {
            var stackSize = this.size - 1;
            var arraySegment = this.array;

            if ((uint)stackSize >= (uint)arraySegment.Length)
            {
                return new CustomBufferSegment();
            }

            this.size = stackSize;
            var result = arraySegment[stackSize];
            arraySegment[stackSize] = default;

            if ((CustomBufferSegment)result == null)
            {
                return new CustomBufferSegment();
            }
            return result;
        }

        public bool Push(CustomBufferSegment item)          // 세그먼트 반환
        {
            if (this.size >= this.maxSize)
            {
                //Trace.WriteLine("Segment Stack is Full!");
                return false;
            }

            var stackSize = this.size;
            var arraySegment = this.array;

            if ((uint)stackSize < (uint)arraySegment.Length)
            {
                arraySegment[stackSize] = item;
                this.size = stackSize + 1;
            }
            else
            {
                this.PushWithResize(item);
            }

            try
            {
                if ((CustomBufferSegment)this.array[stackSize] == null)
                {
                    int i = 0;
                }
            }
            catch (Exception ex)
            {
                int p = 0;
            }

            return true;
        }

        // 드물게 호출되므로 Push의 코드 크기를 줄이기 위해 인라인을 방지
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void PushWithResize(CustomBufferSegment item)   
        {
            Array.Resize(ref this.array, 2 * this.array.Length);
            this.array[this.size] = item;
            ++this.size;
        }

        /// <summary>
        /// 배열 저장 시 쓰기작업을 할 때 CLR의 공변 체크를 피하기 위해 참조 타입을 래핑한 구조체
        /// </summary>
        /// <remarks>
        /// <see cref="CustomBufferSegmentStack"/> 객체에서 배열이 쓰기작업할 때마다 공변체크의 코스트를 줄여 퍼포먼스를 증가
        /// </remarks>
        private readonly struct SegmentAsValueType
        {
            private readonly CustomBufferSegment value;
            private SegmentAsValueType(CustomBufferSegment value) => this.value = value;
            public static implicit operator SegmentAsValueType(CustomBufferSegment segment) => new SegmentAsValueType(segment);
            public static implicit operator CustomBufferSegment(SegmentAsValueType segment) => segment.value;
        }
    }
}
