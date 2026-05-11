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
                flags: new((byte)(SegmentFlags.FlagOptions.Syn | SegmentFlags.FlagOptions.Ack)),
                data: SegmentSerializer.SerializeSequenceNumber(
                    connection.ReceiveBuffer.FindLastOrderedSequenceNumber(segment.SequenceNumber)
                    )
                );
        }
        public static Segment BuildRst(Segment segmentReceived, Connection connection)
        {
            return new Segment(
                sequence: connection.GetNextSequenceNumber(),
                flags: new((byte)SegmentFlags.FlagOptions.Rst),
                data: []
                );
        }
        public static Segment BuildAck(Segment segment, Connection connection)
        {
            return new Segment(
                sequence: connection.GetNextSequenceNumber(),
                flags: new((byte)SegmentFlags.FlagOptions.Ack),
                data: SegmentSerializer.SerializeSequenceNumber(
                    connection.ReceiveBuffer.LastOrderedSequenceNumber
                    )
                );
        }
        public static Segment BuildData(Connection connection, byte[] data)
        {
            return new Segment(
                sequence: connection.GetNextSequenceNumber(),
                flags: new((byte)SegmentFlags.FlagOptions.None),
                data: data
                );
        }
        public static Segment BuildFin(Connection connection)
        {
            return new Segment(
                sequence: connection.GetNextSequenceNumber(),
                flags: new((byte)SegmentFlags.FlagOptions.Fin),
                data: []
                );
        }
        public static Segment BuildFinAck(Connection connection, Segment segment)
        {
            return new Segment(
                sequence: connection.GetNextSequenceNumber(),
                flags: new((byte)(SegmentFlags.FlagOptions.Fin | SegmentFlags.FlagOptions.Ack)),
                data: SegmentSerializer.SerializeSequenceNumber(
                    connection.ReceiveBuffer.LastOrderedSequenceNumber
                    )
                );
        }
    }
}
