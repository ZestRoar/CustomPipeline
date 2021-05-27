using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Diagnostics.CodeAnalysis;

namespace CustomPipelines
{
    internal struct CustomBufferSegmentStack
    {
        private SegmentAsValueType[] array;
        private int size;
        private int maxSize;

        public CustomBufferSegmentStack(CustomPipeOptions pipeOptions)
        {
            this.array = new SegmentAsValueType[pipeOptions.InitialSegmentPoolSize];
            this.size = 0;
            this.maxSize = pipeOptions.MaxSegmentPoolSize;
        }
        
        public bool TryPop([NotNullWhen(true)] out CustomBufferSegment? result)     // 세그먼트 하나 할당 (풀에서 하나 제거)
        {
            int size = this.size - 1;
            SegmentAsValueType[] array = this.array;

            if ((uint)size >= (uint)array.Length)
            {
                result = default;
                return false;
            }

            this.size = size;
            result = array[size];
            array[size] = default;
            return true;
        }

        public bool Push(CustomBufferSegment item)          // 세그먼트 반환
        {
            if (this.size >= this.maxSize)
            {
                Trace.WriteLine("Segment Stack is Full!");
                return false;
            }

            int size = this.size;
            SegmentAsValueType[] array = this.array;

            if ((uint)size < (uint)array.Length)
            {
                array[size] = item;
                this.size = size + 1;
            }
            else
            {
                this.PushWithResize(item);
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
