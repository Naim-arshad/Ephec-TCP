using Shared.Logging;
using Shared.Models;
using System.Collections.Concurrent;

namespace Shared.Protocol.TCP
{
    public class TcpRetransmissionHandler(UdpTransport udpTransport)
    {
        //TODO : monter les info dans TCPConnection pour stantardiser les timesout
        private readonly UdpTransport _udpTransport = udpTransport;
        private readonly SemaphoreSlim _semaphore = new(5);
        private const int RetransmissionTimeout = 5000; // in milliseconds

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
                        if(connection.Value.SendBuffer.ActiveRetransmission)
                        {
                            foreach (var segment in connection.Value.GetSegmentsToRetransmit())
                            {
                                if (segment.RetransmissionCount < MaxRetransmissions && connection.Value.ActiveRetransmission)
                                {
                                    _ = Task.Run(async () =>
                                    {
                                        try
                                        {
                                            Logger.Debug($"Retransmitting segment with sequence number {segment.SequenceNumber} " +
                                                $"to {connection.Value.Endpoint}. Retransmission count: {segment.RetransmissionCount + 1}");
                                            await _semaphore.WaitAsync(cancellationToken);
                                            await _udpTransport.SendSegmentAsync(segment.Serialize(), connection.Value.Endpoint);
                                        }
                                        catch (Exception ex) //TODO: Consider catching more specific exceptions if needed (e.g., IOException for network errors).
                                        {
                                            // Log the exception or handle it as needed.
                                            Console.WriteLine($"Error retransmitting segment with sequence number " +
                                                $"{segment.SequenceNumber} to {connection.Value.Endpoint}: {ex.Message}");
                                        }
                                        finally
                                        {
                                            _semaphore.Release();
                                        }
                                    });
                                }
                                else
                                {
                                    //TODO: Implement logic for handling max retransmissions reached (e.g., close connection, log error, etc.)
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
