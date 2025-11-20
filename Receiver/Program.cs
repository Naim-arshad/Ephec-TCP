using Shared.Models;

namespace Receiver
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Packet packet = new Packet(0xAAAA, Packet.PacketFlags.FIN, new byte[0x02]);
            byte[] PacketBinary = packet.Serialize();
            Console.WriteLine(Packet.BytesToHexString(PacketBinary));
            Packet deserializedPacket = Packet.Deserialize(PacketBinary);
            Console.WriteLine(Packet.BytesToHexString(deserializedPacket.Serialize()));

        }
    }
}
