using Shared.Logging;
using Shared.Models;
using System.Collections.Concurrent;

namespace Shared.Protocol.TCP
{
    public class TCPSegmentProcessor
    {
        public static byte[] Process(Segment segment, Connection connectionInfo,
            ConcurrentDictionary<string, Connection> mapConnection)
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
                    byteToSend = HandleAck(segment, connectionInfo, mapConnection);
                    break;
                case (byte)SegmentFlags.FlagOptions.Rst:
                    Logger.Debug($"Received Rst segment from {connectionInfo.Endpoint}. Handling Rst segment.");
                    //TODO HandleRst(segment, connectionInfo, mapConnection);
                    break;
                default:
                    Logger.Error($"{segment.Flags.Value} : Type de segment non géré");
                    //TODO throw new Exception("Type de segment non géré");
                    break;
            }
            return byteToSend;
        }

        private static Segment HandleSyn(Segment segment, Connection connectionInfo,
            ConcurrentDictionary<string, Connection> mapConnection)
        {
            Logger.Debug($"Handling SYN segment: {segment.SequenceNumber}");
            Segment segmentToSend;
            if (connectionInfo.CurrentState != Connection.State.Closed)
            {
                Logger.Error($"Received SYN segment for an active connection from {connectionInfo.Endpoint}. Sending RST.");
                
                segmentToSend = TCPSegmentBuilder.BuildRst(segment, connectionInfo);
                mapConnection.TryRemove(connectionInfo.Endpoint.ToString(), out _);
            }
            else
            {
                segmentToSend = TCPSegmentBuilder.BuildSynAck(connectionInfo, segment);
                connectionInfo.UpdateConnectionInfo(Connection.State.SynReceived, segment, segmentToSend, true);
            }
            Logger.Debug($"Segment responded with {segmentToSend.SequenceNumber}");
            return segmentToSend;
        }
        private static Segment HandleSynAck(Segment segment, Connection connectionInfo,
            ConcurrentDictionary<string, Connection> mapConnection)
        {
            
            Logger.Debug($"Handling SYN-ACK segment: {segment.SequenceNumber}");
            Segment segmentToSend;
            if (connectionInfo.CurrentState != Connection.State.SynSent)
            {
                Logger.Error($"Received SYN-ACK segment for a connection that is not in SYN_Sent state from {connectionInfo.Endpoint}. Sending RST.");
                segmentToSend = TCPSegmentBuilder.BuildRst(segment, connectionInfo);
                mapConnection.TryRemove(connectionInfo.Endpoint.ToString(), out _);
            }
            else
            {
                if (connectionInfo.LastSegmentSent == null)
                {
                    Logger.Error($"LastSegmentSent cannot be null while receiving SYN-ACK segment in Syn_Sent State from {connectionInfo.Endpoint}");
                    mapConnection.TryRemove(connectionInfo.Endpoint.ToString(), out _);
                    return TCPSegmentBuilder.BuildRst(segment, connectionInfo);
                }
                    

                if(!connectionInfo.CheckSequenceNumber(segment.GetDataToSequence()))
                {
                    Logger.Error($"Received SYN-ACK segment with invalid sequence number. Expected: {connectionInfo.LastSegmentSent.SequenceNumber}, Received: {segment.GetDataToSequence()}. Sending RST.");
                    segmentToSend = TCPSegmentBuilder.BuildRst(segment, connectionInfo);
                    mapConnection.TryRemove(connectionInfo.Endpoint.ToString(), out _);
                } else
                {
                    segmentToSend = TCPSegmentBuilder.BuildAck(segment, connectionInfo);
                    connectionInfo.RemoveSegmentToRetransmit(segment);
                    connectionInfo.UpdateConnectionInfo(Connection.State.Established, segment, segmentToSend, false);
                    Logger.Info($"Connection established with {connectionInfo.Endpoint}");
                }
            }
            Logger.Debug($"Segment responded with {segmentToSend.SequenceNumber}");
            return segmentToSend;
        }
        private static byte[] HandleAck(Segment segment, Connection connectionInfo,
            ConcurrentDictionary<string, Connection> mapConnection)
        {
            //TODO update the code
            Logger.Debug($"Handling ACK segment: {segment}");
            
            if(connectionInfo.RemoveSegmentToRetransmit(segment))
            {
                if (connectionInfo.LastSegmentSent?.Flags.Value == (byte)(SegmentFlags.FlagOptions.Syn | SegmentFlags.FlagOptions.Ack))
                {
                    Logger.Debug($"Connection established with IP : {connectionInfo.Endpoint}");
                    return [];
                }
            } else
            {
                Logger.Error($"Received ACK segment with invalid sequence number. Expected: {connectionInfo.LastSegmentSent?.SequenceNumber}, Received: {segment.GetDataToSequence()}. Sending RST.");
                var segmentToSend = TCPSegmentBuilder.BuildRst(segment, connectionInfo);
                mapConnection.TryRemove(connectionInfo.Endpoint.ToString(), out _);
                return segmentToSend.Serialize();
            }
            return [];
        }
    }
}
