using Shared.Models;
using Shared.Protocol.TCP;
using System.Net;
using System.Net.Sockets;
using static System.Net.Mime.MediaTypeNames;

namespace Transmitter
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            

            TcpConnection tcpOverlayTransport = new TcpConnection(12000, 60);

            string? key = "";
            while (key != "q")
            {
                Console.WriteLine("Sending packet...");
                await tcpOverlayTransport.InitiatConnection("127.0.0.1:11000");
                key = Console.ReadLine();
            }

            
        }
    }
}
