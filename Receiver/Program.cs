using Shared.Logging;
using Shared.Protocol.TCP;

namespace Receiver
{
    internal class Program
    {
        private const string LogSource = "app.receiver";

        static async Task Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: Receiver <port> <output_file>");
                return;
            }

            int port = int.Parse(args[0]);
            string outputFile = args[1];

            TcpConnection connection = new(port, 30, outputFile);
            Logger.Info(LogSource, $"Receiver listening on port {port}. Output: {outputFile}");

            Logger.Info(LogSource, "Press Enter to exit.");
            Console.ReadLine();
        }
    }
}