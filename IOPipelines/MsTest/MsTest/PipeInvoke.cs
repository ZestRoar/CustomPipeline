using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
//using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime.InteropServices;
using System.Text;
using PipelineAsync;
using MsPipeline;

namespace MsTest
{


    class PipeInvoke : IPipeInvoke
    {
        private IMsPipeline pipe;
        public delegate void ProcessPipe(ReadOnlySequence<byte> sequence);
        private SequencePosition? readPosition;

        public PipeInvoke()
        {
            //pipe = new PipeExtension();
        }
        //public PipeInvoke(PipeOptions optionsOfPipe)
        //{
        //    pipe = new PipeExtension(optionsOfPipe);
        //}

        public void WriteFromSocketAsync(Socket socket)
        {
            try
            {
                byte[] bytes = new byte[256];
                int bytesRead = -1;
                while (bytesRead != 0)
                {
                    //Memory<byte> memory = pipe.GetWriterMemory;

                    //bytesRead = socket.Receive(bytes, 0, socket.Available, SocketFlags.None);
                    //
                    //var receiveTask = Task.Run(() => socket.ReceiveAsync(memory, SocketFlags.None));
                    //Task.WhenAll(receiveTask);
                    //bytesRead = receiveTask.Result.Result;
                    //bytesRead = socket.ReceiveAsync(memory,SocketFlags.None).Result;
                    bytesRead = pipe.WriteAsync(socket);

                    pipe.Advance(bytesRead);

                    StateResult result = pipe.FlushAsync();

                    //if (result.IsCompleted || result.IsCanceled)
                    if (result.IsEnded)
                    {
                        break;
                    }
                }

            }
            catch (Exception)
            {
                Console.WriteLine($"[{socket.RemoteEndPoint}]: Receive Exception");

            }

            pipe.CompleteWriter();
        }

        public void ReadAndProcessAsync(ProcessPipe process)
        {
            try
            {
                while (true)
                {
                    var result = pipe.ReadAsync(out var buffer);

                    //process(PipeExtension.SliceBuffer(result, readPosition));

                    if (readPosition != null)
                    {
                        buffer = buffer.Slice(buffer.GetPosition(1, readPosition.Value), buffer.End);
                    }

                    pipe.AdvanceTo(buffer.Start, buffer.End); // 사용된 데이터 알리기
                    readPosition = buffer.PositionOf((byte)'\n');
                    if (result.IsCanceled || (result.IsCompleted && buffer.IsEmpty))
                    {
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Read Exception : {e}");
            }

            pipe.CompleteReader();
        }

        public void BuildTask(Socket socket, ProcessPipe process)
        {
            // 1. pipe로부터 소켓 -> Writer 쓰기 작업
            Task.Run(() => WriteFromSocketAsync(socket));

            // 2. Writer -> Reader 전송 작업
            // 3. Reader -> Process 읽기 및 콜백 작업
            Task.Run(() => ReadAndProcessAsync(process));
        }

        // PipeOptions Pool 항목 참고해서 메모리 풀 버퍼 관리 기능 추가

    }
}



//namespace MsTest
//{


//    class PipeInvoke : IPipeInvoke
//    {
//        private PipeExtension pipe;
//        public delegate void ProcessPipe(ReadOnlySequence<byte> sequence);
//        private SequencePosition? readPosition;

//        public PipeInvoke()
//        {
//            pipe = new PipeExtension();
//        }
//        public PipeInvoke(PipeOptions optionsOfPipe)
//        {
//            pipe = new PipeExtension(optionsOfPipe);
//        }

//        public void WriteFromSocketAsync(Socket socket)
//        {
//            try
//            {
//                byte[] bytes = new byte[256];
//                int bytesRead = -1;
//                while (bytesRead != 0)
//                {
//                    Memory<byte> memory = pipe.GetWriterMemory;

//                    bytesRead = socket.Receive(bytes, 0, socket.Available, SocketFlags.None);

//                    var receiveTask = Task.Run(() => socket.ReceiveAsync(memory, SocketFlags.None));
//                    Task.WhenAll(receiveTask);
//                    bytesRead = receiveTask.Result.Result;
//                    //bytesRead = socket.ReceiveAsync(memory,SocketFlags.None).Result;

//                    pipe.TryAdvance(bytesRead);

//                    FlushResult result = pipe.TryFlushTask();

//                    if (result.IsCompleted || result.IsCanceled)
//                    {
//                        break;
//                    }
//                }

//            }
//            catch (Exception)
//            {
//                Console.WriteLine($"[{socket.RemoteEndPoint}]: Receive Exception");

//            }

//            pipe.CompleteWriter();
//        }

//        public void ReadAndProcessAsync(ProcessPipe process)
//        {
//            try
//            {
//                while (true)
//                {
//                    var result = pipe.TryReadTask();

//                    var buffer = result.Buffer;     // 불연속적인 메모리를 다룸 (ReadOnlySequence)

//                    //process(PipeExtension.SliceBuffer(result, readPosition));

//                    if (readPosition != null)
//                    {
//                        buffer = buffer.Slice(buffer.GetPosition(1, readPosition.Value), buffer.End);
//                    }

//                    pipe.AdvanceTo(buffer.Start, buffer.End); // 사용된 데이터 알리기
//                    readPosition = buffer.PositionOf((byte)'\n');
//                    if (result.IsCanceled || (result.IsCompleted && buffer.IsEmpty))
//                    {
//                        break;
//                    }
//                }
//            }
//            catch (Exception e)
//            {
//                Console.WriteLine($"Read Exception : {e}");
//            }

//            pipe.CompleteReader();
//        }

//        public void BuildTask(Socket socket, ProcessPipe process)
//        {
//            // 1. pipe로부터 소켓 -> Writer 쓰기 작업
//            Task.Run(() => WriteFromSocketAsync(socket));

//            // 2. Writer -> Reader 전송 작업
//            // 3. Reader -> Process 읽기 및 콜백 작업
//            Task.Run(() => ReadAndProcessAsync(process));
//        }

//        // PipeOptions Pool 항목 참고해서 메모리 풀 버퍼 관리 기능 추가

//    }
//}
