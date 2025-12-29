using Shared.Models;
using Shared.Protocol;
using System.Net.Sockets;
using static System.Net.Mime.MediaTypeNames;

namespace Transmitter
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            

            TcpOverlayTransport tcpOverlayTransport = new TcpOverlayTransport(12000);

            string? key = "";
            while (key != "q")
            {
                Console.WriteLine("Sending packet...");
                await tcpOverlayTransport.sendSyn(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 11000));
                key = Console.ReadLine();
            }

            
        }
    }
}
