using Shared.Logging;
using Shared.Models;
using Shared.Models.Segments;
using Shared.Protocol.Serialization; 
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
                    byteToSend = SegmentSerializer.Serialize(HandleSyn(segment, connectionInfo, mapConnection));
                    break;
                case ((byte)SegmentFlags.FlagOptions.Syn | (byte)SegmentFlags.FlagOptions.Ack):
                    byteToSend = SegmentSerializer.Serialize(HandleSynAck(segment, connectionInfo, mapConnection));
                    break;
                case (byte)SegmentFlags.FlagOptions.Ack:
                    byteToSend = HandleAck(segment, connectionInfo, mapConnection);
                    break;
                case (byte)SegmentFlags.FlagOptions.Rst:
                    Logger.Debug($"Received Rst segment from {connectionInfo.Endpoint}. Handling Rst segment.");
                    //TODO HandleRst(segment, connectionInfo, mapConnection);
                    break;
                case (byte)SegmentFlags.FlagOptions.None:
                    Logger.Debug($"Received segment with no flags from {connectionInfo.Endpoint}. No action taken.");
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
                connectionInfo.UpdateConnectionInfo(Connection.State.SynReceived, segment, segmentToSend);
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
                    

                if(!connectionInfo.CheckSequenceNumber(SegmentPayloadReader.ReadAckSequence(segment)))
                {
                    Logger.Error($"Received SYN-ACK segment with invalid sequence number. Expected: {connectionInfo.LastSegmentSent.SequenceNumber+1}, Received: {SegmentPayloadReader.ReadAckSequence(segment)}. Sending RST.");
                    segmentToSend = TCPSegmentBuilder.BuildRst(segment, connectionInfo);
                    mapConnection.TryRemove(connectionInfo.Endpoint.ToString(), out _);
                } else
                {
                    segmentToSend = TCPSegmentBuilder.BuildAck(segment, connectionInfo);
                    if (connectionInfo.SendBuffer.Acknowledge(segment, connectionInfo))
                    {
                        Logger.Debug($"Received ACK segment with valid sequence number. Connection established with {connectionInfo.Endpoint}");
                        connectionInfo.UpdateConnectionInfo(Connection.State.Established, segment, segmentToSend);
                    }
                    else
                    {
                        Logger.Error($"Failed to acknowledge SYN-ACK segment. Expected: {connectionInfo.LastSegmentSent.SequenceNumber}, Received: {SegmentPayloadReader.ReadAckSequence(segment)}. Sending RST.");
                        segmentToSend = TCPSegmentBuilder.BuildRst(segment, connectionInfo);
                        mapConnection.TryRemove(connectionInfo.Endpoint.ToString(), out _);
                        return segmentToSend;
                    }
                        
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
            ushort acknowledgedSequenceNumber = SegmentPayloadReader.ReadAckSequence(segment);
            
            if(connectionInfo.SendBuffer.Acknowledge(segment, connectionInfo))
            {
                if (connectionInfo.LastSegmentSent?.Flags.Value == (byte)(SegmentFlags.FlagOptions.Syn | SegmentFlags.FlagOptions.Ack))
                {
                    connectionInfo.UpdateConnectionInfo(Connection.State.Established, segment, null);
                    Logger.Debug($"Connection established with IP : {connectionInfo.Endpoint}");
                    return [];
                }
            } else
            {
                if (connectionInfo.LastAcknowledgedSequenceId == acknowledgedSequenceNumber)
                    return [];

                Logger.Warning($"Received ACK segment with invalid sequence number. Expected: {connectionInfo.LastSegmentSent.SequenceNumber}, Received: {acknowledgedSequenceNumber}.");
                return SegmentSerializer.Serialize(connectionInfo.LastSegmentSent);
            }
            return [];
        }
    }
}
