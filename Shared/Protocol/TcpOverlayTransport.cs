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

        public TcpOverlayTransport(int port)
        {
            _mapConnection = new Dictionary<string, ConnectionInfo>();
            _udpTransport = new UdpTransport(port);
            
            _udpTransport.OnSegmentReceived += OnSegmentReceived;
            _udpTransport.StartServer();
        }

        public void OnSegmentReceived(byte[] PacketBinary, IPEndPoint ip)
        {
            Segment segment = Segment.Deserialize(PacketBinary);

            if (!_mapConnection.ContainsKey(ip.ToString()))
            {
                if (segment.Flags.Value == (byte)SegmentFlags.FlagOptions.Syn)
                    HandleSYN(segment, ip);
                else 
                {
                    Console.WriteLine("Flags autre que Syn pour une IP non reconnu" + Segment.BytesToHexString(PacketBinary));
                }
                    
            }
            else
            {
                switch (segment.Flags.Value)
                {
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
        }

        private void HandleSYN(Segment segment, IPEndPoint ip)
        {
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

        }

        public async Task sendSyn(IPEndPoint ip)
        {
            Segment packet = new Segment(0xAAAA, new SegmentFlags(SegmentFlags.FlagOptions.Syn), new byte[0x02]);
            byte[] PacketBinary = packet.Serialize();

            _ = _udpTransport.SendSegmentAsync(
                PacketBinary,
                ip
            );
        }
    }
}
