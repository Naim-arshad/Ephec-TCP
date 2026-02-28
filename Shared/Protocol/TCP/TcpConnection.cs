using System;
using System.Collections.Concurrent;
using System.Net;
using System.IO;
using System.Threading.Tasks;
using System.Buffers.Binary;
using Shared.Models;
using Shared.Protocol.Files;

namespace Shared.Protocol.TCP
{
    public class TcpConnection
    {
        private readonly ConcurrentDictionary<string, ConnectionInfo> _mapConnection = new();
        private readonly UdpTransport _udpTransport;
        private readonly static Random _rand = new();
        private string _defaultTargetFileName = "output.dat";

        public TcpConnection(int port)
        {
            _udpTransport = new UdpTransport(port);
            _udpTransport.OnSegmentReceived += OnSegmentReceived;
            _udpTransport.StartServer();
        }

        public static ushort RandomSequenceNumber() => (ushort)_rand.Next(1000, 50000);

        public async Task SendFileAsync(IPEndPoint target, string filePath)
        {
            if (!File.Exists(filePath)) return;
            var connection = _mapConnection.GetOrAdd(target.ToString(), _ => new ConnectionInfo(target));

            // --- 1. OUVERTURE (SYN) ---
            ushort x = RandomSequenceNumber();
            connection.LocalISN = x;
            connection.LocalNextSeq = (ushort)(x + 1);

            Console.WriteLine("\n[TX] --- DÉBUT DU HANDSHAKE ---");
            Console.WriteLine($"[TX] SYN envoyé (seq={x})");

            connection.UpdateConnectionInfo(ConnectionInfo.State.SynSent, null, null);
            connection.AckSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            await _udpTransport.SendSegmentAsync(new Segment(x, new SegmentFlags(SegmentFlags.FlagOptions.Syn), Array.Empty<byte>()).Serialize(), target);

            if (!await WaitForSignal(connection, 5000)) return;

            // --- 2. TRANSFERT DE DONNÉES ---
            Console.WriteLine($"[ÉTAT] Connexion ÉTABLIE. Envoi de : {filePath}");
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            byte[] buffer = new byte[1024];
            int bytesRead;
            ushort currentSeq = connection.LocalNextSeq;

            while ((bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                byte[] chunk = new byte[bytesRead];
                Array.Copy(buffer, chunk, bytesRead);
                Segment s = new Segment(currentSeq, new SegmentFlags(SegmentFlags.FlagOptions.None), chunk);

                connection.AckSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                connection.UpdateConnectionInfo(ConnectionInfo.State.WaitingAck, null, s);

                await _udpTransport.SendSegmentAsync(s.Serialize(), target);
                Console.WriteLine($"[TX] Segment {currentSeq} envoyé. Attente ACK...");

                if (await WaitForSignal(connection, 5000))
                {
                    Console.WriteLine($"[RX] ACK reçu pour segment {currentSeq}.");
                    currentSeq++;
                    connection.LocalNextSeq = currentSeq;
                }
            }

            Console.WriteLine("\n[SUCCÈS] Transfert terminé.");
        }

        private async void OnSegmentReceived(byte[] data, IPEndPoint ip)
        {
            Segment segment;
            try { segment = Segment.Deserialize(data); } catch { return; }
            var connection = _mapConnection.GetOrAdd(ip.ToString(), _ => new ConnectionInfo(ip));
            byte flags = segment.Flags.Value;

            // CAS : SYN (Serveur reçoit x)
            if (SegmentFlags.IsFlagSet(flags, SegmentFlags.FlagOptions.Syn) && !SegmentFlags.IsFlagSet(flags, SegmentFlags.FlagOptions.Ack))
            {
                Console.WriteLine($"[RX] SYN reçu (seq={segment.SequenceNumber})");
                Segment synAck = TcpFlagHandler.HandleSYN(segment, ip, _mapConnection);
                await _udpTransport.SendSegmentAsync(synAck.Serialize(), ip);
                Console.WriteLine($"[TX] SYN-ACK envoyé (seq={synAck.SequenceNumber}, ack={segment.SequenceNumber + 1})");
            }
            // CAS : SYN+ACK (Client reçoit y et x+1)
            else if (SegmentFlags.IsFlagSet(flags, SegmentFlags.FlagOptions.Syn) && SegmentFlags.IsFlagSet(flags, SegmentFlags.FlagOptions.Ack))
            {
                ushort y = segment.SequenceNumber;
                Console.WriteLine($"[RX] SYN-ACK reçu (seq={y})");
                connection.RemoteNextSeq = (ushort)(y + 1);
                connection.UpdateState(ConnectionInfo.State.Established);

                // Point 4 Schéma : ACK final(seq=x+1, ack=y+1)
                byte[] ackPayload = new byte[2];
                BinaryPrimitives.WriteUInt16BigEndian(ackPayload, (ushort)(y + 1));
                Segment finalAck = new Segment(connection.LocalNextSeq, new SegmentFlags(SegmentFlags.FlagOptions.Ack), ackPayload);
                await _udpTransport.SendSegmentAsync(finalAck.Serialize(), ip);

                Console.WriteLine($"[TX] ACK final envoyé (seq={connection.LocalNextSeq}, ack={y + 1})");
                connection.AckSignal?.TrySetResult(true);
            }
            // CAS : DATA (Réception de contenu)
            else if (flags == (byte)SegmentFlags.FlagOptions.None)
            {
                Console.WriteLine($"[RX] DATA reçu (seq={segment.SequenceNumber}). Écriture...");
                FileSegment.WriteToFile(connection.TargetFileName ?? _defaultTargetFileName, segment, (ushort)(connection.RemoteNextSeq - 1));

                Segment dataAck = new Segment(segment.SequenceNumber, new SegmentFlags(SegmentFlags.FlagOptions.Ack), Array.Empty<byte>());
                await _udpTransport.SendSegmentAsync(dataAck.Serialize(), ip);
                Console.WriteLine($"[TX] ACK envoyé pour segment {segment.SequenceNumber}");
            }
            // CAS : ACK (Acquittement reçu)
            else if (SegmentFlags.IsFlagSet(flags, SegmentFlags.FlagOptions.Ack))
            {
                // Point 5 Schéma : On reçoit l'ACK final du client (Transition SynReceived -> Established)
                if (connection.CurrentState == ConnectionInfo.State.SynReceived)
                {
                    ushort ackVal = 0;
                    if (segment.Data != null && segment.Data.Length >= 2)
                        ackVal = BinaryPrimitives.ReadUInt16BigEndian(segment.Data);

                    Console.WriteLine($"[RX] ACK final reçu (seq={segment.SequenceNumber}, ack={ackVal})");
                    connection.UpdateState(ConnectionInfo.State.Established);
                    Console.WriteLine("[ÉTAT] Connexion ÉTABLIE avec le client.");
                }

                connection.MarkSegmentAsAcknowledged(segment.SequenceNumber);
                connection.AckSignal?.TrySetResult(true);
            }
        }

        private async Task<bool> WaitForSignal(ConnectionInfo c, int ms) => await Task.WhenAny(c.AckSignal!.Task, Task.Delay(ms)) == c.AckSignal.Task;
        public void SetTargetFileName(IPEndPoint ip, string name) => _defaultTargetFileName = name;
    }
}