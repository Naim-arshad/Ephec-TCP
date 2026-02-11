using Shared.Models;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Shared.Protocol.TCP
{
    public class TcpFlagHandler
    {
        public static byte[] HandleSYN(Segment segmentReceived, IPEndPoint ip, Dictionary<string, ConnectionInfo> mapConnection)
        {

            ConnectionInfo connectionInfo;

            Console.WriteLine($"Packet reçu {segmentReceived}");

            if (mapConnection.ContainsKey(ip.ToString()))
            {
                Console.WriteLine("SYN reçu (existing connection)");
                connectionInfo = mapConnection[ip.ToString()];
            }
            else
            {
                Console.WriteLine("SYN reçu (new connection)");
                connectionInfo = new(ip);
                mapConnection.Add(ip.ToString(), connectionInfo);
            }

            Segment segmentToSend;

            if (connectionInfo.CurrentState == ConnectionInfo.State.Closed)
            {
                segmentToSend = new(
                    sequence: TcpConnection.RandomSequenceNumber(segmentReceived.SequenceNumber),
                    flags: new SegmentFlags((byte)(SegmentFlags.FlagOptions.Syn | SegmentFlags.FlagOptions.Ack)),
                    data: segmentReceived.GetSequenceToBytes()
                );

                mapConnection[ip.ToString()].UpdateConnectionInfo(
                    ConnectionInfo.State.SynReceived,
                    segmentReceived,
                    segmentToSend);
            }
            else
            {
                //TODO peut etre extraire ce code et en faire une fonction pour éviter la duplication de code reponse ACK
                Console.WriteLine("Connexion déjà existante et active, envoie d'un ACK avec la sequence du dernier paquet recu");

                segmentToSend = new(
                    sequence: TcpConnection.RandomSequenceNumber(segmentReceived.SequenceNumber),
                    flags: new SegmentFlags((byte)(SegmentFlags.FlagOptions.Ack)),
                    data: segmentReceived.GetSequenceToBytes()
                );

                mapConnection[ip.ToString()].UpdateConnectionInfo(
                mapConnection[ip.ToString()].CurrentState,
                segmentReceived,
                segmentToSend);
            }

            return segmentToSend.Serialize();
        }

    }
}
