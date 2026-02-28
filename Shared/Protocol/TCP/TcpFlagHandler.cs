using Shared.Models;
using System.Collections.Concurrent;
using System.Net;
using System.Buffers.Binary;

namespace Shared.Protocol.TCP
{
    public class TcpFlagHandler
    {
        public static Segment HandleSYN(Segment segmentReceived, IPEndPoint ip, ConcurrentDictionary<string, ConnectionInfo> mapConnection)
        {
            string key = ip.ToString();
            ConnectionInfo info = mapConnection.GetOrAdd(key, _ => new ConnectionInfo(ip));

            ushort x = segmentReceived.SequenceNumber;
            ushort y = TcpConnection.RandomSequenceNumber();

            info.LocalISN = y;
            info.LocalNextSeq = (ushort)(y + 1);
            info.RemoteNextSeq = (ushort)(x + 1);

            byte[] ackPayload = new byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(ackPayload, (ushort)(x + 1));

            Segment synAck = new Segment(y, new SegmentFlags((byte)(SegmentFlags.FlagOptions.Syn | SegmentFlags.FlagOptions.Ack)), ackPayload);
            info.UpdateConnectionInfo(ConnectionInfo.State.SynReceived, segmentReceived, synAck);

            return synAck;
        }
    }
}