using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Buffers;
using System.IO.Pipelines;
using PipelineAsync;
using MsPipeline;

namespace MsTest
{
    class Program
    {
        static void Main(string[] args)
        {
            var listenSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress ipAddress = ipHostInfo.AddressList[0];
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 11000);

            listenSocket.Bind(localEndPoint);
            listenSocket.Listen(120);

            Console.WriteLine("SingleIO Listening on port 11000");
            while (true)
            {
                var socket = listenSocket.Accept();
                
                Console.WriteLine($"[{socket.RemoteEndPoint}]: connected");

                PipeInvoke pipe = new PipeInvoke();

                // 비동기 프로세스 진행
                pipe.BuildTask(socket, ProcessLine);

            }








            // ====== 실험 중 ======

            //var listenSocketEcho = new Socket(SocketType.Stream, ProtocolType.Tcp);
            //IPEndPoint localEndPointEcho = new IPEndPoint(ipAddress, 11001);
            //listenSocketEcho.Bind(localEndPointEcho);
            //listenSocketEcho.Listen(120);

            //PipePool pool = new PipePool(4);
            //ProcessSingleIO(listenSocket, pool);
            //pool.CollectPipe(); // 종료된 파이프를 준비시키는데 사용

            //Task single = ProcessSingleIO(listenSocket, pool);
            //Task echo = ProcessEcho(listenSocketEcho, pool);
            //await Task.WhenAll(single, echo);

            //await ProcessEcho(listenSocketEcho, pool);

        }

        private static async Task ProcessSingleIO(Socket listenSocket, PipePool pool)
        {
            Console.WriteLine("SingleIO Listening on port 11000");
            while (true)
            {
                var socket = await listenSocket.AcceptAsync();

                Console.WriteLine($"[{socket.RemoteEndPoint}]: connected");

                IPipeAsync pipe = pool.CreatePipe();
                //_ = pipe.RunProcess(socket, 1, ProcessLine);

                // 종료 조건에 따라 파이프를 전부 해제할 때까지 기다리고 break
                // 
            }
        }

        private static async Task ProcessEcho(Socket listenSocket, PipePool pool)
        {
            Console.WriteLine("Echo Listening on port 11001");
            while (true)
            {
                var socket = await listenSocket.AcceptAsync();

                Console.WriteLine($"[{socket.RemoteEndPoint}]: connected");

                IPipeAsync pipe = pool.CreatePipe();
                //_ = pipe.RunSending(socket, 1, null);     // 미완성

                // 종료 조건에 따라 파이프를 전부 해제할 때까지 기다리고 break
                // 
            }
        }

        private static void ProcessLine(ReadOnlySequence<byte> buffer)
        {
            foreach (var segment in buffer)
            {
                Console.Write(Encoding.UTF8.GetString(segment.Span));
            }


        }
    }
}
