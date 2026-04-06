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
                    byteToSend = HandleSyn(segment, connectionInfo, mapConnection).Serialize();
                    break;
                case ((byte)SegmentFlags.FlagOptions.Syn | (byte)SegmentFlags.FlagOptions.Ack):
                    byteToSend = HandleSynAck(segment, connectionInfo, mapConnection).Serialize();
                    break;
                case (byte)SegmentFlags.FlagOptions.Ack:
                    HandleAck(segment, connectionInfo, mapConnection);
                    break;
                case (byte)SegmentFlags.FlagOptions.Rst:
                    Logger.Debug($"Received Rst segment from {connectionInfo.Endpoint}. Handling Rst segment.");
                    //TODO HandleFin(segment, connectionInfo, mapConnection);
                    break;
                default:
                    Logger.Error($"{segment.Flags.Value} : Type de segment non géré");
                    //TODO throw new Exception("Type de segment non géré");
                    break;
            }
            return byteToSend;
        }

        private static Segment HandleSyn(Segment segment, ConnectionInfo connectionInfo,
            ConcurrentDictionary<string, ConnectionInfo> mapConnection)
        {
            Logger.Debug($"Handling SYN segment: {segment}");
            Segment segmentToSend;
            if (connectionInfo.CurrentState != ConnectionInfo.State.Closed)
            {
                Logger.Error($"Received SYN segment for an active connection from {connectionInfo.Endpoint}. Sending RST.");
                
                segmentToSend = TCPSegmentBuilder.BuildRst(segment, connectionInfo.LastSegmentSent);
                mapConnection.TryRemove(connectionInfo.Endpoint.ToString(), out _);
            }
            else
            {
                if (connectionInfo.LastSegmentSent != null &&
                    connectionInfo.LastSegmentSent.Flags.Value == (byte)(SegmentFlags.FlagOptions.Syn | SegmentFlags.FlagOptions.Ack))
                {
                    segmentToSend = connectionInfo.LastSegmentSent;
                }
                else
                {
                    segmentToSend = TCPSegmentBuilder.BuildSynAck(segment);
                }
                connectionInfo.UpdateConnectionInfo(ConnectionInfo.State.SynReceived, segment, segmentToSend);
            }
            return segmentToSend;
        }
        private static Segment HandleSynAck(Segment segment, ConnectionInfo connectionInfo,
            ConcurrentDictionary<string, ConnectionInfo> mapConnection)
        {
            
            Logger.Debug($"Handling SYN-ACK segment: {segment}");
            Segment segmentToSend;
            if (connectionInfo.CurrentState != ConnectionInfo.State.SynSent)
            {
                Logger.Error($"Received SYN-ACK segment for a connection that is not in SYN_Sent state from {connectionInfo.Endpoint}. Sending RST.");
                segmentToSend = TCPSegmentBuilder.BuildRst(segment, connectionInfo.LastSegmentSent);
                mapConnection.TryRemove(connectionInfo.Endpoint.ToString(), out _);
            }
            else
            {
                if (connectionInfo.LastSegmentSent == null)
                {
                    Logger.Error($"LastSegmentSent cannot be null while receiving SYN-ACK segment in Syn_Sent State from {connectionInfo.Endpoint}");
                    mapConnection.TryRemove(connectionInfo.Endpoint.ToString(), out _);
                    return TCPSegmentBuilder.BuildRst(segment, connectionInfo.LastSegmentSent);
                }
                    

                if(connectionInfo.LastSegmentSent.SequenceNumber != segment.GetDataToSequence())
                {
                    Logger.Error($"Received SYN-ACK segment with unexpected sequence number from {connectionInfo.Endpoint}. Expected: {connectionInfo.LastSegmentSent.SequenceNumber}, Received: {BitConverter.ToUInt16(segment.Data, 0)}. Sending RST.");
                    segmentToSend = TCPSegmentBuilder.BuildRst(segment, connectionInfo.LastSegmentSent);
                    mapConnection.TryRemove(connectionInfo.Endpoint.ToString(), out _);
                } else
                {
                    segmentToSend = TCPSegmentBuilder.BuildAck(segment, connectionInfo.LastSegmentSent);
                    connectionInfo.MarkSegmentAsAcknowledged(connectionInfo.LastSegmentSent.SequenceNumber);
                    connectionInfo.UpdateConnectionInfo(ConnectionInfo.State.Established, segment, segmentToSend);
                    connectionInfo.MarkSegmentAsAcknowledged(segmentToSend.SequenceNumber);
                    Logger.Info($"Connection established with {connectionInfo.Endpoint}");
                }
            }
            return segmentToSend;
        }
        private static Segment HandleAck(Segment segment, ConnectionInfo connectionInfo,
            ConcurrentDictionary<string, ConnectionInfo> mapConnection)
        {
            Logger.Debug($"Handling ACK segment: {segment}");
            connectionInfo.MarkSegmentAsAcknowledged(segment.SequenceNumber);
            Logger.Debug($"Connection established with IP : {connectionInfo.Endpoint}");
            return null;
        }
    }
}
