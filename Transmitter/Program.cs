using Shared.Logging;
using Shared.Protocol.TCP;

namespace Transmitter
{
    internal class Program
    {
        private const string LogSource = "app.transmitter";

        static async Task Main(string[] args)
        {
            // On vérifie qu'on a bien reçu les 3 arguments (IP, Port, Fichier)
            if (args.Length < 3)
            {
                Console.Error.WriteLine("Usage: Transmitter <ip> <port> <file>");
                return;
            }

            string ip = args[0];
            string port = args[1];
            string filePath = args[2]; // C'est ici qu'on récupère "fichier.txt"
            string remoteAddress = $"{ip}:{port}";

            TcpConnection connection = new(11000, 30);
            Logger.Info(LogSource, $"Starting transmitter. localPort=11000, target={remoteAddress}");

            // Phase 1 : Ouverture de la connexion
            await connection.InitiatConnection(remoteAddress);

            // Phase 2 : Envoi du fichier
            Logger.Info(LogSource, $"Sending file: {filePath}");
            await connection.SendFile(remoteAddress, filePath);

            // Phase 3 : Fermeture
            await connection.StopConnection(remoteAddress);

            Logger.Info(LogSource, "Transmission complete. Press Enter to exit.");
            Console.ReadLine();
        }
    }
}