using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace CustomPipelines
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

                CustomPipe pipe = new CustomPipe();
                pipe.RegisterWriteCallback(writeAction);
                pipe.RegisterReadCallback(readAction);

                SequencePosition? readPosition = null;

                // 프로세스 진행
                byte[] bytes = new byte[256];
                int bytesRead = -1;
                while (bytesRead != 0)
                {
                    bytesRead = pipe.BlockingWrite(socket);

                    pipe.Advance(bytesRead);

                    StateResult result = pipe.FlushResult();

                    if (result.IsCompleted || result.IsCanceled)
                    {
                        break;
                    }
                }

                while (true)
                {
                    var result = pipe.BlockingRead();
                    var buffer = result.Buffer.Value;


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
