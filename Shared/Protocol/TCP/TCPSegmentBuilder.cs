using Shared.Models;
using Shared.Models.Segments;
using Shared.Protocol.Serialization;


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
                data: SegmentSerializer.SerializeSequenceNumber(segment)
                );
        }
        public static Segment BuildRst(Segment segmentReceived, Connection connection)
        {
            return new Segment(
                sequence: connection.GetNextSequenceNumber(),
                flags: new ((byte)SegmentFlags.FlagOptions.Rst),
                data: []
                );
        }
        public static Segment BuildAck(Segment segmentReceived, Connection connection)
        {
            return new Segment(
                sequence: connection.GetNextSequenceNumber(),
                flags: new ((byte)SegmentFlags.FlagOptions.Ack),
                data: SegmentSerializer.SerializeSequenceNumber(segmentReceived)
                );
        }
    }
}
