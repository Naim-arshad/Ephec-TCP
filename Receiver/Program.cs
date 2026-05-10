using Shared.Logging;
using Shared.Models;
using Shared.Protocol.TCP;
using System.Net;
using System.Net.Sockets;

namespace Receiver
{
    internal class Program
    {
        private const string LogSource = "app.receiver";

        static async Task Main(string[] args)
        {
            TcpConnection connection = new(12000, 30);
            Logger.Info(LogSource, "Starting receiver. localPort=12000, outputFile=received_file.bin");
            Logger.Info(LogSource, "Receiver is listening. Press Enter to exit.");
            
        }
    }
}
