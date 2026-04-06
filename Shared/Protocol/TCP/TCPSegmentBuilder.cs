using Shared.Models;


namespace Shared.Protocol.TCP
{
    public class TCPSegmentBuilder
    {
        public static Segment BuildSyn(ConnectionInfo connection)
        {
            return new Segment(
                sequence: connection.GetNextSequenceNumber(),
                flags: new((byte)SegmentFlags.FlagOptions.Syn),
                data: []);
                
        }
        public static Segment BuildSynAck(ConnectionInfo connection, Segment segment)
        {
            return new Segment(
                sequence: connection.GetNextSequenceNumber(),
                flags: new ((byte)(SegmentFlags.FlagOptions.Syn | SegmentFlags.FlagOptions.Ack)),
                data: segment.GetSequenceToBytes()
                );
        }
        public static Segment BuildRst(Segment segmentReceived, ConnectionInfo connection)
        {
            return new Segment(
                sequence: connection.GetNextSequenceNumber(),
                flags: new ((byte)SegmentFlags.FlagOptions.Rst),
                data: segmentReceived.GetSequenceToBytes()
                );
        }
        public static Segment BuildAck(Segment segmentReceived, ConnectionInfo connection)
        {
            return new Segment(
                sequence: connection.GetNextSequenceNumber(),
                flags: new ((byte)SegmentFlags.FlagOptions.Ack),
                data: segmentReceived.GetSequenceToBytes()
                );
        }
    }
}
