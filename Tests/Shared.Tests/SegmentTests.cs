using System;
using System.Linq;
using System.Reflection;
using System.Buffers.Binary;
using Xunit;
using Shared.Models;

namespace Shared.Tests
{
    public class SegmentTests
    {
        [Fact]
        public void Constructor_NullData_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new Segment(0, new SegmentFlags(SegmentFlags.FlagOptions.Syn), null));
        }

        [Fact]
        public void Constructor_DataTooLarge_ThrowsArgumentException()
        {
            byte[] tooLarge = new byte[65536]; // > 65535
            Assert.Throws<ArgumentException>(() => new Segment(0, new SegmentFlags(SegmentFlags.FlagOptions.Syn), tooLarge));
        }

        [Fact]
        public void Constructor_InvalidFlags_SynAndFin_ThrowsInvalidOperationException()
        {
            byte[] data = Array.Empty<byte>();
            Assert.Throws<InvalidOperationException>(() => new Segment(0, new SegmentFlags(SegmentFlags.FlagOptions.Syn | SegmentFlags.FlagOptions.Fin), data));
        }

        [Fact]
        public void Constructor_InvalidFlags_AckAndRst_ThrowsInvalidOperationException()
        {
            byte[] data = Array.Empty<byte>();
            Assert.Throws<InvalidOperationException>(() => new Segment(0, new SegmentFlags(SegmentFlags.FlagOptions.Ack | SegmentFlags.FlagOptions.Rst), data));
        }

        [Fact]
        public void Serialize_ReturnsExpectedLayout()
        {
            byte[] data = new byte[] { 0xAA, 0xBB };
            ushort seq = 0x1234;
            var flags = new SegmentFlags(SegmentFlags.FlagOptions.Syn | SegmentFlags.FlagOptions.Ack);
            var pkt = new Segment(seq, flags, data);

            byte[] bytes = pkt.Serialize();

            Assert.Equal(5 + data.Length, bytes.Length);

            ushort reportedLength = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(0, 2));
            Assert.Equal((ushort)data.Length, reportedLength);

            ushort reportedSeq = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(2, 2));
            Assert.Equal(seq, reportedSeq);

            Assert.Equal((byte)flags.Value, bytes[4]);

            Assert.True(data.SequenceEqual(bytes.Skip(5).ToArray()));
        }

        [Fact]
        public void SerializeDeserialize_Roundtrip_PreservesAllFields()
        {
            byte[] data = new byte[] { 1, 2, 3, 4, 5 };
            ushort seq = 0xAAAA;
            var flags = new SegmentFlags(SegmentFlags.FlagOptions.Fin);
            var pkt = new Segment(seq, flags, data);

            byte[] bin = pkt.Serialize();
            Segment deserialized = Segment.Deserialize(bin);

            Assert.Equal(pkt.SequenceNumber, deserialized.SequenceNumber);
            Assert.Equal(pkt.Flags, deserialized.Flags);
            Assert.True(pkt.Data.SequenceEqual(deserialized.Data));
        }

        [Fact]
        public void Deserialize_Null_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => Segment.Deserialize(null));
        }

        [Fact]
        public void Deserialize_TooShort_ThrowsArgumentException()
        {
            byte[] tooShort = new byte[4];
            Assert.Throws<ArgumentException>(() => Segment.Deserialize(tooShort));
        }

        [Fact]
        public void Deserialize_MismatchedLength_ThrowsArgumentException()
        {
            ushort declaredLength = 5;
            ushort seq = 0x0001;
            byte flags = (byte)SegmentFlags.FlagOptions.Syn;
            byte[] payload = new byte[3]; // réel = 3
            byte[] buffer = new byte[5 + payload.Length];

            BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(0, 2), declaredLength);
            BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(2, 2), seq);
            buffer[4] = flags;
            Array.Copy(payload, 0, buffer, 5, payload.Length);

            Assert.Throws<ArgumentException>(() => Segment.Deserialize(buffer));
        }

        [Fact]
        public void BytesToHexString_FormatsCorrectly()
        {
            byte[] bytes = new byte[] { 0x0A, 0x1B, 0xFF };
            string hex = Segment.BytesToHexString(bytes);
            Assert.Equal("0A 1B FF", hex);
        }

        [Fact]
        public void Private_AreFlagsValid_ReturnsExpectedViaReflection()
        {
            var pkt = new Segment(0, new SegmentFlags(SegmentFlags.FlagOptions.Syn), Array.Empty<byte>());

            MethodInfo mi = typeof(SegmentFlags).GetMethod("AreFlagsValid", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(mi);

            bool synValid = (bool)mi.Invoke(pkt, new object[] { SegmentFlags.FlagOptions.Syn });
            Assert.True(synValid);

            bool synFinValid = (bool)mi.Invoke(pkt, new object[] { SegmentFlags.FlagOptions.Syn | SegmentFlags.FlagOptions.Fin });
            Assert.False(synFinValid);

            bool ackRstValid = (bool)mi.Invoke(pkt, new object[] { SegmentFlags.FlagOptions.Ack | SegmentFlags.FlagOptions.Rst });
            Assert.False(ackRstValid);

            bool ackValid = (bool)mi.Invoke(pkt, new object[] { SegmentFlags.FlagOptions.Ack });
            Assert.True(ackValid);
        }
    }
}