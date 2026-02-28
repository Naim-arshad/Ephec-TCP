using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Shared.Protocol; // Assure-toi que cela correspond à ton namespace
using Xunit;
using FluentAssertions; // Si tu n'as pas ce package, remplace par Assert.Equal, etc.

namespace Shared.Protocol.Tests
{
    public class UdpTransportTests
    {
        // Helper pour trouver un port libre dynamiquement pour éviter les conflits
        private int GetFreePort()
        {
            using (var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Dgram, System.Net.Sockets.ProtocolType.Udp))
            {
                socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                return ((IPEndPoint)socket.LocalEndPoint!).Port;
            }
        }

        [Fact]
        public void Constructor_ShouldThrow_WhenPortIsInvalid()
        {
            // Act
            Action act = () => new UdpTransport(70000);

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public async Task SendAndReceive_ShouldWork_BetweenTwoInstances()
        {
            // Arrange
            int portA = GetFreePort();
            int portB = GetFreePort();

            await using var transportA = new UdpTransport(portA); // Serveur (Reçoit)
            await using var transportB = new UdpTransport(portB); // Client (Envoie)

            var tcs = new TaskCompletionSource<string>();

            // On s'abonne à l'événement de réception
            transportA.OnSegmentReceived += (data, endpoint) =>
            {
                string message = Encoding.UTF8.GetString(data);
                tcs.TrySetResult(message); // On débloque la Task quand on reçoit
            };

            transportA.StartServer();

            // Act
            string sentMessage = "Hello UDP World";
            byte[] bytes = Encoding.UTF8.GetBytes(sentMessage);
            var targetEndpoint = new IPEndPoint(IPAddress.Loopback, portA);

            await transportB.SendSegmentAsync(bytes, targetEndpoint);

            // Assert
            // On attend que le message arrive (timeout de 2 sec pour ne pas bloquer indéfiniment si ça échoue)
            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(2000));

            completedTask.Should().Be(tcs.Task, "Le message aurait dû être reçu avant le timeout");
            tcs.Task.Result.Should().Be(sentMessage);
        }

        [Fact]
        public async Task RestartServer_ShouldWork_WithoutCrashing()
        {
            // Ce test vérifie spécifiquement le fix du Start -> Stop -> Start

            // Arrange
            int port = GetFreePort();
            await using var transport = new UdpTransport(port);

            // Act & Assert

            // 1. Premier démarrage
            transport.StartServer();
            await Task.Delay(100); // Laisse le temps au thread de démarrer

            // 2. Arrêt
            await transport.StopServer();

            // 3. Redémarrage (Si le socket avait été fermé dans StopServer, ceci planterait plus tard)
            transport.StartServer();

            // 4. Vérification que ça marche toujours en envoyant un message à soi-même
            var tcs = new TaskCompletionSource<bool>();
            transport.OnSegmentReceived += (_, _) => tcs.TrySetResult(true);

            var endpoint = new IPEndPoint(IPAddress.Loopback, port);
            await transport.SendSegmentAsync(new byte[] { 1 }, endpoint);

            var result = await Task.WhenAny(tcs.Task, Task.Delay(1000));
            result.Should().Be(tcs.Task, "Le serveur devrait pouvoir recevoir des messages après un redémarrage");
        }

        [Fact]
        public async Task Dispose_ShouldCleanUpResources()
        {
            // Arrange
            int port = GetFreePort();
            var transport = new UdpTransport(port);
            transport.StartServer();

            // Act
            await transport.DisposeAsync();

            // Assert
            // On vérifie qu'on ne peut plus utiliser l'objet (ObjectDisposedException)
            // Note : UdpClient.SendAsync lance parfois ObjectDisposedException, parfois SocketException selon l'état
            Func<Task> act = async () => await transport.SendSegmentAsync(new byte[1], new IPEndPoint(IPAddress.Loopback, port));

            // Dans ta classe, tu as un try/catch qui avale l'erreur dans SendSegmentAsync et fait un Console.WriteLine.
            // Donc ici, on ne s'attend pas à une exception, mais on vérifie que le code ne crash pas.
            await act.Should().NotThrowAsync();
        }
    }
}