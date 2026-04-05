using Shared.Logging;
using Shared.Models;
using System.Collections.Concurrent;

namespace Shared.Protocol.TCP
{
    public class TCPSegmentProcessor
    {
        public static byte[] Process(Segment segment, ConnectionInfo connectionInfo, UdpTransport udpTransport,
            ConcurrentDictionary<string, ConnectionInfo> mapConnection)
        {
            byte[] byteToSend = [];

            switch (segment.Flags.Value)
            {
                case (byte)SegmentFlags.FlagOptions.Syn:
                    byteToSend = HandleSyn(segment, connectionInfo, mapConnection);
                    break;
                case ((byte)SegmentFlags.FlagOptions.Syn | (byte)SegmentFlags.FlagOptions.Ack):
                    byteToSend = TcpFlagHandler.HandleSYNACK(segment, connectionInfo.Endpoint, _mapConnection, _timeout)
                        .Serialize();
                    break;
                // Ajouter d'autres cas pour gérer différents types de segments
                case (byte)SegmentFlags.FlagOptions.Ack:
                    Console.WriteLine("TODO: Gérer l'ACK");
                    break;
                default:
                    Logger.Error($"{segment.Flags.Value} : Type de segment non géré");
                    throw new Exception("Type de segment non géré");
            }
            return byteToSend;
        }

        private static Segment HandleSyn(Segment segment, ConnectionInfo connectionInfo,
            ConcurrentDictionary<string, ConnectionInfo> mapConnection)
        {
            Logger.Debug($"Handling SYN segment: {segment}");
            Segment segmentToSend;
            if(connectionInfo.CurrentState != ConnectionInfo.State.Closed)
            {
                segmentToSend = new(
                    sequence: TcpConnection.RandomSequenceNumber(segment.SequenceNumber),
                    flags: new ((byte) SegmentFlags.FlagOptions.Rst),
                    data: []);

                mapConnection.TryRemove(connectionInfo.Endpoint.ToString(), out _);
            } else
            {
                if(connectionInfo.LastSegmentSent != null)
                {
                    segmentToSend = connectionInfo.LastSegmentSent;
                } else
                {
                    segmentToSend = new(
                        sequence: TcpConnection.RandomSequenceNumber(segment.SequenceNumber),
                        flags: new ((byte) SegmentFlags.FlagOptions.Syn | (byte) SegmentFlags.FlagOptions.Ack),
                        data: segment.GetSequenceToBytes());
                }

                connectionInfo.UpdateConnectionInfo(ConnectionInfo.State.SynReceived, segment, segmentToSend);
            }
            return segmentToSend;
        }
    }
}
