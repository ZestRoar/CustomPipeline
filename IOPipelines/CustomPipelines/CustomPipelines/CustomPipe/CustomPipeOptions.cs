using System;
using System.Buffers;

namespace CustomPipelines
{
    internal class CustomPipeOptions
    {
        private const int DefaultMinimumSegmentSize = 4096;
        private const int DefaultPauseWriterThreshold = 65536;

        public static CustomPipeOptions Default { get; } = new CustomPipeOptions();

        public CustomPipeOptions(long pauseWriterThreshold = (long)DefaultPauseWriterThreshold,
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

            PauseWriterThreshold = pauseWriterThreshold;
            ResumeWriterThreshold = resumeWriterThreshold;
            MinimumSegmentSize = minimumSegmentSize;
        }

        public bool CheckPauseWriter(long oldBytes, long currentBytes)
            => oldBytes < PauseWriterThreshold && currentBytes >= PauseWriterThreshold;
        public bool CheckResumeWriter(long oldBytes, long currentBytes)
            => oldBytes >= ResumeWriterThreshold && currentBytes < ResumeWriterThreshold;

        /// <summary>Flush 쓰기 작업 중단 바이트</summary>
        public long PauseWriterThreshold { get; } 

        /// <summary>Flush 쓰기 작업 재개 바이트</summary>
        public long ResumeWriterThreshold { get; }

        /// <summary>요청받은 세그먼트 최소 사이즈</summary>
        public int MinimumSegmentSize { get; } 

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
