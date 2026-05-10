using Shared.Logging;
using Shared.Models;
using Shared.Models.FileTransfer;
using Shared.Models.Segments;
using Shared.Protocol.Serialization;
using Shared.Protocol.UDP;
using Shared.Protocol.FileTransfer;
using System.Collections.Concurrent;
using System.Net;

namespace Shared.Protocol.TCP
{

    public class TcpConnection
    {
        private const string LogSource = "tcp.connection";
        private const int TransmissionWindowSize = 256;
        private const int SendWindowPollDelayMilliseconds = 10;

        private readonly static Random rand = new();

        private readonly ConcurrentDictionary<string, Connection> _mapConnection;
        private readonly UdpTransport _udpTransport;
        private readonly TcpRetransmissionHandler _retransmissionHandler;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly TimeSpan _timeout;
        private readonly string? _receivedFilePath;

        public TcpConnection(int port, int timeout, string? receivedFilePath = "received_file.bin")
        {
            _mapConnection = new ConcurrentDictionary<string, Connection>();
            _udpTransport = new UdpTransport(port);
            _receivedFilePath = receivedFilePath;
            
            _udpTransport.OnSegmentReceived += OnSegmentReceived;
            _timeout = TimeSpan.FromSeconds(timeout);
            _udpTransport.StartServer();

            _retransmissionHandler = new TcpRetransmissionHandler(_udpTransport, TransmissionWindowSize);
            _ = _retransmissionHandler.LoopRetransmissions(_mapConnection, _cancellationTokenSource.Token);
            Logger.Info(
                LogSource,
                $"TCP connection manager started. localPort={port}, timeoutSeconds={timeout}, receiveFile={receivedFilePath ?? "disabled"}");
        }

        public void OnSegmentReceived(byte[] PacketBinary, IPEndPoint ip)
        {
            Segment segment;
            byte[] packet;
            Logger.Debug(LogSource, $"Incoming packet. remote={ip}, bytes={PacketBinary.Length}");

            try
            {
                segment = SegmentParser.Deserialize(PacketBinary);
                Logger.Debug(LogSource, $"Incoming segment parsed. remote={ip}, {FormatSegment(segment)}");

                _mapConnection.TryGetValue(ip.ToString(), out Connection? connectionInfo);
                if (connectionInfo == null)
                {
                    connectionInfo = new(ip, _timeout, _receivedFilePath);
                    _mapConnection[ip.ToString()] = connectionInfo;
                    Logger.Info(LogSource, $"Connection context created. remote={ip}, state={connectionInfo.CurrentState}");
                }

                packet = TCPSegmentProcessor.Process(segment, connectionInfo, _mapConnection);

            } catch (Exception ex)
            {
                Logger.Error(ex, LogSource, $"Error while processing incoming segment. remote={ip}");
                return;
            }
            if(packet != null && packet.Length >= 5)
            {
                Logger.Debug(LogSource, $"Sending response packet. remote={ip}, bytes={packet.Length}");
                _ = _udpTransport.SendSegmentAsync(packet, ip);
            }
            else
            {
                Logger.Debug(LogSource, $"No response packet generated. remote={ip}");
            }
        }

        public async Task InitiatConnection(string ip)
        {
            Logger.Info(LogSource, $"Initiating connection. remote={ip}");

            IPEndPoint endPoint = IPEndPoint.Parse(ip);
            Connection connectionInfo = new(endPoint, _timeout);
            Segment segment = TCPSegmentBuilder.BuildSyn(connectionInfo);

            _mapConnection.TryAdd(endPoint.ToString(), connectionInfo);
            connectionInfo.UpdateConnectionInfo(Connection.State.SynSent, null, segment);
            Logger.Debug(LogSource, $"Sending SYN. remote={endPoint}, {FormatSegment(segment)}");

            _ = _udpTransport.SendSegmentAsync(
                SegmentSerializer.Serialize(segment), 
                endPoint);

            while(connectionInfo.CurrentState != Connection.State.Established)
            {
                //TODO
                await Task.Delay(100);
            }
            Logger.Info(LogSource, $"Connection established. remote={endPoint}");
        }

        public async Task SendFile(string ip, string filePath)
        {
            Logger.Info(LogSource, $"Preparing file transfer. remote={ip}, file={filePath}");
            using FileChunker fileChunker = new FileChunker(filePath, 1024);
            IPEndPoint endPoint = IPEndPoint.Parse(ip);
            Logger.Info(
                LogSource,
                $"Starting file transfer. remote={ip}, file={filePath}, bytes={fileChunker.TotalSize}, chunkSize=1024");

            if (!_mapConnection.TryGetValue(endPoint.ToString(), out Connection? connectionInfo) || 
                connectionInfo.CurrentState != Connection.State.Established)
            {
                Logger.Error(LogSource, $"No established connection found. remote={ip}, file={filePath}");
                return;
            }

            int sentChunkCount = 0;
            long sentByteCount = 0;
            FileChunk? fileChunk = fileChunker.GetNextChunk();
            while (fileChunk is not null) 
            {
                while (connectionInfo.SendBuffer.PendingSegmentCount >= TransmissionWindowSize)
                {
                    if (!_mapConnection.ContainsKey(endPoint.ToString()) ||
                        connectionInfo.CurrentState != Connection.State.Established)
                    {
                        Logger.Error(LogSource, $"Connection is no longer established. remote={ip}, file={filePath}, sentChunks={sentChunkCount}");
                        return;
                    }

                    Logger.Debug(
                        LogSource,
                        $"Send window is full. remote={ip}, pendingSegments={connectionInfo.SendBuffer.PendingSegmentCount}, limit={TransmissionWindowSize}");
                    await Task.Delay(SendWindowPollDelayMilliseconds);
                }

                Segment segment = TCPSegmentBuilder.BuildData(connectionInfo, fileChunk.Data);
                connectionInfo.UpdateConnectionInfo(Connection.State.Established, null, segment);
                Logger.Debug(
                    LogSource,
                    $"Sending file chunk. remote={ip}, chunkIndex={fileChunk.Index}, bytes={fileChunk.Data.Length}, {FormatSegment(segment)}");
                await _udpTransport.SendSegmentAsync(
                    SegmentSerializer.Serialize(segment),
                    connectionInfo.Endpoint);

                sentChunkCount++;
                sentByteCount += fileChunk.Data.Length;
                fileChunk = fileChunker.GetNextChunk();
            }

            if (!await WaitForPendingSegmentsAcknowledged(connectionInfo, ip, filePath, sentChunkCount))
            {
                return;
            }

            Logger.Info(
                LogSource,
                $"File transfer completed. remote={ip}, file={filePath}, chunks={sentChunkCount}, bytes={sentByteCount}");
        }

        public async Task StopConnection(string ip)
        {
            Logger.Info(LogSource, "Closing TCP connection.");

            EndPoint endPoint = IPEndPoint.Parse(ip);
            if(_mapConnection.TryGetValue(endPoint.ToString(), out Connection? connectionInfo) && 
                connectionInfo.CurrentState == Connection.State.Established)
            {
                Segment finSegment = TCPSegmentBuilder.BuildFin(connectionInfo);
                connectionInfo.UpdateConnectionInfo(Connection.State.FinWait1, null, finSegment);
                Logger.Debug(LogSource, $"Sending FIN. remote={ip}, {FormatSegment(finSegment)}");
                await _udpTransport.SendSegmentAsync(
                    SegmentSerializer.Serialize(finSegment),
                    connectionInfo.Endpoint);
            }
            _cancellationTokenSource.Cancel();
            await _udpTransport.StopServer();
        }

        private static string FormatSegment(Segment segment)
        {
            return $"sequence={segment.SequenceNumber}, flags={segment.Flags}, payloadBytes={segment.Data.Length}";
        }

        private async Task<bool> WaitForPendingSegmentsAcknowledged(
            Connection connectionInfo,
            string ip,
            string filePath,
            int sentChunkCount)
        {
            DateTime deadline = DateTime.UtcNow.Add(_timeout);
            bool waitLogged = false;

            while (connectionInfo.SendBuffer.PendingSegmentCount > 0)
            {
                if (!_mapConnection.ContainsKey(connectionInfo.Endpoint.ToString()) ||
                    connectionInfo.CurrentState != Connection.State.Established)
                {
                    Logger.Error(
                        LogSource,
                        $"Connection is no longer established while waiting for final ACKs. remote={ip}, file={filePath}, sentChunks={sentChunkCount}, pendingSegments={connectionInfo.SendBuffer.PendingSegmentCount}");
                    return false;
                }

                if (DateTime.UtcNow >= deadline)
                {
                    Logger.Error(
                        LogSource,
                        $"Timed out waiting for final ACKs. remote={ip}, file={filePath}, sentChunks={sentChunkCount}, pendingSegments={connectionInfo.SendBuffer.PendingSegmentCount}");
                    return false;
                }

                if (!waitLogged)
                {
                    Logger.Debug(
                        LogSource,
                        $"Waiting for final ACKs. remote={ip}, file={filePath}, pendingSegments={connectionInfo.SendBuffer.PendingSegmentCount}");
                    waitLogged = true;
                }

                await Task.Delay(SendWindowPollDelayMilliseconds);
            }

            return true;
        }
    }
}
