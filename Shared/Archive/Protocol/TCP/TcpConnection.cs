using Shared.Logging;
using Shared.Models;
using System;
using System.Collections.Concurrent;
using System.Net;

namespace Shared.Protocol.TCP
{

    public class TcpConnection
    {
        private readonly static Random rand = new();

        private readonly ConcurrentDictionary<string, Connection> _mapConnection;
        private readonly UdpTransport _udpTransport;
        private readonly TcpRetransmissionHandler _retransmissionHandler;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly TimeSpan _timeout;

        public TcpConnection(int port, int timeout)
        {
            _mapConnection = new ConcurrentDictionary<string, Connection>();
            _udpTransport = new UdpTransport(port);
            
            _udpTransport.OnSegmentReceived += OnSegmentReceived;
            _udpTransport.StartServer();

            _retransmissionHandler = new TcpRetransmissionHandler(_udpTransport);
            _ = _retransmissionHandler.LoopRetransmissions(_mapConnection, _cancellationTokenSource.Token);

            _timeout = TimeSpan.FromSeconds(timeout);
        }

        public void OnSegmentReceived(byte[] PacketBinary, IPEndPoint ip)
        {
            Logger.Info($"Incoming Packet from IP:{ip}");

            _mapConnection.TryGetValue(ip.ToString(), out Connection? connectionInfo);
            if (connectionInfo == null)
            {
                connectionInfo = new(ip, _timeout);
                _mapConnection[ip.ToString()] = connectionInfo;
            }
            byte[] packet = TCPSegmentProcessor.Process(Segment.Deserialize(PacketBinary), connectionInfo, _mapConnection);
            if(packet != null && packet.Length >= 5)
                _ = _udpTransport.SendSegmentAsync(packet, ip);
        }

        public async Task InitiatConnection(string ip)
        {
            //TODO implémenter la logie d'initiation de connection TCP (handshake, etc.)
            Logger.Info("Initiating connection to IP:" + ip);

            IPEndPoint endPoint = IPEndPoint.Parse(ip);
            Connection connectionInfo = new(endPoint, _timeout);
            Segment segment = TCPSegmentBuilder.BuildSyn(connectionInfo);

            _mapConnection.TryAdd(endPoint.ToString(), connectionInfo);
            connectionInfo.BeginConnection(segment);

            _ = _udpTransport.SendSegmentAsync(
                segment.Serialize(), 
                endPoint);
        }

        public static ushort RandomSequenceNumber(ushort excludeNumber)
        {
            ushort number = (ushort)rand.Next(0, ushort.MaxValue);
            if (number != excludeNumber && (number + 2) <= ushort.MaxValue)
                number += 2;
            else
                number = 0;
            return number;
        }
    }
}
