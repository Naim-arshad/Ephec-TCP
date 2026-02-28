using System.Net;
using Shared.Protocol.TCP;

namespace Transmitter
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length < 3) return;
            IPEndPoint target = new IPEndPoint(IPAddress.Parse(args[0]), int.Parse(args[1]));
            string filePath = args[2];

            TcpConnection transport = new TcpConnection(12000);
            await transport.SendFileAsync(target, filePath);

            Console.WriteLine("Transfert terminé.");
        }
    }
}