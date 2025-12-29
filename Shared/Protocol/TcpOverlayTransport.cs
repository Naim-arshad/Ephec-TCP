using Shared.Models;
using System;
using System.Collections.Generic;
using System.Net;

namespace Shared.Protocol
{
    public class TcpOverlayTransport
    {
        private readonly Dictionary<string, ConnectionInfo> _mapConnection;
        private readonly UdpTransport _udpTransport;
        private readonly List<ConnectionInfo> _connections;
        public TcpOverlayTransport(int port)
        {
            _mapConnection = new Dictionary<string, ConnectionInfo>();
            _connections = new List<ConnectionInfo>();
            _udpTransport = new UdpTransport(port);
            
            _udpTransport.OnSegmentReceived += OnSegmentReceived;
            _udpTransport.StartServer();
        }

        public void OnSegmentReceived(byte[] PacketBinary, IPEndPoint ip)
        {
            Console.Write($"{ip.ToString()}: ");
            Segment segment = Segment.Deserialize(PacketBinary);

            switch (segment.Flags.Value)
            {
                case (byte)SegmentFlags.FlagOptions.Syn:
                    HandleSYN(segment, ip);
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

        private void HandleSYN(Segment segmentReceived, IPEndPoint ip)
        {
            Segment segmentToSend = new(
                sequence: RandomSequenceNumber(segmentReceived.SequenceNumber),
                flags: new SegmentFlags((byte)(SegmentFlags.FlagOptions.Syn | SegmentFlags.FlagOptions.Ack)),
                data: segmentReceived.GetSequenceToBytes()
                );

            if (!_mapConnection.ContainsKey(ip.ToString()))
            {
                Console.WriteLine("SYN reçu (new ip)");

                _mapConnection.Add(ip.ToString(), new ConnectionInfo(ip));
                _mapConnection[ip.ToString()].UpdateConnectionInfo(
                    ConnectionInfo.ConnectionState.InitiatedConnection,
                    segmentReceived,
                    segmentToSend);
            } else
            {
                Console.WriteLine("SYN reçu (existing ip)");
                //TODO: gérer le cas où la connexion existe déjà (probablement l'ignorer ou voir si on check l'etat de la connexion)
            }
            /*
            Console.WriteLine($"Nouveau SYN reçu de {ip.ToString()}");
            _mapConnection.Add(ip.ToString(), new ConnectionInfo(ip));
            _mapConnection[ip.ToString()].State = (ConnectionInfo.ConnectionState.InitiatedConnection);
            
            _ = _udpTransport.SendSegmentAsync(
                new Segment(
                    sequence: 0,
                    flags: new SegmentFlags((byte)(SegmentFlags.FlagOptions.Syn | SegmentFlags.FlagOptions.Ack)),
                    data: Array.Empty<byte>()
                ).Serialize(),
                ip
            );
            */

        }

        public async Task sendSyn(IPEndPoint ip)
        {
            //TODO: implémenter sendSyn (ci-dessous est un code basique pour tester)
            Segment packet = new(0xAAAA, new SegmentFlags(SegmentFlags.FlagOptions.Syn), new byte[0x02]);
            byte[] PacketBinary = packet.Serialize();

            _ = _udpTransport.SendSegmentAsync(
                PacketBinary,
                ip
            );
        }

        private static ushort RandomSequenceNumber(ushort excludeNumber)
        {
            Random rand = new();
            ushort number = (ushort)rand.Next(0, ushort.MaxValue);
            if(number == excludeNumber)
                number++;
            return number;
        }
    }
}
