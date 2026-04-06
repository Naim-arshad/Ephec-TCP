using System;
using System.Linq;
using System.Net;
using Shared.Models;
using Xunit;

namespace Shared.Models.Tests
{
    public class ConnectionInfoTests
    {
        private static ConnectionInfo CreateConnectionInfo(TimeSpan? timeout = null)
        {
            return new ConnectionInfo(new IPEndPoint(IPAddress.Loopback, 5000), timeout ?? TimeSpan.FromSeconds(5));
        }

        private static Segment CreateSegment(ushort sequence = 1, SegmentFlags.FlagOptions flags = SegmentFlags.FlagOptions.Syn)
        {
            return new Segment(sequence, new SegmentFlags(flags), Array.Empty<byte>());
        }

        [Fact]
        public void Constructor_NullEndpoint_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new ConnectionInfo(null!, TimeSpan.FromSeconds(1)));
        }

        [Fact]
        public void Constructor_InitializesExpectedDefaults()
        {
            var endpoint = new IPEndPoint(IPAddress.Loopback, 4040);
            var timeout = TimeSpan.FromSeconds(2);

            var connectionInfo = new ConnectionInfo(endpoint, timeout);

            Assert.Equal(endpoint, connectionInfo.Endpoint);
            Assert.Equal(ConnectionInfo.State.Closed, connectionInfo.CurrentState);
            Assert.Equal(timeout, connectionInfo.Timeout);
            Assert.Null(connectionInfo.LastSegmentReceived);
            Assert.Null(connectionInfo.LastSegmentSent);
            Assert.Empty(connectionInfo.GetSegmentsToRetransmit());
        }

        [Fact]
        public void BeginConnection_NullSegment_ThrowsArgumentNullException()
        {
            var connectionInfo = CreateConnectionInfo();

            Assert.Throws<ArgumentNullException>(() => connectionInfo.BeginConnection(null!));
        }

        [Fact]
        public void BeginConnection_UpdatesStateAndRegistersSentSegment()
        {
            var connectionInfo = CreateConnectionInfo();
            var sent = CreateSegment(sequence: 10, flags: SegmentFlags.FlagOptions.Syn);

            connectionInfo.BeginConnection(sent);

            Assert.Equal(ConnectionInfo.State.SynSent, connectionInfo.CurrentState);
            Assert.Same(sent, connectionInfo.LastSegmentSent);
            Assert.Null(connectionInfo.LastSegmentReceived);
            Assert.Contains(sent, connectionInfo.GetSegmentsToRetransmit());
            Assert.True(connectionInfo.LastActivity > 0);
        }

        [Fact]
        public void UpdateConnectionInfo_BothSegmentsNull_ThrowsArgumentException()
        {
            var connectionInfo = CreateConnectionInfo();

            Assert.Throws<ArgumentException>(() =>
                connectionInfo.UpdateConnectionInfo(ConnectionInfo.State.Established, null, null));
        }

        [Fact]
        public void UpdateConnectionInfo_OnlyReceivedSegment_UpdatesReceivedAndPreservesSent()
        {
            var connectionInfo = CreateConnectionInfo();
            var sent = CreateSegment(sequence: 20, flags: SegmentFlags.FlagOptions.Syn);
            connectionInfo.BeginConnection(sent);

            var received = CreateSegment(sequence: 21, flags: SegmentFlags.FlagOptions.Ack);
            connectionInfo.UpdateConnectionInfo(ConnectionInfo.State.Established, received, null);

            Assert.Equal(ConnectionInfo.State.Established, connectionInfo.CurrentState);
            Assert.Same(received, connectionInfo.LastSegmentReceived);
            Assert.Same(sent, connectionInfo.LastSegmentSent);
            Assert.Single(connectionInfo.GetSegmentsToRetransmit());
        }

        [Fact]
        public void UpdateConnectionInfo_OnlySentSegment_UpdatesSentAndAddsToRetransmitMap()
        {
            var connectionInfo = CreateConnectionInfo();
            var first = CreateSegment(sequence: 31, flags: SegmentFlags.FlagOptions.Syn);
            connectionInfo.BeginConnection(first);

            var second = CreateSegment(sequence: 32, flags: SegmentFlags.FlagOptions.Ack);
            connectionInfo.UpdateConnectionInfo(ConnectionInfo.State.Established, null, second);

            var pending = connectionInfo.GetSegmentsToRetransmit().ToArray();

            Assert.Equal(2, pending.Length);
            Assert.Contains(first, pending);
            Assert.Contains(second, pending);
            Assert.Same(second, connectionInfo.LastSegmentSent);
        }

        [Fact]
        public void UpdateConnectionInfo_DuplicateSentSequence_ThrowsArgumentException()
        {
            var connectionInfo = CreateConnectionInfo();
            var first = CreateSegment(sequence: 40, flags: SegmentFlags.FlagOptions.Syn);
            connectionInfo.BeginConnection(first);

            var duplicateSequence = CreateSegment(sequence: 40, flags: SegmentFlags.FlagOptions.Ack);

            Assert.Throws<ArgumentException>(() =>
                connectionInfo.UpdateConnectionInfo(ConnectionInfo.State.Established, null, duplicateSequence));
        }

        [Fact]
        public void MarkSegmentAsAcknowledged_ExistingSequence_RemovesSegmentAndSetsAckReceived()
        {
            var connectionInfo = CreateConnectionInfo();
            var sent = new Segment(50, new SegmentFlags(SegmentFlags.FlagOptions.Syn), Array.Empty<byte>(), ackReceived: false);
            connectionInfo.BeginConnection(sent);

            connectionInfo.MarkSegmentAsAcknowledged(50);

            Assert.True(sent.AckReceived);
            Assert.Empty(connectionInfo.GetSegmentsToRetransmit());
        }

        [Fact]
        public void MarkSegmentAsAcknowledged_UnknownSequence_DoesNothing()
        {
            var connectionInfo = CreateConnectionInfo();
            var sent = new Segment(60, new SegmentFlags(SegmentFlags.FlagOptions.Syn), Array.Empty<byte>(), ackReceived: false);
            connectionInfo.BeginConnection(sent);

            connectionInfo.MarkSegmentAsAcknowledged(999);

            Assert.False(sent.AckReceived);
            Assert.Single(connectionInfo.GetSegmentsToRetransmit());
        }

        [Fact]
        public void CheckSequenceNumber_LastSegmentSentNull_ThrowsArgumentNullException()
        {
            var connectionInfo = CreateConnectionInfo();

            Assert.Throws<ArgumentNullException>(() => connectionInfo.CheckSequenceNumber(1));
        }

        [Fact]
        public void CheckSequenceNumber_ReturnsExpectedResult()
        {
            var connectionInfo = CreateConnectionInfo();
            var sent = CreateSegment(sequence: 70, flags: SegmentFlags.FlagOptions.Syn);
            connectionInfo.BeginConnection(sent);

            Assert.True(connectionInfo.CheckSequenceNumber(70));
            Assert.False(connectionInfo.CheckSequenceNumber(71));
        }

        [Fact]
        public void GetNextSequenceNumber_ReturnsMonotonicSequence()
        {
            var connectionInfo = CreateConnectionInfo();

            ushort first = connectionInfo.GetNextSequenceNumber();
            ushort second = connectionInfo.GetNextSequenceNumber();

            Assert.Equal((ushort)(first + 1), second);
        }

        [Fact]
        public void IsExpired_RespectsTimeoutBasedOnLastActivity()
        {
            var connectionInfo = CreateConnectionInfo(TimeSpan.FromMilliseconds(250));
            var sent = CreateSegment(sequence: 80, flags: SegmentFlags.FlagOptions.Syn);

            connectionInfo.BeginConnection(sent);

            Assert.False(connectionInfo.IsExpired());
        }

        [Fact]
        public void IsExpired_WhenNoActivityAndZeroTimeout_ReturnsTrue()
        {
            var connectionInfo = CreateConnectionInfo(TimeSpan.Zero);

            Assert.True(connectionInfo.IsExpired());
        }

        [Fact]
        public void GetSegmentsToRetransmit_ReturnsSnapshot()
        {
            var connectionInfo = CreateConnectionInfo();
            var first = CreateSegment(sequence: 90, flags: SegmentFlags.FlagOptions.Syn);
            var second = CreateSegment(sequence: 91, flags: SegmentFlags.FlagOptions.Ack);
            connectionInfo.BeginConnection(first);
            connectionInfo.UpdateConnectionInfo(ConnectionInfo.State.Established, null, second);

            var snapshot = connectionInfo.GetSegmentsToRetransmit().ToList();
            snapshot.Clear();

            Assert.Equal(2, connectionInfo.GetSegmentsToRetransmit().Count());
        }
    }
}
