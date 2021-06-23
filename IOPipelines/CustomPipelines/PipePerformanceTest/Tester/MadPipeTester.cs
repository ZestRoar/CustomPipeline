namespace PipePerformanceTest
{
    using System;
    using System.IO;
    using System.Buffers;
    using MadPipeline;

    class MadPipeTester
    {
        private Madline madline;
        private IMadlineWriter madWriter;
        private IMadlineReader madReader;

        private bool writeSet = false;
        private bool readSet = false;

        private int writeCount = 0;
        private int readCount = 0;

        public MadPipeTester()
        {
            var options = new MadlineOptions();
            this.madline = new Madline(options);
            this.madWriter = this.madline;
            this.madReader = this.madline;
        }

        public Memory<byte> GetWriterMemory(int bytes)
        {
            return this.madWriter.GetMemory(bytes);
        }

        public void Advance(int bytes)
        {
            if (this.madWriter.TryAdvance(bytes))
            {
                this.madWriter.Flush();
            }
            else
            {
                this.madWriter.WriteSignal().OnCompleted(() =>
                {
                    this.madWriter.Advance(bytes);
                    this.madWriter.Flush();
                });
            }

            while (this.madline.State.IsWritingPaused)
            {
            }
        }

        // 반드시 청크 형식으로 된 것만..
        public void Read(FileStream fileStream, int bytes)
        {
            if (this.madReader.TryRead(out var result, 0))
            {
                this.ProcessCopy(fileStream, in result, bytes);
            }
            else
            {
                this.madReader.DoRead().Then(
                    readResult =>
                    {
                        this.ProcessCopy(fileStream, in readResult, bytes);
                    });

            }

            while (this.madline.State.IsReadingPaused)
            {
            }
        }
        public void ProcessCopy(FileStream fileStream, in ReadOnlySequence<byte> result, int bytes)
        {
            var remains = bytes;
            foreach (var segment in result)
            {
                var length = segment.ToArray().Length;
                if (remains > length)
                {
                    remains -= length;
                    fileStream.Write(segment.ToArray(), 0, length);
                    continue;
                }
                else
                {
                    fileStream.Write(segment.ToArray(), 0, remains);
                    break;
                }
            }

            this.madReader.AdvanceTo(result.GetPosition(bytes));
        }

        public void CompleteWriter()
        {
            this.madWriter.CompleteWriter();
        }
        public void CompleteReader()
        {
            this.madReader.CompleteReader();
        }
    }
}
