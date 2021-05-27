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
                void WriteProcess()
                {
                    byte[] bytes = new byte[256];
                    int bytesRead = -1;
                    while (bytesRead != 0)
                    {
                        bytesRead = socket.Receive(bytes);

                        if (bytesRead == 0)
                        {
                            break;
                        }

                        pipe.Write(bytes, 0, bytesRead);

                        pipe.Advance(bytesRead);

                    }
                    

                }

                while (true)
                {
                    WriteProcess();

                    var buffer = pipe.Buffer;

                    readPosition = buffer.PositionOf((byte)'\n');
                    pipe.AdvanceTo(readPosition.Value);             // 사용된 데이터 알리기

                    readPosition = buffer.PositionOf((byte)'\t');
                    if(readPosition!=null)
                    {
                        break;
                    }

                }

                pipe.CompleteReader();

                
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
