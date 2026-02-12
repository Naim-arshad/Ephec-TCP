using Shared.Models;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Shared.Protocol.TCP
{
    public class TcpRetransmissionHandler(UdpTransport udpTransport)
    {
        private ConcurrentDictionary<string, ConnectionInfo>? MapConnection;
        private readonly UdpTransport _udpTransport = udpTransport;

        private const int RetransmissionTimeout = 1000; // in milliseconds
        public const int MaxRetransmissions = 5;

        public async Task LoopRetransmissions(
            ConcurrentDictionary<string, ConnectionInfo> mapConnection,
            CancellationToken cancellationToken)
        {
            if(mapConnection == null)
            {

                throw new ArgumentNullException(nameof(mapConnection), 
                    "MapConnection cannot be null when starting the retransmission loop.");
            }
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(RetransmissionTimeout, cancellationToken);
                    MapConnection = mapConnection; //snapshot of the current state of connections

                    foreach (var connection in MapConnection)
                    {
                        foreach (var segment in connection.Value.GetSegmentsToRetransmit())
                        {
                            if (segment.RetransmissionCount < 5)
                            {
                                _ = _udpTransport.SendSegmentAsync(segment.Serialize(), connection.Value.Endpoint);
                                segment.IncrementRetransmissionCount();
                            }
                            else
                            {
                                //TODO: Implement logic for handling max retransmissions reached (e.g., close connection, log error, etc.)
                                Console.WriteLine($"Max retransmissions reached for segment with sequence number " +
                                    $"{segment.SequenceNumber} to {connection.Value.Endpoint}. Closing connection.");
                                MapConnection.TryRemove(connection.Key, out _);
                            }
                        }
                    }
                }
            }
            catch (TaskCanceledException) //TODO: Consider catching more specific exceptions if needed (e.g., IOException for network errors).
            {
                // Expected when cancellation is requested, no action needed.
            }
        }
    }
}
