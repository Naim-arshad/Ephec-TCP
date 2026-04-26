using Shared.Models;
using Shared.Protocol.TCP;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Transmitter
{
    internal class Program
    {
        private const string DefaultTarget = "127.0.0.1:11000";

        static async Task Main(string[] args)
        {
            TcpConnection tcpOverlayTransport = new(12000, 60);
            string target = DefaultTarget;

            while (true)
            {
                ShowMenu(target);
                string? choice = Console.ReadLine()?.Trim().ToLowerInvariant();

                switch (choice)
                {
                    case "1":
                        target = ReadTargetEndpoint(target);
                        break;

                    case "2":
                        Console.WriteLine($"[INFO] Envoi d'un SYN vers {target}...");
                        await tcpOverlayTransport.InitiatConnection(target);
                        break;

                    case "3":
                        await SendCustomPacketAsync(target);
                        break;

                    case "q":
                        Console.WriteLine("Fermeture du transmitter.");
                        return;

                    default:
                        Console.WriteLine("Choix invalide. Merci de sélectionner une option du menu.");
                        break;
                }

                    Console.WriteLine();
            }
        }

        private static void ShowMenu(string target)
        {
            Console.WriteLine("=============================================");
            Console.WriteLine("     Transmitter - Menu de test TCP overlay ");
            Console.WriteLine("=============================================");
            Console.WriteLine($"Target actuel : {target}");
            Console.WriteLine("1) Changer la target (IP:PORT)");
            Console.WriteLine("2) Initier la connexion (SYN)");
            Console.WriteLine("3) Envoyer un paquet custom (flags/seq/data)");
            Console.WriteLine("q) Quitter");
            Console.Write("Votre choix: ");
        }

        private static string ReadTargetEndpoint(string currentValue)
        {
            Console.Write($"Nouvelle target IP:PORT (vide pour garder {currentValue}) : ");
            string? input = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(input))
                return currentValue;

            if (!IPEndPoint.TryParse(input, out _))
            {
                Console.WriteLine("Format invalide. Exemple attendu : 127.0.0.1:11000");
                return currentValue;
            }

            return input;
        }

        private static async Task SendCustomPacketAsync(string target)
        {
            if (!IPEndPoint.TryParse(target, out IPEndPoint? endpoint) || endpoint is null)
            {
                Console.WriteLine("Target invalide, impossible d'envoyer le paquet custom.");
                return;
            }

            SegmentFlags flags = PromptFlags();
            ushort sequence = PromptUShort("Numéro de séquence (0-65535)");

            Console.Write("Payload texte (laisser vide pour aucun payload): ");
            string payload = Console.ReadLine() ?? string.Empty;
            byte[] data = Encoding.UTF8.GetBytes(payload);

            Segment segment = new(sequence, flags, data);
            byte[] packet = segment.Serialize();

            using UdpClient client = new();
            int sent = await client.SendAsync(packet, endpoint);

            Console.WriteLine($"Paquet envoyé vers {endpoint} ({sent} octets). Flags={FlagsToDisplay(flags)}, Seq={sequence}, DataLen={data.Length}");
        }

        private static SegmentFlags PromptFlags()
        {
            while (true)
            {
                Console.WriteLine("Flags disponibles: SYN, ACK, FIN, RST");
                Console.Write("Flags (séparés par virgules, ex: SYN,ACK | vide = NONE): ");
                string raw = (Console.ReadLine() ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(raw))
                    return new SegmentFlags(SegmentFlags.FlagOptions.None);

                string[] parts = raw
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                SegmentFlags.FlagOptions options = SegmentFlags.FlagOptions.None;
                bool parseError = false;

                foreach (string part in parts)
                {
                    if (!Enum.TryParse(part, true, out SegmentFlags.FlagOptions parsed))
                    {
                        Console.WriteLine($"Flag inconnu : {part}");
                        parseError = true;
                        break;
                    }

                    options |= parsed;
                }

                if (parseError)
                    continue;

                try
                {
                    return new SegmentFlags(options);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Combinaison de flags invalide: {ex.Message}");
                }
            }
        }

        private static ushort PromptUShort(string label)
        {
            while (true)
            {
                Console.Write($"{label}: ");
                string? input = Console.ReadLine()?.Trim();

                if (ushort.TryParse(input, out ushort value))
                    return value;

                Console.WriteLine("Valeur invalide. Entrez un entier entre 0 et 65535.");
            }
        }

        private static string FlagsToDisplay(SegmentFlags flags)
        {
            if (flags.Value == 0)
                return SegmentFlags.FlagOptions.None.ToString();

            List<string> values = [];
            foreach (SegmentFlags.FlagOptions flag in Enum.GetValues<SegmentFlags.FlagOptions>())
            {
                if (flag == SegmentFlags.FlagOptions.None)
                    continue;

                if (SegmentFlags.IsFlagSet(flags.Value, flag))
                    values.Add(flag.ToString());
            }


            return string.Join('|', values);
        }
    }
}
