using Shared.Logging;
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
            TcpConnection connection = new(12000, 30);
            Logger.Info("Starting receiver. Press Enter to exit.");
            Console.ReadLine();
        }
    }
}
