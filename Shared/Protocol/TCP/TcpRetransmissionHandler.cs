using Shared.Logging;
using Shared.Models;
using Shared.Protocol.Serialization;
using Shared.Protocol.UDP;
using System.Collections.Concurrent;

namespace Shared.Protocol.TCP
{
    public class TcpRetransmissionHandler(UdpTransport udpTransport, int fullWindowThreshold = 256)
    {
        private const string LogSource = "tcp.retransmission";

        //TODO : monter les info dans TCPConnection pour stantardiser les timesout
        private readonly UdpTransport _udpTransport = udpTransport;
        private readonly int _fullWindowThreshold = fullWindowThreshold;
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
            Logger.Info(LogSource, $"Retransmission loop started. intervalMs={RetransmissionTimeout}, maxRetransmissions={MaxRetransmissions}, fullWindowThreshold={_fullWindowThreshold}");
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(RetransmissionTimeout, cancellationToken);

                    foreach (var connection in mapConnection)
                    {
                        if (ShouldRetransmit(connection.Value))
                        {
                            int maxSegmentsToRetransmit =
                                connection.Value.SendBuffer.PendingSegmentCount >= _fullWindowThreshold
                                    ? _fullWindowThreshold
                                    : 1;

                            bool retransmittedAnySegment = false;
                            foreach (var segment in connection.Value.GetSegmentsToRetransmit(maxSegmentsToRetransmit))
                            {
                                if (connection.Value.RetransmissionCount < MaxRetransmissions)
                                {
                                    bool semaphoreAcquired = false;
                                    try
                                    {
                                        Logger.Debug(
                                            LogSource,
                                            $"Retransmitting segment. remote={connection.Value.Endpoint}, sequence={segment.SequenceNumber}, attempt={connection.Value.RetransmissionCount + 1}");
                                        await _semaphore.WaitAsync(cancellationToken);
                                        semaphoreAcquired = true;
                                        await _udpTransport.SendSegmentAsync(SegmentSerializer.Serialize(segment), connection.Value.Endpoint);
                                        retransmittedAnySegment = true;
                                    }
                                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                                    {
                                        throw;
                                    }
                                    catch (Exception ex) //TODO: Consider catching more specific exceptions if needed (e.g., IOException for network errors).
                                    {
                                        Logger.Error(ex, LogSource, $"Retransmission failed. remote={connection.Value.Endpoint}, sequence={segment.SequenceNumber}");
                                    }
                                    finally
                                    {
                                        if (semaphoreAcquired)
                                            _semaphore.Release();
                                    }
                                }
                                else
                                {
                                    Logger.Error(
                                        LogSource,
                                        $"Max retransmissions reached. remote={connection.Value.Endpoint}, sequence={segment.SequenceNumber}, max={MaxRetransmissions}, action=close-connection");
                                    mapConnection.TryRemove(connection.Key, out _);
                                    break; // Exit the loop for this connection since it's being removed.
                                }
                            }

                            if (retransmittedAnySegment)
                                connection.Value.IncrementRetransmissionCount();
                        }
                        //TODO désactivé la retransmission active après un certain nombre de retransmissions ou après un certain temps d'inactivité pour éviter les boucles infinies de retransmissions.
                    }
                }
            }
            catch (TaskCanceledException) //TODO: Consider catching more specific exceptions if needed (e.g., IOException for network errors).
            {
                Logger.Debug(LogSource, "Retransmission loop cancelled.");
            }
            finally
            {
                Logger.Info(LogSource, "Retransmission loop stopped.");
            }
        } 

        private bool ShouldRetransmit(Connection connection)
        {
            return connection.ActiveRetransmission ||
                connection.SendBuffer.PendingSegmentCount >= _fullWindowThreshold;
        }
    }
}
