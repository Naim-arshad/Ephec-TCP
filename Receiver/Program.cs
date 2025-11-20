using Shared.Models;

namespace Receiver
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Packet packet = new Packet(0xAAAA, Packet.PacketFlags.FIN, new byte[0xF]);
            Console.WriteLine(Packet.BytesToHexString(packet.serialisation()));
        }
    }
}
