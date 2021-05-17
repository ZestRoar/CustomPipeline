using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SocketTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var clientSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);

            Console.WriteLine("Connecting to port 8087");
            IPHostEntry ipHostInfo = Dns.GetHostEntry("DOGGYFOOT.corp.madngine.com");
            IPAddress ipAddress = ipHostInfo.AddressList[0];

            clientSocket.Connect(new IPEndPoint(ipAddress, 11000));
            var stream = new NetworkStream(clientSocket);

            await Console.OpenStandardInput().CopyToAsync(stream);
        }
    }
}
