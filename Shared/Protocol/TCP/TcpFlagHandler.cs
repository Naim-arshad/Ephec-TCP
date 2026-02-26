using Shared.Models;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace Shared.Protocol.TCP
{
    public class TcpFlagHandler
    {
        /// <summary>
        /// Handles an incoming SYN segment for a TCP-like connection, updating the connection state and preparing an
        /// appropriate response segment.
        /// </summary>
        /// <remarks>If the connection is new, the method initializes connection information and responds
        /// with a SYN-ACK segment. For existing connections, it responds with an ACK segment. The connection state and
        /// segment history are updated accordingly.</remarks>
        /// <param name="segmentReceived">The received SYN segment to process. Must not be null.</param>
        /// <param name="ip">The remote endpoint associated with the incoming segment. Used to identify or create the connection.</param>
        /// <param name="mapConnection">A dictionary mapping remote endpoint identifiers to their corresponding connection information. The method
        /// updates this map as needed.</param>
        /// <returns>A byte array representing the serialized response segment to be sent back to the remote endpoint. The
        /// segment will contain SYN-ACK or ACK flags depending on the connection state.</returns>
        public static Segment HandleSYN(Segment segmentReceived, IPEndPoint ip, ConcurrentDictionary<string, ConnectionInfo> mapConnection)
        {
            string endpointKey = ip.ToString();

            Console.WriteLine($"Packet reçu {segmentReceived}");

            bool isExistingConnection = mapConnection.TryGetValue(endpointKey, out ConnectionInfo? existingConnectionInfo);
            ConnectionInfo connectionInfo = existingConnectionInfo ?? mapConnection.GetOrAdd(endpointKey, _ =>
            {
                Console.WriteLine("SYN reçu (new connection)");
                return new ConnectionInfo(ip);
            });

            if (isExistingConnection)
            {
                Console.WriteLine("SYN reçu (existing connection)");
            }

            Segment segmentToSend;

            if (connectionInfo.CurrentState == ConnectionInfo.State.Closed)
            {
                segmentToSend = new(
                    sequence: TcpConnection.RandomSequenceNumber(segmentReceived.SequenceNumber),
                    flags: new SegmentFlags((byte)(SegmentFlags.FlagOptions.Syn | SegmentFlags.FlagOptions.Ack)),
                    data: segmentReceived.GetSequenceToBytes()
                );

                connectionInfo.UpdateConnectionInfo(
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

                connectionInfo.UpdateConnectionInfo(
                    connectionInfo.CurrentState,
                    segmentReceived,
                    segmentToSend);
            }

            return segmentToSend;
        }


    }
}
