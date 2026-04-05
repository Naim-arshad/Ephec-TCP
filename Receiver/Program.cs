using Shared.Models;
using Shared.Protocol.TCP;
using System.Net;
using System.Net.Sockets;
using Shared.Logging;

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
            TcpConnection tcpOverlayTransport = new(11000, 60);

            

            Console.ReadLine();
            Logger.Info("En cours d'écoute...");
            Console.ReadLine();
            Logger.Info("Fermeture...");
        }
    }
}
