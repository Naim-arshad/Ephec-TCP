using Shared.Models;


namespace Shared.Protocol.TCP
{
    public class TCPSegmentBuilder
    {
        public static Segment BuildSyn()
        {
            return new Segment(
                sequence: TcpConnection.RandomSequenceNumber(0),
                flags: new ((byte)SegmentFlags.FlagOptions.Syn),
                data: []);
                
        }
        public static Segment BuildSynAck(Segment segmentReceived)
        {
            return new Segment(
                sequence: TcpConnection.RandomSequenceNumber(0),
                flags: new ((byte)(SegmentFlags.FlagOptions.Syn | SegmentFlags.FlagOptions.Ack)),
                data: segmentReceived.GetSequenceToBytes()
                );
        }
        public static Segment BuildRst(Segment segmentReceived, Segment? lastSegmentSent)
        {
            int lastSequenceSent = lastSegmentSent != null ?
                        lastSegmentSent.SequenceNumber : TcpConnection.RandomSequenceNumber(segmentReceived.SequenceNumber);
            lastSequenceSent++;
            return new Segment(
                sequence: (ushort)lastSequenceSent,
                flags: new ((byte)SegmentFlags.FlagOptions.Rst),
                data: segmentReceived.GetSequenceToBytes()
                );
        }
        public static Segment BuildAck(Segment segmentReceived, Segment? lastSegmentSent)
        {
            int lastSequenceSent = lastSegmentSent != null ?
                        lastSegmentSent.SequenceNumber : TcpConnection.RandomSequenceNumber(segmentReceived.SequenceNumber);
            lastSequenceSent++;
            return new Segment(
                sequence: (ushort)lastSequenceSent,
                flags: new ((byte)SegmentFlags.FlagOptions.Ack),
                data: segmentReceived.GetSequenceToBytes()
                );
        }
    }
}
