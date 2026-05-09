using System;
using System.Collections.Generic;
using System.Text;
using Shared.Models;
using Shared.Logging;

namespace Shared.Protocol.TCP
{
    public class TcpSendBuffer
    {
        public readonly Dictionary<ushort, Segment> SegmentsToSend;

        public Segment LastSegmentSent { get; private set; }
        public bool ActiveRetransmission { get; private set; }

        private int _duplicateAckCount;
        private ushort _lastAckedSequenceNumber;

        public TcpSendBuffer()
        {
            SegmentsToSend = [];
            ActiveRetransmission = false;
            _duplicateAckCount = 0;
            _lastAckedSequenceNumber = 0;

            LastSegmentSent = new Segment(0, new SegmentFlags(SegmentFlags.FlagOptions.None), []);
        }

        public async Task AddSegmentToSendAsync(Segment segment)
        {
            if(SegmentsToSend.ContainsKey(segment.SequenceNumber))
            {
                Logger.Warning($"Segment with sequence number {segment.SequenceNumber} already exists in the send buffer.");
            }
            else
            {
                SegmentsToSend[segment.SequenceNumber] = segment;
                LastSegmentSent = segment;
            }
        }

        public void Acknowledge(Segment segment)
        {
            if (segment.SequenceNumber == _lastAckedSequenceNumber)
            {
                _duplicateAckCount++;
            }
            else
            {
                if (SegmentsToSend.ContainsKey(segment.SequenceNumber))
                {
                    SegmentsToSend.Remove(segment.SequenceNumber);
                    _lastAckedSequenceNumber = segment.SequenceNumber;
                    _duplicateAckCount = 0;
                }
                else
                {
                    Logger.Warning($"Acknowledged segment with sequence number {segment.SequenceNumber} not found in send buffer.");
                    Logger.Warning($"Retransmition activated.");
                    ActiveRetransmission = true;
                }
            }
            if(_duplicateAckCount >= 3)
            {
                Logger.Warning($"Received {_duplicateAckCount} duplicate ACKs for sequence number {_lastAckedSequenceNumber}.");
                Logger.Warning($"Retransmition activated.");
                ActiveRetransmission = true;
            }
        }
    }
}
