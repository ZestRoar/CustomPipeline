using System;
using System.Buffers;

namespace CustomPipelines
{
    internal class CustomPipeOptions
    {
        private const int DefaultMinimumSegmentSize = 4096;
        private const int DefaultPauseWriterThreshold = 65536;

        public static CustomPipeOptions Default { get; } = new CustomPipeOptions();

        public CustomPipeOptions(System.Buffers.MemoryPool<byte>? pool = null,
            long pauseWriterThreshold = (long)DefaultPauseWriterThreshold,
            long resumeWriterThreshold = (long)(DefaultPauseWriterThreshold >> 1),
            int minimumSegmentSize = DefaultMinimumSegmentSize)
        {
            if (pauseWriterThreshold < 0)
            {
                throw new ArgumentOutOfRangeException(pauseWriterThreshold.ToString());
            }

            if (resumeWriterThreshold < 0 || resumeWriterThreshold > pauseWriterThreshold)
            {
                throw new ArgumentOutOfRangeException(resumeWriterThreshold.ToString());
            }

            Pool = pool ?? MemoryPool<byte>.Shared;
            IsDefaultSharedMemoryPool = Pool == MemoryPool<byte>.Shared;
            PauseWriterThreshold = pauseWriterThreshold;
            ResumeWriterThreshold = resumeWriterThreshold;
        }

        /// <summary>Flush 쓰기 작업 중단 바이트</summary>
        public long PauseWriterThreshold { get; } = DefaultPauseWriterThreshold;

        /// <summary>Flush 쓰기 작업 재개 바이트</summary>
        public long ResumeWriterThreshold { get; }

        /// <summary>요청받은 세그먼트 최소 사이즈</summary>
        public int MinimumSegmentSize { get; } = DefaultMinimumSegmentSize;

        /// <summary>버퍼 관리를 위한 메모리풀을 참조</summary>
        public MemoryPool<byte> Pool { get; }

        /// <summary>
        /// 메모리풀 공유상태 체크
        /// </summary>
        internal bool IsDefaultSharedMemoryPool { get; }

        /// <summary>
        /// 메모리풀 초기 사이즈
        /// </summary>
        internal int InitialSegmentPoolSize { get; } = 4;

        /// <summary>
        /// 메모리풀 세그먼트 최대 수
        /// </summary>
        internal int MaxSegmentPoolSize { get; } = 256;
    }
}
