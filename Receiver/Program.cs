using Shared.Models;

namespace Receiver
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Packet packet = new Packet(11000, Packet.Flags.ACK, new byte[0xFFFF]);
            Console.WriteLine("Hello, World!");
        }
    }
}
