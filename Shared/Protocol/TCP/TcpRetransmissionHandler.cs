using Shared.Logging;
using Shared.Models;
using Shared.Protocol.Serialization;
using Shared.Protocol.UDP;
using System.Collections.Concurrent;

namespace Shared.Protocol.TCP
{
    public class TcpRetransmissionHandler(UdpTransport udpTransport)
    {
        //TODO : monter les info dans TCPConnection pour stantardiser les timesout
        private readonly UdpTransport _udpTransport = udpTransport;
        private readonly SemaphoreSlim _semaphore = new(5);
        private const int RetransmissionTimeout = 5000; // in milliseconds
        private const int MaxRetransmissions = 5; // Maximum number of retransmissions before giving up on a segment.

        public async Task LoopRetransmissions(
            ConcurrentDictionary<string, Connection> mapConnection,
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

                    foreach (var connection in mapConnection)
                    {
                        if(connection.Value.ActiveRetransmission)
                        {
                            foreach (var segment in connection.Value.GetSegmentsToRetransmit())
                            {
                                if (connection.Value.RetransmissionCount < MaxRetransmissions && connection.Value.ActiveRetransmission)
                                {
                                    bool semaphoreAcquired = false;
                                    try
                                    {
                                        Logger.Debug($"Retransmitting segment with sequence number {segment.SequenceNumber} " +
                                            $"to {connection.Value.Endpoint}. Retransmission count: {connection.Value.RetransmissionCount + 1}");
                                        await _semaphore.WaitAsync(cancellationToken);
                                        semaphoreAcquired = true;
                                        await _udpTransport.SendSegmentAsync(SegmentSerializer.Serialize(segment), connection.Value.Endpoint);
                                        connection.Value.IncrementRetransmissionCount();
                                    }
                                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                                    {
                                        throw;
                                    }
                                    catch (Exception ex) //TODO: Consider catching more specific exceptions if needed (e.g., IOException for network errors).
                                    {
                                        // Log the exception or handle it as needed.
                                        Console.WriteLine($"Error retransmitting segment with sequence number " +
                                            $"{segment.SequenceNumber} to {connection.Value.Endpoint}: {ex.Message}");
                                    }
                                    finally
                                    {
                                        if (semaphoreAcquired)
                                            _semaphore.Release();
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"Max retransmissions reached for segment with sequence number " +
                                        $"{segment.SequenceNumber} to {connection.Value.Endpoint}. Closing connection.");
                                    mapConnection.TryRemove(connection.Key, out _);
                                    break; // Exit the loop for this connection since it's being removed.
                                }
                            }
                        }
                        //TODO désactivé la retransmission active après un certain nombre de retransmissions ou après un certain temps d'inactivité pour éviter les boucles infinies de retransmissions.
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
