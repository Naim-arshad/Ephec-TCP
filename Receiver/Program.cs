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
            Segment packet = new Segment(0xAAAA, Segment.PacketFlags.FIN, new byte[0x02]);
            byte[] PacketBinary = packet.Serialize();
            Segment packet2 = new Segment(0xFFFF, Segment.PacketFlags.ACK, new byte[0x01]);
            byte[] PacketBinary2 = packet2.Serialize();
            Console.WriteLine(Segment.BytesToHexString(PacketBinary));
            Segment deserializedPacket = Segment.Deserialize(PacketBinary);
            Console.WriteLine(Segment.BytesToHexString(deserializedPacket.Serialize()));

            UdpTransport receiver = new UdpTransport(11000);
            receiver.StartServer();

            receiver.OnSegmentReceived += test;
            

            Console.ReadLine();
            UdpClient udp = new UdpClient();
            
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
