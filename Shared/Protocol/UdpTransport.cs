using System.Net;
using System.Net.Sockets;

namespace Shared.Protocol
{
    public class UdpTransport : IAsyncDisposable
    {
        private readonly UdpClient _udpClient;
        const int SIO_UDP_CONNRESET = -1744830452;
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

            try
            {
                //Pour ignorer le packet caché de windows ICMP si le port n'est pas ouvert
                _udpClient.Client.IOControl(
                    (IOControlCode)SIO_UDP_CONNRESET, 
                    new byte[] { 0 }, 
                    null); 
            } catch
            {
            }
            _isRunning = false;
            _disposed = false;
        }

        public void StartServer()
        {
            if (_isRunning) return; // Le serveur est déjà en cours d'exécution

            _isRunning = true;
            _cancellationTokenSource = new CancellationTokenSource();
            _receiveLoopTask = UdpReceiveLoop(_cancellationTokenSource.Token);
        }

        private async Task UdpReceiveLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested) //Vérifie si l'annulation a été demandée
                {
                    var result = await _udpClient.ReceiveAsync(token);
                    OnSegmentReceived?.Invoke(result.Buffer, result.RemoteEndPoint);
                }
            }
            catch (OperationCanceledException)
            {
                // Arrêt normal
            }
            catch (SocketException ex)
            {
                // Gestion des erreurs réseaux socket
                Console.WriteLine($"Socket Error: {ex.SocketErrorCode}");
            }
            catch (ObjectDisposedException)
            {
                // Le socket a été fermé brutalement
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UdpReceiveLoop: {ex.Message}");

            }
            finally
            {
                _isRunning = false;
            }
        }

        public async Task StopServer()
        {
            if (!_isRunning || _cancellationTokenSource == null) return; // Le serveur n'est pas en cours d'exécution

            try
            {
                _cancellationTokenSource.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Sécurité au cas où CTS serait déjà disposé
            }

            if (_receiveLoopTask != null)
            {
                try
                {
                    await _receiveLoopTask;
                }
                catch (OperationCanceledException)
                {
                    // Attendu lors de l'annulation
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
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Send Error: {ex.Message}");
            }
            catch (ObjectDisposedException)
            {
                Console.WriteLine("Cannot send data: UdpClient has been disposed.");
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
                Console.WriteLine($"Erreur lors de l'arrêt du serveur dans DisposeAsync : {ex.Message}");
            }
            finally
            {
                _udpClient.Dispose();

                GC.SuppressFinalize(this);
            }
        }
    }
}
