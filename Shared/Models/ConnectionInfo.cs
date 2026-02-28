using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using System.Linq;

namespace Shared.Models
{
    public class ConnectionInfo
    {
        public readonly IPEndPoint Endpoint;
        private readonly object _sync = new();
        public State CurrentState { get; private set; }
        public string? TargetFileName { get; set; }
        public TaskCompletionSource<bool>? AckSignal { get; set; }

        // Variables pour le schéma EPHEC (Séquences x et y)
        public ushort LocalISN { get; set; }
        public ushort LocalNextSeq { get; set; }
        public ushort RemoteNextSeq { get; set; }

        private readonly Dictionary<ushort, Segment> _mapSegmentToRetransmit = new();

        public enum State { Closed, Listen, SynSent, SynReceived, Established, WaitingAck, FinWait1, ClosedFinal }

        public ConnectionInfo(IPEndPoint endpoint)
        {
            Endpoint = endpoint;
            CurrentState = State.Closed;
        }

        public void UpdateState(State newState)
        {
            lock (_sync) { CurrentState = newState; }
        }

        // --- LA MÉTHODE À 3 ARGUMENTS ---
        public void UpdateConnectionInfo(State newState, Segment? lastReceived, Segment? lastSent)
        {
            lock (_sync)
            {
                CurrentState = newState;
                // On stocke le segment envoyé pour la retransmission
                if (lastSent != null)
                    _mapSegmentToRetransmit[lastSent.SequenceNumber] = lastSent;
            }
        }

        public void MarkSegmentAsAcknowledged(ushort seq)
        {
            lock (_sync) _mapSegmentToRetransmit.Remove(seq);
        }

        public IEnumerable<Segment> GetSegmentsToRetransmit()
        {
            lock (_sync) return _mapSegmentToRetransmit.Values.ToList();
        }
    }
}