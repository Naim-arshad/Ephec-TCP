using Shared.Models;
using Shared.Protocol.TCP;
using System.Net;
using System.Net.Sockets;

namespace Receiver
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            /*
            UdpTransport receiver = new UdpTransport(11000);
            receiver.StartServer();
            
            receiver.OnSegmentReceived += test;

            Console.ReadLine();
            Console.WriteLine("En cours d'écoute...");
            Console.ReadLine();
            await receiver.DisposeAsync();
            */
            TcpConnection tcpOverlayTransport = new TcpConnection(11000);

            

            Console.ReadLine();
            Console.WriteLine("En cours d'écoute...");
            Console.ReadLine();
            Console.WriteLine("Fermeture...");
        }
    }
}
