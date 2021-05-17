using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MsTest
{
    public class CustomPipeOptions
    {
        private const int DefaultMinimumSegmentSize = 4096;
        private const int DefaultPauseWriterThreshold = 65536;

        public static CustomPipeOptions Default { get; } = new CustomPipeOptions();

        public CustomPipeOptions(System.Buffers.MemoryPool<byte>? pool = null,
            CustomPipeScheduler? readerScheduler = null, CustomPipeScheduler? writerScheduler = null,
            long pauseWriterThreshold = (long)DefaultPauseWriterThreshold, long resumeWriterThreshold = (long) (DefaultPauseWriterThreshold>>1),
            int minimumSegmentSize = DefaultMinimumSegmentSize, bool useSynchronizationContext = true)
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
            ReaderScheduler = readerScheduler ?? new CustomPipeScheduler(true);
            WriterScheduler = writerScheduler ?? new CustomPipeScheduler(true);
            PauseWriterThreshold = pauseWriterThreshold;
            ResumeWriterThreshold = resumeWriterThreshold;
            UseSynchronizationContext = useSynchronizationContext;
        }

        /// <summary>Gets a value that determines if asynchronous callbacks and continuations should be executed on the <see cref="System.Threading.SynchronizationContext" /> they were captured on. This takes precedence over the schedulers specified in <see cref="System.IO.Pipelines.PipeOptions.ReaderScheduler" /> and <see cref="System.IO.Pipelines.PipeOptions.WriterScheduler" />.</summary>
        /// <value><see langword="true" /> if asynchronous callbacks and continuations should be executed on the <see cref="System.Threading.SynchronizationContext" /> they were captured on; otherwise, <see langword="false" />.</value>
        public bool UseSynchronizationContext { get; }

        /// <summary>Gets the number of bytes in the <see cref="System.IO.Pipelines.Pipe" /> when <see cref="System.IO.Pipelines.PipeWriter.FlushAsync(System.Threading.CancellationToken)" /> starts blocking.</summary>
        /// <value>The number of bytes in the <see cref="System.IO.Pipelines.Pipe" /> when <see cref="System.IO.Pipelines.PipeWriter.FlushAsync(System.Threading.CancellationToken)" /> starts blocking.</value>
        public long PauseWriterThreshold { get; } 

        /// <summary>Gets the number of bytes in the <see cref="System.IO.Pipelines.Pipe" /> when <see cref="System.IO.Pipelines.PipeWriter.FlushAsync(System.Threading.CancellationToken)" /> stops blocking.</summary>
        /// <value>The number of bytes in the <see cref="System.IO.Pipelines.Pipe" /> when <see cref="System.IO.Pipelines.PipeWriter.FlushAsync(System.Threading.CancellationToken)" /> stops blocking.</value>
        public long ResumeWriterThreshold { get; }

        /// <summary>Gets the minimum size of the segment requested from the <see cref="System.IO.Pipelines.PipeOptions.Pool" />.</summary>
        /// <value>The minimum size of the segment requested from the <see cref="System.IO.Pipelines.PipeOptions.Pool" />.</value>
        public int MinimumSegmentSize { get; }

        /// <summary>Gets the <see cref="System.IO.Pipelines.PipeScheduler" /> used to execute <see cref="System.IO.Pipelines.PipeWriter" /> callbacks and async continuations.</summary>
        /// <value>A <see cref="System.IO.Pipelines.PipeScheduler" /> object used to execute <see cref="System.IO.Pipelines.PipeWriter" /> callbacks and async continuations.</value>
        public CustomPipeScheduler WriterScheduler { get; }

        /// <summary>Gets the <see cref="System.IO.Pipelines.PipeScheduler" /> used to execute <see cref="System.IO.Pipelines.PipeReader" /> callbacks and async continuations.</summary>
        /// <value>A <see cref="System.IO.Pipelines.PipeScheduler" /> that is used to execute <see cref="System.IO.Pipelines.PipeReader" /> callbacks and async continuations.</value>
        public CustomPipeScheduler ReaderScheduler { get; }

        /// <summary>Gets the <see cref="System.Buffers.MemoryPool{T}" /> object used for buffer management.</summary>
        /// <value>A pool of memory blocks used for buffer management.</value>
        public MemoryPool<byte> Pool { get; }

        /// <summary>
        /// Returns true if Pool is <see cref="MemoryPool{Byte}"/>.Shared
        /// </summary>
        internal bool IsDefaultSharedMemoryPool { get; }

        /// <summary>
        /// The initialize size of the segment pool
        /// </summary>
        internal int InitialSegmentPoolSize { get; } = 4;

        /// <summary>
        /// The maximum number of segments to pool
        /// </summary>
        internal int MaxSegmentPoolSize { get; } = 256;
    }
}
