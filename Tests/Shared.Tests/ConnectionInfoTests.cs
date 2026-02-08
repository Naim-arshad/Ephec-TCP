using System;
using System.Linq;
using System.Reflection;
using System.Buffers.Binary;
using System.Net;
using Xunit;
using Shared.Models;

namespace Shared.Models.Tests
{
    public class ConnectionInfoTests
    {
        private readonly IPEndPoint _testEndpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5000);
        private readonly Segment _testSegment = new Segment(0, new SegmentFlags(SegmentFlags.FlagOptions.Syn), Array.Empty<byte>());

        #region Constructor Tests

        [Fact]
        public void Constructor_ValidEndpoint_InitializesCorrectly()
        {
            var endpoint = new IPEndPoint(IPAddress.Parse("192.168.1.1"), 8080);
            var connectionInfo = new ConnectionInfo(endpoint);

            Assert.Equal(endpoint, connectionInfo.Endpoint);
            Assert.Null(connectionInfo.LastSegmentReceived);
            Assert.Null(connectionInfo.LastSegmentSent);
            Assert.Equal(0, connectionInfo.LastSentTimes);
            Assert.Equal(0, connectionInfo.RetransmissionCount);
            Assert.Equal(ConnectionInfo.ConnectionState.Disconnected, connectionInfo.State);
        }

        [Fact]
        public void Constructor_NullEndpoint_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new ConnectionInfo(null));
        }

        #endregion

        #region BeginConnection Tests

        [Fact]
        public void BeginConnection_ValidSegment_UpdatesStateAndSegment()
        {
            var connectionInfo = new ConnectionInfo(_testEndpoint);
            var segment = new Segment(1, new SegmentFlags(SegmentFlags.FlagOptions.Syn), Array.Empty<byte>());

            connectionInfo.BeginConnection(segment);

            Assert.Equal(ConnectionInfo.ConnectionState.InitiatedConnection, connectionInfo.State);
            Assert.Equal(segment, connectionInfo.LastSegmentSent);
            Assert.NotEqual(0, connectionInfo.LastSentTimes);
        }

        [Fact]
        public void BeginConnection_NullSegment_ThrowsArgumentNullException()
        {
            var connectionInfo = new ConnectionInfo(_testEndpoint);
            Assert.Throws<ArgumentNullException>(() => connectionInfo.BeginConnection(null));
        }

        [Fact]
        public void BeginConnection_MultipleCallsFromDisconnected_LastCallWins()
        {
            var connectionInfo = new ConnectionInfo(_testEndpoint);
            var segment1 = new Segment(1, new SegmentFlags(SegmentFlags.FlagOptions.Syn), Array.Empty<byte>());
            var segment2 = new Segment(2, new SegmentFlags(SegmentFlags.FlagOptions.Syn), Array.Empty<byte>());

            connectionInfo.BeginConnection(segment1);
            long firstTime = connectionInfo.LastSentTimes;

            System.Threading.Thread.Sleep(10);
            connectionInfo.BeginConnection(segment2);

            Assert.Equal(segment2, connectionInfo.LastSegmentSent);
            Assert.True(connectionInfo.LastSentTimes >= firstTime);
        }

        #endregion

        #region UpdateConnectionInfo Tests

        [Fact]
        public void UpdateConnectionInfo_ValidSegmentReceived_UpdatesState()
        {
            var connectionInfo = new ConnectionInfo(_testEndpoint);
            var receivedSegment = new Segment(1, new SegmentFlags(SegmentFlags.FlagOptions.Ack), Array.Empty<byte>());
            var sentSegment = new Segment(2, new SegmentFlags(SegmentFlags.FlagOptions.Syn), Array.Empty<byte>());

            connectionInfo.UpdateConnectionInfo(ConnectionInfo.ConnectionState.Connected, receivedSegment, null);

            Assert.Equal(ConnectionInfo.ConnectionState.Connected, connectionInfo.State);
            Assert.Equal(receivedSegment, connectionInfo.LastSegmentReceived);
            Assert.Null(connectionInfo.LastSegmentSent);
        }

        [Fact]
        public void UpdateConnectionInfo_ValidSegmentSent_UpdatesState()
        {
            var connectionInfo = new ConnectionInfo(_testEndpoint);
            var sentSegment = new Segment(1, new SegmentFlags(SegmentFlags.FlagOptions.Syn), Array.Empty<byte>());

            connectionInfo.UpdateConnectionInfo(ConnectionInfo.ConnectionState.Connected, null, sentSegment);

            Assert.Equal(ConnectionInfo.ConnectionState.Connected, connectionInfo.State);
            Assert.Null(connectionInfo.LastSegmentReceived);
            Assert.Equal(sentSegment, connectionInfo.LastSegmentSent);
        }

        [Fact]
        public void UpdateConnectionInfo_BothSegmentsValid_UpdatesBoth()
        {
            var connectionInfo = new ConnectionInfo(_testEndpoint);
            var receivedSegment = new Segment(1, new SegmentFlags(SegmentFlags.FlagOptions.Ack), Array.Empty<byte>());
            var sentSegment = new Segment(2, new SegmentFlags(SegmentFlags.FlagOptions.Syn), Array.Empty<byte>());

            connectionInfo.UpdateConnectionInfo(ConnectionInfo.ConnectionState.Connected, receivedSegment, sentSegment);

            Assert.Equal(receivedSegment, connectionInfo.LastSegmentReceived);
            Assert.Equal(sentSegment, connectionInfo.LastSegmentSent);
        }

        [Fact]
        public void UpdateConnectionInfo_BothSegmentsNull_ThrowsArgumentException()
        {
            var connectionInfo = new ConnectionInfo(_testEndpoint);

            var exception = Assert.Throws<ArgumentException>(() =>
                connectionInfo.UpdateConnectionInfo(ConnectionInfo.ConnectionState.Connected, null, null));

            Assert.Contains("At least one of", exception.Message);
        }

        [Fact]
        public void UpdateConnectionInfo_UpdatesLastSentTimes()
        {
            var connectionInfo = new ConnectionInfo(_testEndpoint);
            var segment = new Segment(1, new SegmentFlags(SegmentFlags.FlagOptions.Ack), Array.Empty<byte>());
            long beforeTime = connectionInfo.LastSentTimes;

            System.Threading.Thread.Sleep(10);
            connectionInfo.UpdateConnectionInfo(ConnectionInfo.ConnectionState.Connected, segment, null);

            Assert.True(connectionInfo.LastSentTimes > beforeTime);
        }

        [Fact]
        public void UpdateConnectionInfo_VariousStates_AllSucceed()
        {
            var connectionInfo = new ConnectionInfo(_testEndpoint);
            var segment = new Segment(1, new SegmentFlags(SegmentFlags.FlagOptions.Ack), Array.Empty<byte>());
            var states = new[]
            {
                ConnectionInfo.ConnectionState.Connected,
                ConnectionInfo.ConnectionState.ClosingConnection,
                ConnectionInfo.ConnectionState.FailedConnection,
                ConnectionInfo.ConnectionState.ResetConnection
            };

            foreach (var state in states)
            {
                connectionInfo.UpdateConnectionInfo(state, segment, null);
                Assert.Equal(state, connectionInfo.State);
            }
        }

        #endregion

        #region ResetConnection Tests

        [Fact]
        public void ResetConnection_ValidSegment_UpdatesStateAndSegment()
        {
            var connectionInfo = new ConnectionInfo(_testEndpoint);
            var segment = new Segment(1, new SegmentFlags(SegmentFlags.FlagOptions.Rst), Array.Empty<byte>());

            connectionInfo.ResetConnection(segment);

            Assert.Equal(ConnectionInfo.ConnectionState.ResetConnection, connectionInfo.State);
            Assert.Equal(segment, connectionInfo.LastSegmentSent);
            Assert.NotEqual(0, connectionInfo.LastSentTimes);
        }

        [Fact]
        public void ResetConnection_NullSegment_ThrowsArgumentNullException()
        {
            var connectionInfo = new ConnectionInfo(_testEndpoint);
            Assert.Throws<ArgumentNullException>(() => connectionInfo.ResetConnection(null));
        }

        [Fact]
        public void ResetConnection_UpdatesLastSentTimes()
        {
            var connectionInfo = new ConnectionInfo(_testEndpoint);
            var segment = new Segment(1, new SegmentFlags(SegmentFlags.FlagOptions.Rst), Array.Empty<byte>());
            connectionInfo.BeginConnection(_testSegment);
            long beforeTime = connectionInfo.LastSentTimes;

            System.Threading.Thread.Sleep(10);
            connectionInfo.ResetConnection(segment);

            Assert.True(connectionInfo.LastSentTimes > beforeTime);
        }

        #endregion

        #region CloseConnection Tests

        [Fact]
        public void CloseConnection_ValidSegment_UpdatesStateAndSegment()
        {
            var connectionInfo = new ConnectionInfo(_testEndpoint);
            var segment = new Segment(1, new SegmentFlags(SegmentFlags.FlagOptions.Fin), Array.Empty<byte>());

            connectionInfo.CloseConnection(segment);

            Assert.Equal(ConnectionInfo.ConnectionState.ClosingConnection, connectionInfo.State);
            Assert.Equal(segment, connectionInfo.LastSegmentSent);
            Assert.NotEqual(0, connectionInfo.LastSentTimes);
        }

        [Fact]
        public void CloseConnection_NullSegment_ThrowsArgumentNullException()
        {
            var connectionInfo = new ConnectionInfo(_testEndpoint);
            Assert.Throws<ArgumentNullException>(() => connectionInfo.CloseConnection(null));
        }

        [Fact]
        public void CloseConnection_UpdatesLastSentTimes()
        {
            var connectionInfo = new ConnectionInfo(_testEndpoint);
            var segment = new Segment(1, new SegmentFlags(SegmentFlags.FlagOptions.Fin), Array.Empty<byte>());
            connectionInfo.BeginConnection(_testSegment);
            long beforeTime = connectionInfo.LastSentTimes;

            System.Threading.Thread.Sleep(10);
            connectionInfo.CloseConnection(segment);

            Assert.True(connectionInfo.LastSentTimes > beforeTime);
        }

        #endregion

        #region IncrementRetransmissionCount Tests

        [Fact]
        public void IncrementRetransmissionCount_StartsAtZero_IncrementedToOne()
        {
            var connectionInfo = new ConnectionInfo(_testEndpoint);
            Assert.Equal(0, connectionInfo.RetransmissionCount);

            connectionInfo.IncrementRetransmissionCount();

            Assert.Equal(1, connectionInfo.RetransmissionCount);
        }

        [Fact]
        public void IncrementRetransmissionCount_MultipleIncrements_CountIncreases()
        {
            var connectionInfo = new ConnectionInfo(_testEndpoint);

            for (int i = 1; i <= 5; i++)
            {
                connectionInfo.IncrementRetransmissionCount();
                Assert.Equal(i, connectionInfo.RetransmissionCount);
            }
        }

        [Fact]
        public void IncrementRetransmissionCount_CanExceedMaxRetransmissions()
        {
            var connectionInfo = new ConnectionInfo(_testEndpoint);

            for (int i = 0; i <= ConnectionInfo.MaxRetransmissions; i++)
            {
                connectionInfo.IncrementRetransmissionCount();
            }

            Assert.True(connectionInfo.RetransmissionCount > ConnectionInfo.MaxRetransmissions);
        }

        [Fact]
        public void IncrementRetransmissionCount_UpdatesLastSentTimes()
        {
            var connectionInfo = new ConnectionInfo(_testEndpoint);
            long beforeTime = connectionInfo.LastSentTimes;

            System.Threading.Thread.Sleep(10);
            connectionInfo.IncrementRetransmissionCount();

            Assert.True(connectionInfo.LastSentTimes > beforeTime);
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void CompleteConnectionLifecycle_ConnectionFlow()
        {
            var connectionInfo = new ConnectionInfo(_testEndpoint);
            var synSegment = new Segment(1, new SegmentFlags(SegmentFlags.FlagOptions.Syn), Array.Empty<byte>());
            var synAckSegment = new Segment(2, new SegmentFlags(SegmentFlags.FlagOptions.Ack), Array.Empty<byte>());
            var ackSegment = new Segment(3, new SegmentFlags(SegmentFlags.FlagOptions.Ack), Array.Empty<byte>());

            // Start connection
            connectionInfo.BeginConnection(synSegment);
            Assert.Equal(ConnectionInfo.ConnectionState.InitiatedConnection, connectionInfo.State);

            // Establish connection
            connectionInfo.UpdateConnectionInfo(ConnectionInfo.ConnectionState.Connected, synAckSegment, null);
            Assert.Equal(ConnectionInfo.ConnectionState.Connected, connectionInfo.State);
            Assert.Equal(synAckSegment, connectionInfo.LastSegmentReceived);

            // Close connection
            var finSegment = new Segment(4, new SegmentFlags(SegmentFlags.FlagOptions.Fin), Array.Empty<byte>());
            connectionInfo.CloseConnection(finSegment);
            Assert.Equal(ConnectionInfo.ConnectionState.ClosingConnection, connectionInfo.State);
        }

        [Fact]
        public void StateTransitions_AllValidTransitions()
        {
            var connectionInfo = new ConnectionInfo(_testEndpoint);
            var segment = new Segment(1, new SegmentFlags(SegmentFlags.FlagOptions.Ack), Array.Empty<byte>());

            var validTransitions = new[]
            {
                ConnectionInfo.ConnectionState.Disconnected,
                ConnectionInfo.ConnectionState.InitiatedConnection,
                ConnectionInfo.ConnectionState.Connected,
                ConnectionInfo.ConnectionState.ClosingConnection,
                ConnectionInfo.ConnectionState.Disconnected
            };

            foreach (var state in validTransitions)
            {
                if (state == ConnectionInfo.ConnectionState.Disconnected)
                {
                    connectionInfo = new ConnectionInfo(_testEndpoint);
                }
                else if (state == ConnectionInfo.ConnectionState.InitiatedConnection)
                {
                    connectionInfo.BeginConnection(segment);
                }
                else
                {
                    connectionInfo.UpdateConnectionInfo(state, segment, null);
                }

                Assert.Equal(state, connectionInfo.State);
            }
        }

        [Fact]
        public void MultipleEndpoints_IndependentInstances()
        {
            var endpoint1 = new IPEndPoint(IPAddress.Parse("192.168.1.1"), 8080);
            var endpoint2 = new IPEndPoint(IPAddress.Parse("192.168.1.2"), 8081);

            var connectionInfo1 = new ConnectionInfo(endpoint1);
            var connectionInfo2 = new ConnectionInfo(endpoint2);

            var segment1 = new Segment(1, new SegmentFlags(SegmentFlags.FlagOptions.Syn), Array.Empty<byte>());
            var segment2 = new Segment(2, new SegmentFlags(SegmentFlags.FlagOptions.Ack), Array.Empty<byte>());

            connectionInfo1.BeginConnection(segment1);
            connectionInfo2.BeginConnection(segment2);

            Assert.Equal(ConnectionInfo.ConnectionState.InitiatedConnection, connectionInfo1.State);
            Assert.Equal(ConnectionInfo.ConnectionState.InitiatedConnection, connectionInfo2.State);
            Assert.Equal(segment1, connectionInfo1.LastSegmentSent);
            Assert.Equal(segment2, connectionInfo2.LastSegmentSent);
        }

        #endregion

        #region MaxRetransmissions Constant Tests

        [Fact]
        public void MaxRetransmissions_ConstantValueIsValid()
        {
            Assert.Equal(5, ConnectionInfo.MaxRetransmissions);
        }

        #endregion
    }
}