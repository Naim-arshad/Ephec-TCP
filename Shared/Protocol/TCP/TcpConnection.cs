using Shared.Models;
using System;
using System.Collections.Generic;
using System.Net;

namespace Shared.Protocol.TCP
{
    public class TcpConnection
    {
        private readonly Dictionary<string, ConnectionInfo> _mapConnection;
        private readonly UdpTransport _udpTransport;

        public TcpConnection(int port)
        {
            _mapConnection = new Dictionary<string, ConnectionInfo>();
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

            ConnectionInfo connectionInfo;

            Console.WriteLine($"Packet reçu {segmentReceived}");

            if (_mapConnection.ContainsKey(ip.ToString()))
            {
                Console.WriteLine("SYN reçu (existing connection)");
                connectionInfo = _mapConnection[ip.ToString()];
            } else
            {
                Console.WriteLine("SYN reçu (new connection)");
                connectionInfo = new(ip);
                _mapConnection.Add(ip.ToString(), connectionInfo);
            }

            Segment segmentToSend;

            if (connectionInfo.CurrentState == ConnectionInfo.State.Closed)
            {
                segmentToSend = new(
                    sequence: RandomSequenceNumber(segmentReceived.SequenceNumber),
                    flags: new SegmentFlags((byte)(SegmentFlags.FlagOptions.Syn | SegmentFlags.FlagOptions.Ack)),
                    data: segmentReceived.GetSequenceToBytes()
                );

                _mapConnection[ip.ToString()].UpdateConnectionInfo(
                    ConnectionInfo.State.SynReceived,
                    segmentReceived,
                    segmentToSend);
            } else
            {
                //TODO peut etre extraire ce code et en faire une fonction pour éviter la duplication de code reponse ACK
                Console.WriteLine("Connexion déjà existante et active, envoie d'un ACK avec la sequence du dernier paquet recu");

                segmentToSend = new(
                    sequence: RandomSequenceNumber(segmentReceived.SequenceNumber),
                    flags: new SegmentFlags((byte)(SegmentFlags.FlagOptions.Ack)),
                    data: segmentReceived.GetSequenceToBytes()
                );

                _mapConnection[ip.ToString()].UpdateConnectionInfo(
                _mapConnection[ip.ToString()].CurrentState,
                segmentReceived,
                segmentToSend);
            }
            _ = _udpTransport.SendSegmentAsync(segmentToSend.Serialize(), ip);
        }

        public async Task SendSyn(IPEndPoint ip)
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
            if (number == excludeNumber && (number + 2) <= ushort.MaxValue)
                number += 2;
            else
                number = 0;
            return number;
        }
    }
}
