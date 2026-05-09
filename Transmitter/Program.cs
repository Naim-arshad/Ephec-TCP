using Shared.Logging;
using Shared.Protocol;
using Shared.Protocol.TCP;
namespace Transmitter
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            TcpConnection connection = new(11000, 30);
            Logger.Info("Starting transmitter. Press Enter to exit.");
            _ = connection.InitiatConnection("127.0.0.1:12000");
            Console.ReadLine();
        }
    }
}
