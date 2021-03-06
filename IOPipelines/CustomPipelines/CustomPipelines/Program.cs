using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CustomPipelines
{
    class Program
    {
        private CustomPipe _pipe;
        private Pipe originPipe;

        void RunProgram()
        {
            _pipe = new CustomPipe(new CustomPipeOptions(0, 0, 100));
            originPipe = new Pipe(new PipeOptions(default, null, null, 0, 0, 100));

            ByteByByteTestOriginSync();
            ByteByByteTestZ();
            ByteByByteTestOrigin();

            return;
        }

        public async Task ByteByByteTestOrigin()
        {
            for (var i = 1; i <= 1024 * 1024; i++)
            {
                originPipe.Writer.GetMemory(100);
                originPipe.Writer.Advance(1);
                await originPipe.Writer.FlushAsync();

                //Assert.AreEqual(i, originPipe.Length);
            }

            await originPipe.Writer.FlushAsync();
            //var result = originPipe.Reader.ReadAsync().Result;

            for (int i = 1024 * 1024 - 1; i >= 0; i--)
            {
                var result = await originPipe.Reader.ReadAsync();
                var consumed = result.Buffer.Slice(1).Start;
                //var consumed = result.Buffer.Start;

                //Assert.AreEqual(i + 1, result.Buffer.Length);

                originPipe.Reader.AdvanceTo(consumed, consumed);

                //Assert.AreEqual(i, originPipe.Length);
            }
        }
        public void ByteByByteTestOriginSync()
        {
            for (var i = 1; i <= 1024 * 1024; i++)
            {
                originPipe.Writer.GetMemory(100);
                originPipe.Writer.Advance(1);
                originPipe.Writer.FlushAsync();

                //Assert.AreEqual(i, originPipe.Length);
            }

            originPipe.Writer.FlushAsync();
            //var result = originPipe.Reader.ReadAsync().Result;

            for (int i = 1024 * 1024 - 1; i >= 0; i--)
            {
                var result = originPipe.Reader.ReadAsync().Result;
                var consumed = result.Buffer.Slice(1).Start;
                //var consumed = result.Buffer.Start;

                //Assert.AreEqual(i + 1, result.Buffer.Length);

                originPipe.Reader.AdvanceTo(consumed, consumed);

                //Assert.AreEqual(i, originPipe.Length);
            }
        }


        public void ByteByByteTestZ()
        {
            for (var i = 1; i <= 1024 * 1024; ++i)
            {
                _pipe.GetWriterMemory(100);
                _pipe.Advance(1);
                //Assert.AreEqual(i, _pipe.Length);
            }


            for (int i = 1024 * 1024 - 1; i >= 0; --i)
            {
                //_pipe.ReadAsync();
                _pipe.TryRead(out var result, 1);
                var consumed = result.Buffer.Value.Slice(1).Start;
                //var consumed = _pipe.Buffer.Slice(1).Start;
                //var consumed = _pipe.Buffer.Start;

                //Assert.AreEqual(i + 1, _pipe.Buffer.Length);

                _pipe.AdvanceTo(consumed);

                //Assert.AreEqual(i, _pipe.Length);
            }

        }
        static void Main(string[] args)
        {
            CoupleTest test = new();

            test.CoupleThreadTest();

            return;

            Program prog = new Program();


            prog.RunProgram();

            return;


        }

        private static void ProcessLine(ReadOnlySequence<byte> buffer)
        {
            foreach (var segment in buffer)
            {
                Console.Write(Encoding.UTF8.GetString(segment.Span));
            }


        }

        private static readonly Action writeAction = WriteCallbackMethod;
        private static readonly Action readAction = ReadCallbackMethod;

        static void WriteCallbackMethod()
        {
            Console.WriteLine("callback : write");
        }

        static void ReadCallbackMethod()
        {
            Console.WriteLine("callback : read");
        }

    }
}
