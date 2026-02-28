using System;
using System.Net;
using System.Threading.Tasks;
using Shared.Protocol.TCP;

namespace Receiver
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            // --- CONFIGURATION DYNAMIQUE ---

            // Récupère le port depuis les arguments (ex: dotnet run -- 11000). 
            // Si absent, utilise 11000 par défaut.
            int port = args.Length > 0 ? int.Parse(args[0]) : 11000;

            // Récupère le nom du fichier de sortie. 
            // Si absent, utilise "Fichier_Recu_Final.txt" par défaut.
            string output = args.Length > 1 ? args[1] : "Fichier_Recu_Final.txt";

            // --- INITIALISATION DU MOTEUR TCP ---

            // Crée la connexion TCP sur le port spécifié.
            TcpConnection transport = new TcpConnection(port);

            // Configure le nom du fichier cible pour toutes les connexions entrantes.
            // IPAddress.Any permet d'accepter des données de n'importe quelle source.
            transport.SetTargetFileName(new IPEndPoint(IPAddress.Any, 0), output);

            // --- AFFICHAGE CONSOLE ---
            Console.WriteLine("===========================================");
            Console.WriteLine($"   SERVEUR RÉCEPTEUR PROTOCOLE TCP/UDP    ");
            Console.WriteLine("===========================================");
            Console.WriteLine($"[*] Port d'écoute dynamique : {port}");
            Console.WriteLine($"[*] Fichier de sortie       : {output}");
            Console.WriteLine("[*] État : En attente de connexion...");
            Console.WriteLine("-------------------------------------------");

            // Maintient le programme en vie indéfiniment pour écouter les paquets.
            await Task.Delay(-1);
        }
    }
}