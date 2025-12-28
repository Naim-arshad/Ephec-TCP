using Shared.Models;
using Shared.Protocol;
using System.Net.Sockets;
using static System.Net.Mime.MediaTypeNames;

namespace Transmitter
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Segment packet = new Segment(0xAAAA, Segment.PacketFlags.FIN, new byte[0x02]);
            byte[] PacketBinary = packet.Serialize();

            UdpTransport udp = new UdpTransport(12000);
            string? key = "";
            while (key != "q")
            {
                Console.WriteLine("Sending packet...");
                await udp.SendSegmentAsync(PacketBinary, new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 11000));
                key = Console.ReadLine();
            }
        }
    }
}
