using Shared.Logging;
using System.Net;
using System.Net.Sockets;

namespace Shared.Protocol.UDP
{
    public class UdpTransport : IAsyncDisposable
    {
        private const string LogSource = "udp.transport";
        private const int SioUdpConnReset = -1744830452;
        private const int SocketBufferSize = 4 * 1024 * 1024;

        private readonly UdpClient _udpClient;
        private readonly int _port;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _receiveLoopTask;
        private volatile bool _isRunning;
        private volatile bool _disposed;

        public event Action<byte[], IPEndPoint>? OnSegmentReceived;

        public UdpTransport(int port)
        {
            if (port < 0 || port > 65535)
                throw new ArgumentOutOfRangeException(nameof(port), "Port doit être entre 0 et 65535.");

            _udpClient = new UdpClient(port);
            _udpClient.Client.ReceiveBufferSize = SocketBufferSize;
            _udpClient.Client.SendBufferSize = SocketBufferSize;
            _port = port;

            try
            {
                // Ignore le paquet ICMP cache de Windows quand le port distant n'est pas ouvert.
                _udpClient.Client.IOControl(
                    (IOControlCode)SioUdpConnReset,
                    [0],
                    null);
            }
            catch (Exception ex)
            {
                Logger.Debug(LogSource, $"UDP ICMP reset option not applied. localPort={_port}, reason={ex.Message}");
            }

            _isRunning = false;
            _disposed = false;
            Logger.Info(LogSource, $"UDP transport initialized. localPort={_port}");
        }

        public void StartServer()
        {
            if (_isRunning)
            {
                Logger.Debug(LogSource, $"UDP receive loop already running. localPort={_port}");
                return;
            }

            _isRunning = true;
            _cancellationTokenSource = new CancellationTokenSource();
            _receiveLoopTask = UdpReceiveLoop(_cancellationTokenSource.Token);
            Logger.Info(LogSource, $"UDP receive loop started. localPort={_port}");
        }

        private async Task UdpReceiveLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var result = await _udpClient.ReceiveAsync(token);
                    Logger.Debug(
                        LogSource,
                        $"UDP packet received. localPort={_port}, remote={result.RemoteEndPoint}, bytes={result.Buffer.Length}");
                    OnSegmentReceived?.Invoke(result.Buffer, result.RemoteEndPoint);
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Debug(LogSource, $"UDP receive loop cancellation requested. localPort={_port}");
            }
            catch (SocketException ex)
            {
                Logger.Error(ex, LogSource, $"UDP receive loop socket error. localPort={_port}, socketError={ex.SocketErrorCode}");
            }
            catch (ObjectDisposedException)
            {
                Logger.Debug(LogSource, $"UDP socket disposed. localPort={_port}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, LogSource, $"UDP receive loop failed. localPort={_port}");
            }
            finally
            {
                _isRunning = false;
                Logger.Info(LogSource, $"UDP receive loop stopped. localPort={_port}");
            }
        }

        public async Task StopServer()
        {
            if (!_isRunning || _cancellationTokenSource == null)
            {
                Logger.Debug(LogSource, $"UDP receive loop already stopped. localPort={_port}");
                return;
            }

            try
            {
                _cancellationTokenSource.Cancel();
                Logger.Info(LogSource, $"Stopping UDP receive loop. localPort={_port}");
            }
            catch (ObjectDisposedException)
            {
                Logger.Debug(LogSource, $"UDP cancellation token was already disposed. localPort={_port}");
            }

            if (_receiveLoopTask != null)
            {
                try
                {
                    await _receiveLoopTask;
                }
                catch (OperationCanceledException)
                {
                    Logger.Debug(LogSource, $"UDP receive loop stopped by cancellation. localPort={_port}");
                }
            }

            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
            _isRunning = false;
        }

        public async Task SendSegmentAsync(byte[] data, IPEndPoint remoteIPEndPoint)
        {
            try
            {
                await _udpClient.SendAsync(data, data.Length, remoteIPEndPoint);
                Logger.Debug(LogSource, $"UDP packet sent. localPort={_port}, remote={remoteIPEndPoint}, bytes={data.Length}");
            }
            catch (SocketException ex)
            {
                Logger.Error(ex, LogSource, $"UDP send failed. localPort={_port}, remote={remoteIPEndPoint}");
            }
            catch (ObjectDisposedException)
            {
                Logger.Warning(LogSource, $"Cannot send UDP packet because transport is disposed. localPort={_port}, remote={remoteIPEndPoint}");
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                await StopServer();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, LogSource, $"Error while disposing UDP transport. localPort={_port}");
            }
            finally
            {
                _udpClient.Dispose();
                Logger.Info(LogSource, $"UDP transport disposed. localPort={_port}");

                GC.SuppressFinalize(this);
            }
        }
    }
}
