using Shared.Models;
using System.Collections.Concurrent;
using System.Net;
using Shared.Logging;

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
        public static Segment HandleSYN(Segment segmentReceived, IPEndPoint ip, ConcurrentDictionary<string, 
            ConnectionInfo> mapConnection, TimeSpan timeout)
        {
            string endpointKey = ip.ToString();

            Logger.Debug($"Packet reçu {segmentReceived}");

            bool isExistingConnection = mapConnection.TryGetValue(endpointKey, out ConnectionInfo? existingConnectionInfo);
            ConnectionInfo connectionInfo = existingConnectionInfo ?? mapConnection.GetOrAdd(endpointKey, _ =>
            {
                Logger.Debug("SYN reçu (new connection)");
                return new ConnectionInfo(ip);
            });

            if (isExistingConnection)
            {
                Logger.Debug("SYN reçu (existing connection)"); 
                if (connectionInfo.IsExpired(timeout))
                {
                    Logger.Debug("Connexion expirée, réinitialisation de la connexion et envoie d'un SYN-ACK");
                    connectionInfo.UpdateConnectionInfo(
                        ConnectionInfo.State.Closed,
                        segmentReceived,
                        null);
                }
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
                Logger.Debug("Connexion déjà existante et active, envoie d'un ACK avec la sequence du dernier paquet recu");

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
        public static Segment HandleSYNACK(Segment segmentReceived, IPEndPoint ip, ConcurrentDictionary<string, 
            ConnectionInfo> mapConnection, TimeSpan timeout)
        {
            string endpointKey = ip.ToString();

            Logger.Debug($"Packet reçu {segmentReceived}");

            if (!mapConnection.TryGetValue(endpointKey, out ConnectionInfo? existingConnectionInfo))
            {
                //TODO: return un rst segment pour reset la connexion du coup
                Logger.Error("SYN-ACK reçu pour une connexion inexistante, reset de la connexion envoyé");
                return new(
                sequence: TcpConnection.RandomSequenceNumber(segmentReceived.SequenceNumber),
                flags: new SegmentFlags((byte)(SegmentFlags.FlagOptions.Rst)),
                data: []
            ); 
            }
            if (existingConnectionInfo.IsExpired(timeout))
            {
                Logger.Error("Connexion expirée, réinitialisation de la connexion");
                existingConnectionInfo.UpdateConnectionInfo(
                    ConnectionInfo.State.Closed,
                    segmentReceived,
                    null);
                mapConnection.TryRemove(endpointKey, out _);
                return new(
                    sequence: TcpConnection.RandomSequenceNumber(segmentReceived.SequenceNumber),
                    flags: new SegmentFlags((byte)(SegmentFlags.FlagOptions.Rst)),
                    data: []
                );
            }
            if(existingConnectionInfo.LastSegmentSent != null 
                && existingConnectionInfo.LastSegmentSent.Flags.Value == 
                    (byte)((SegmentFlags.FlagOptions.Syn) | SegmentFlags.FlagOptions.Ack))
            {
                return existingConnectionInfo.LastSegmentSent;
            }
            Logger.Debug("SYN-ACK reçu pour une connexion existante et active, envoie d'un ACK avec la sequence du dernier paquet recu");
            Segment segmentToSend = new(
                sequence: TcpConnection.RandomSequenceNumber(segmentReceived.SequenceNumber),
                flags: new SegmentFlags((byte)(SegmentFlags.FlagOptions.Ack)),
                data: segmentReceived.GetSequenceToBytes()
            );
            existingConnectionInfo.UpdateConnectionInfo(
                ConnectionInfo.State.Established,
                segmentReceived,
                segmentToSend);
            return segmentToSend;
        }
    }
}
