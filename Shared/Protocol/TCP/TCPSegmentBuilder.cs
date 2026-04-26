using Shared.Models;
using Shared.Models.Connection;


namespace Shared.Protocol.TCP
{
    public class TCPSegmentBuilder
    {
        public static Segment BuildSyn(Connection connection)
        {
            return new Segment(
                sequence: connection.GetNextSequenceNumber(),
                flags: new((byte)SegmentFlags.FlagOptions.Syn),
                data: []);
                
        }
        public static Segment BuildSynAck(Connection connection, Segment segment)
        {
            return new Segment(
                sequence: connection.GetNextSequenceNumber(),
                flags: new ((byte)(SegmentFlags.FlagOptions.Syn | SegmentFlags.FlagOptions.Ack)),
                data: segment.GetSequenceToBytes()
                );
        }
        public static Segment BuildRst(Segment segmentReceived, Connection connection)
        {
            return new Segment(
                sequence: connection.GetNextSequenceNumber(),
                flags: new ((byte)SegmentFlags.FlagOptions.Rst),
                data: segmentReceived.GetSequenceToBytes()
                );
        }
        public static Segment BuildAck(Segment segmentReceived, Connection connection)
        {
            return new Segment(
                sequence: connection.GetNextSequenceNumber(),
                flags: new ((byte)SegmentFlags.FlagOptions.Ack),
                data: segmentReceived.GetSequenceToBytes()
                );
        }
    }
}
