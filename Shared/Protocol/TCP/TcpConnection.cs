using Shared.Models;
using System;
using System.Collections.Concurrent;
using System.Net;

namespace Shared.Protocol.TCP
{

    public class TcpConnection
    {
        private readonly ConcurrentDictionary<string, ConnectionInfo> _mapConnection;
        private readonly UdpTransport _udpTransport;

        public TcpConnection(int port)
        {
            _mapConnection = new ConcurrentDictionary<string, ConnectionInfo>();
            _udpTransport = new UdpTransport(port);
            
            _udpTransport.OnSegmentReceived += OnSegmentReceived;
            _udpTransport.StartServer();
        }

        public void OnSegmentReceived(byte[] PacketBinary, IPEndPoint ip)
        {
            Console.Write($"{ip.ToString()}: ");
            Segment segment = Segment.Deserialize(PacketBinary);
            byte[] byteToSend;

            switch (segment.Flags.Value)
            {
                case (byte)SegmentFlags.FlagOptions.Syn:
                    byteToSend = TcpFlagHandler.HandleSYN(segment, ip, _mapConnection).Serialize();
                    _ = _udpTransport.SendSegmentAsync(byteToSend, ip); 
                    break;
                case ((byte)SegmentFlags.FlagOptions.Syn | (byte)SegmentFlags.FlagOptions.Ack):
                    Console.WriteLine("TODO: Gérer le SYN-ACK");
                    break;
                // Ajouter d'autres cas pour gérer différents types de segments
                case (byte)SegmentFlags.FlagOptions.Ack:
                    Console.WriteLine("TODO: Gérer l'ACK");
                    break;
                default:
                    throw new Exception("Type de segment non géré");
            }
        }

        public async Task SendSyn(IPEndPoint ip)
        {
            //TODO: implémenter sendSyn (ci-dessous est un code basique pour tester)
            Segment packet = new(RandomSequenceNumber(0), new SegmentFlags(SegmentFlags.FlagOptions.Syn), new byte[0x02]);
            byte[] PacketBinary = packet.Serialize();

            _ = _udpTransport.SendSegmentAsync(
                PacketBinary,
                ip
            );
        }

        public static ushort RandomSequenceNumber(ushort excludeNumber)
        {
            Random rand = new();
            ushort number = (ushort)rand.Next(0, ushort.MaxValue);
            if (number != excludeNumber && (number + 2) <= ushort.MaxValue)
                number += 2;
            else
                number = 0;
            return number;
        }
    }
}
