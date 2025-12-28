using Shared.Models;
using Shared.Protocol;
using System.Net;
using System.Net.Sockets;

namespace Receiver
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            UdpTransport receiver = new UdpTransport(11000);
            receiver.StartServer();
            
            receiver.OnSegmentReceived += test;

            Console.ReadLine();
            Console.WriteLine("En cours d'écoute...");
            Console.ReadLine();
            await receiver.DisposeAsync();

        }

        private static void test(byte[] PacketBinary, IPEndPoint ip)
        {
            Segment deserializedPacket = Segment.Deserialize(PacketBinary);
            Console.WriteLine(Segment.BytesToHexString(deserializedPacket.Serialize()));
        }
    }
}
