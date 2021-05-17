using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Diagnostics.CodeAnalysis;

namespace CustomPipelines
{
    internal struct BufferSegmentStack
    {
        private SegmentAsValueType[] _array;
        private int _size;

        public BufferSegmentStack(int size)
        {
            _array = new SegmentAsValueType[size];
            _size = 0;
        }

        public int Count => _size;

        public bool TryPop([NotNullWhen(true)] out BufferSegment? result)
        {
            int size = _size - 1;
            SegmentAsValueType[] array = _array;

            if ((uint)size >= (uint)array.Length)
            {
                result = default;
                return false;
            }

            _size = size;
            result = array[size];
            array[size] = default;
            return true;
        }

        
        public void Push(BufferSegment item)
        {
            int size = _size;
            SegmentAsValueType[] array = _array;

            if ((uint)size < (uint)array.Length)
            {
                array[size] = item;
                _size = size + 1;
            }
            else
            {
                PushWithResize(item);
            }
        }

        // 드물게 호출하는 메서드를 인라인화하지 않음으로서 코드 퀄리티를 개선
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void PushWithResize(BufferSegment item)
        {
            Array.Resize(ref _array, 2 * _array.Length);
            _array[_size] = item;
            _size++;
        }

        /// <summary>
        /// 배열 저장 시 쓰기작업을 할 때 CLR의 공변 체크를 피하기 위해 참조 타입을 래핑한 구조체
        /// </summary>
        /// <remarks>
        /// <see cref="BufferSegmentStack"/> 객체에서 배열이 쓰기작업할 때마다 공변체크의 코스트를 줄여보니 퍼포먼스가 좋았다고 함
        /// </remarks>
        private readonly struct SegmentAsValueType
        {
            private readonly BufferSegment _value;
            private SegmentAsValueType(BufferSegment value) => _value = value;
            public static implicit operator SegmentAsValueType(BufferSegment s) => new SegmentAsValueType(s);
            public static implicit operator BufferSegment(SegmentAsValueType s) => s._value;
        }
    }
}
