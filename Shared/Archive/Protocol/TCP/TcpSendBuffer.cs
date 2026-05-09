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
        public int RetransmissionCount { get; private set; }

        private int _duplicateAckCount;
        private ushort _lastAckedSequenceNumber;

        public TcpSendBuffer()
        {
            SegmentsToSend = [];
            ActiveRetransmission = false;
            RetransmissionCount = 0;
            _duplicateAckCount = 0;
            _lastAckedSequenceNumber = 0;

            LastSegmentSent = new Segment(0, new SegmentFlags(SegmentFlags.FlagOptions.None), []);
        }

        public async Task AddSegmentToSendAsync(Segment segment)
        {
            if (SegmentsToSend.ContainsKey(segment.SequenceNumber))
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
            var ackedSequenceNumber = segment.GetDataToSequence();
            if (ackedSequenceNumber == _lastAckedSequenceNumber)
            {
                _duplicateAckCount++;
            }
            else
            {
                if (SegmentsToSend.ContainsKey(ackedSequenceNumber))
                {
                    SegmentsToSend.Remove(ackedSequenceNumber);
                    _lastAckedSequenceNumber = ackedSequenceNumber;
                    _duplicateAckCount = 0;
                }
                else
                {
                    Logger.Warning($"Acknowledged segment with sequence number {ackedSequenceNumber} not found in send buffer.");
                    Logger.Warning($"Retransmition activated.");
                    ActiveRetransmission = true;
                }
            }
            if (_duplicateAckCount >= 3)
            {
                Logger.Warning($"Received {_duplicateAckCount} duplicate ACKs for sequence number {_lastAckedSequenceNumber}.");
                Logger.Warning($"Retransmition activated.");
                ActiveRetransmission = true;
            }
        }

        public void AddRetransmission()
        {
            RetransmissionCount++;
            Logger.Warning($"Retransmission count increased to {RetransmissionCount} for segment with sequence number {LastSegmentSent.SequenceNumber}.");
            if(RetransmissionCount >= 5)
            {
                Logger.Critical($"Retransmission count reached {RetransmissionCount} for segment with sequence number {LastSegmentSent.SequenceNumber}. Connection might be lost.");
            }
        }
        public List<Segment> GetSegmentsToRetransmit()
        {
            return [.. SegmentsToSend.Values];
        }
    }
}
