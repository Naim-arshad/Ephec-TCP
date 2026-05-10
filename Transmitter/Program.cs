using Shared.Logging;
using Shared.Protocol;
using Shared.Protocol.TCP;
namespace Transmitter
{
    internal class Program
    {
        private const string LogSource = "app.transmitter";

        static async Task Main(string[] args)
        {
            TcpConnection connection = new(11000, 30);
            Logger.Info(LogSource, "Starting transmitter. localPort=11000, remote=127.0.0.1:12000");
            await connection.InitiatConnection("127.0.0.1:12000");
            await connection.SendFile("127.0.0.1:12000", "./test.txt");
            Logger.Info(LogSource, "Transmitter is idle. Press Enter to exit.");
            Console.ReadLine();
        }
    }
}
