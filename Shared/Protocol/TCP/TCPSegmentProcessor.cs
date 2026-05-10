using Shared.Logging;
using Shared.Models;
using Shared.Models.Segments;
using Shared.Protocol.Serialization; 
using System.Collections.Concurrent;

namespace Shared.Protocol.TCP
{
    public class TCPSegmentProcessor
    {
        private const string LogSource = "tcp.processor";

        public static byte[] Process(Segment segment, Connection connectionInfo,
            ConcurrentDictionary<string, Connection> mapConnection)
        {
            byte[] byteToSend = [];
            Logger.Debug(LogSource, $"Processing segment. remote={connectionInfo.Endpoint}, state={connectionInfo.CurrentState}, {FormatSegment(segment)}");

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
                    Logger.Warning(LogSource, $"RST received. remote={connectionInfo.Endpoint}, {FormatSegment(segment)}");
                    //TODO HandleRst(segment, connectionInfo, mapConnection);
                    break;
                case (byte)SegmentFlags.FlagOptions.Fin:
                    Logger.Warning(LogSource, $"FIN received. remote={connectionInfo.Endpoint}, {FormatSegment(segment)}");
                    byteToSend = HandleFin(segment, connectionInfo, mapConnection);
                    break;
                case ((byte)SegmentFlags.FlagOptions.Fin | (byte)SegmentFlags.FlagOptions.Ack):
                    Logger.Warning(LogSource, $"FIN-ACK received. remote={connectionInfo.Endpoint}, {FormatSegment(segment)}");
                    byteToSend = HandleFinAck(segment, connectionInfo, mapConnection);
                    break;
                case (byte)SegmentFlags.FlagOptions.None:
                    byteToSend = HandleData(segment, connectionInfo, mapConnection);
                    break;
                default:
                    Logger.Error(LogSource, $"Unsupported segment type. remote={connectionInfo.Endpoint}, flags=0x{segment.Flags.Value:X2}");
                    //TODO throw new Exception("Type de segment non géré");
                    break;
            }
            return byteToSend;
        }

        private static Segment HandleSyn(Segment segment, Connection connectionInfo,
            ConcurrentDictionary<string, Connection> mapConnection)
        {
            Logger.Debug(LogSource, $"Handling SYN. remote={connectionInfo.Endpoint}, {FormatSegment(segment)}");
            Segment segmentToSend;
            if (connectionInfo.CurrentState != Connection.State.Closed)
            {
                Logger.Error(LogSource, $"SYN received for active connection. remote={connectionInfo.Endpoint}, state={connectionInfo.CurrentState}, action=send-rst");

                segmentToSend = TCPSegmentBuilder.BuildRst(segment, connectionInfo);
                mapConnection.TryRemove(connectionInfo.Endpoint.ToString(), out _);
            }
            else
            {
                segmentToSend = TCPSegmentBuilder.BuildSynAck(connectionInfo, segment);
                connectionInfo.UpdateConnectionInfo(Connection.State.SynReceived, segment, segmentToSend);
                connectionInfo.ReceiveBuffer.MarkConsumedThrough(segment.SequenceNumber);
            }
            Logger.Debug(LogSource, $"SYN handled. remote={connectionInfo.Endpoint}, response={FormatSegment(segmentToSend)}");
            return segmentToSend;
        }
        private static Segment HandleSynAck(Segment segment, Connection connectionInfo,
            ConcurrentDictionary<string, Connection> mapConnection)
        {
            Logger.Debug(LogSource, $"Handling SYN-ACK. remote={connectionInfo.Endpoint}, {FormatSegment(segment)}");
            Segment segmentToSend;
            if (connectionInfo.CurrentState != Connection.State.SynSent)
            {
                Logger.Error(LogSource, $"SYN-ACK received in invalid state. remote={connectionInfo.Endpoint}, state={connectionInfo.CurrentState}, action=send-rst");
                segmentToSend = TCPSegmentBuilder.BuildRst(segment, connectionInfo);
                mapConnection.TryRemove(connectionInfo.Endpoint.ToString(), out _);
            }
            else
            {
                if (connectionInfo.LastSegmentSent == null)
                {
                    Logger.Error(LogSource, $"SYN-ACK cannot be acknowledged because last sent segment is missing. remote={connectionInfo.Endpoint}, action=send-rst");
                    mapConnection.TryRemove(connectionInfo.Endpoint.ToString(), out _);
                    return TCPSegmentBuilder.BuildRst(segment, connectionInfo);
                }


                if (!connectionInfo.CheckSequenceNumber(SegmentPayloadReader.ReadAckSequence(segment)))
                {
                    Logger.Error(LogSource, $"SYN-ACK has invalid ACK sequence. remote={connectionInfo.Endpoint}, expected={connectionInfo.LastSegmentSent.SequenceNumber + 1}, received={SegmentPayloadReader.ReadAckSequence(segment)}, action=send-rst");
                    segmentToSend = TCPSegmentBuilder.BuildRst(segment, connectionInfo);
                    mapConnection.TryRemove(connectionInfo.Endpoint.ToString(), out _);
                }
                else
                {
                    if (connectionInfo.SendBuffer.Acknowledge(segment, connectionInfo))
                    {
                        Logger.Debug(LogSource, $"SYN-ACK acknowledged. remote={connectionInfo.Endpoint}");
                        connectionInfo.UpdateConnectionInfo(Connection.State.Established, segment, null);
                        connectionInfo.ReceiveBuffer.MarkConsumedThrough(segment.SequenceNumber);
                        segmentToSend = TCPSegmentBuilder.BuildAck(segment, connectionInfo);
                        connectionInfo.UpdateConnectionInfo(Connection.State.Established, null, segmentToSend);
                    }
                    else
                    {
                        Logger.Error(LogSource, $"Failed to acknowledge SYN-ACK. remote={connectionInfo.Endpoint}, expected={connectionInfo.LastSegmentSent.SequenceNumber}, received={SegmentPayloadReader.ReadAckSequence(segment)}, action=send-rst");
                        segmentToSend = TCPSegmentBuilder.BuildRst(segment, connectionInfo);
                        mapConnection.TryRemove(connectionInfo.Endpoint.ToString(), out _);
                        return segmentToSend;
                    }

                    Logger.Info(LogSource, $"Connection established. remote={connectionInfo.Endpoint}");
                }
            }
            Logger.Debug(LogSource, $"SYN-ACK handled. remote={connectionInfo.Endpoint}, response={FormatSegment(segmentToSend)}");
            return segmentToSend;
        }
        private static byte[] HandleAck(Segment segment, Connection connectionInfo,
            ConcurrentDictionary<string, Connection> mapConnection)
        {
            //TODO update the code
            Logger.Debug(LogSource, $"Handling ACK. remote={connectionInfo.Endpoint}, {FormatSegment(segment)}");
            ushort acknowledgedSequenceNumber = SegmentPayloadReader.ReadAckSequence(segment);

            if (connectionInfo.SendBuffer.Acknowledge(segment, connectionInfo))
            {
                if (connectionInfo.LastSegmentSent?.Flags.Value == (byte)(SegmentFlags.FlagOptions.Syn | SegmentFlags.FlagOptions.Ack))
                {
                    connectionInfo.UpdateConnectionInfo(Connection.State.Established, segment, null);
                    connectionInfo.ReceiveBuffer.MarkConsumedThrough(segment.SequenceNumber);
                    Logger.Info(LogSource, $"Connection established. remote={connectionInfo.Endpoint}");
                    return [];
                }
            }
            else
            {
                if (connectionInfo.ActiveRetransmission)
                {
                    IReadOnlyList<Segment> segmentsToRetransmit = connectionInfo.GetSegmentsToRetransmit();
                    if (segmentsToRetransmit.Count > 0)
                    {
                        Segment segmentToRetransmit = segmentsToRetransmit[0];
                        if (connectionInfo.TryMarkFastRetransmission(segmentToRetransmit.SequenceNumber))
                        {
                            Logger.Debug(
                                LogSource,
                                $"Fast retransmission requested. remote={connectionInfo.Endpoint}, sequence={segmentToRetransmit.SequenceNumber}, duplicateAck={acknowledgedSequenceNumber}");
                            return SegmentSerializer.Serialize(segmentToRetransmit);
                        }
                    }
                }

                if (connectionInfo.LastAcknowledgedSequenceId == acknowledgedSequenceNumber)
                    return [];

                Logger.Warning(LogSource, $"ACK has invalid sequence. remote={connectionInfo.Endpoint}, expected={connectionInfo.LastSegmentSent.SequenceNumber}, received={acknowledgedSequenceNumber}, action=resend-last");
                return SegmentSerializer.Serialize(connectionInfo.LastSegmentSent);
            }
            return [];
        }
        private static byte[] HandleData(Segment segment, Connection connectionInfo,
            ConcurrentDictionary<string, Connection> mapConnection)
        {
            Logger.Debug(LogSource, $"Handling DATA. remote={connectionInfo.Endpoint}, {FormatSegment(segment)}");
            if (connectionInfo.CurrentState != Connection.State.Established)
            {
                Logger.Error(LogSource, $"DATA received before connection was established. remote={connectionInfo.Endpoint}, state={connectionInfo.CurrentState}, action=send-rst");
                Segment segmentToSend = TCPSegmentBuilder.BuildRst(segment, connectionInfo);
                mapConnection.TryRemove(connectionInfo.Endpoint.ToString(), out _);
                return SegmentSerializer.Serialize(segmentToSend);
            }
            else
            {
                connectionInfo.UpdateConnectionInfo(connectionInfo.CurrentState, segment, null);
                Segment ackSegment = TCPSegmentBuilder.BuildAck(segment, connectionInfo);
                IReadOnlyList<Segment> flushedSegments = connectionInfo.FlushAcknowledgedSegmentsToFile();

                Logger.Debug(LogSource, $"DATA acknowledged. remote={connectionInfo.Endpoint}, receivedSequence={segment.SequenceNumber}, ackSequence={ackSegment.SequenceNumber}, flushedSegments={flushedSegments.Count}");
                return SegmentSerializer.Serialize(ackSegment);
            }
        }
        private static byte[] HandleFin(Segment segment, Connection connectionInfo,
            ConcurrentDictionary<string, Connection> mapConnection)
        {
            Logger.Debug(LogSource, $"Handling FIN. remote={connectionInfo.Endpoint}, {FormatSegment(segment)}");
            if (connectionInfo.CurrentState != Connection.State.Established)
            {
                Logger.Error(LogSource, $"FIN received before connection was established. remote={connectionInfo.Endpoint}, state={connectionInfo.CurrentState}, action=send-rst");
                Segment segmentToSend = TCPSegmentBuilder.BuildRst(segment, connectionInfo);
                mapConnection.TryRemove(connectionInfo.Endpoint.ToString(), out _);
                return SegmentSerializer.Serialize(segmentToSend);
            }
            else
            {
                Segment ackSegment = TCPSegmentBuilder.BuildFinAck(connectionInfo, segment);
                connectionInfo.UpdateConnectionInfo(Connection.State.CloseWait, segment, ackSegment);
                Logger.Info(LogSource, $"Connection closing. remote={connectionInfo.Endpoint}");
                return SegmentSerializer.Serialize(ackSegment);
            }
        }
        private static byte[] HandleFinAck(Segment segment, Connection connectionInfo,
            ConcurrentDictionary<string, Connection> mapConnection)
        {
            Logger.Debug(LogSource, $"Handling FIN-ACK. remote={connectionInfo.Endpoint}, {FormatSegment(segment)}");
            if (connectionInfo.CurrentState != Connection.State.FinWait1)
            {
                Logger.Error(LogSource, $"FIN-ACK received in invalid state. remote={connectionInfo.Endpoint}, state={connectionInfo.CurrentState}, action=send-rst");
                Segment segmentToSend = TCPSegmentBuilder.BuildRst(segment, connectionInfo);
                mapConnection.TryRemove(connectionInfo.Endpoint.ToString(), out _);
                return SegmentSerializer.Serialize(segmentToSend);
            }
            else
            {
                connectionInfo.UpdateConnectionInfo(Connection.State.TimeWait, segment, null);
                mapConnection.TryRemove(connectionInfo.Endpoint.ToString(), out _);
                Logger.Info(LogSource, $"Connection closed. remote={connectionInfo.Endpoint}");
                return [];
            }
        }
        private static string FormatSegment(Segment segment)
        {
            return $"sequence={segment.SequenceNumber}, flags={segment.Flags}, payloadBytes={segment.Data.Length}";
        }
    }
}
