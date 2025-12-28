using Shared.Models;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Shared.Protocol
{
    public class TcpOverlayTransport
    {
        private readonly Dictionary<string, ConnectionInfo> _mapConnection;
        private readonly UdpTransport _udpTransport;

        public TcpOverlayTransport(int port)
        {
            _mapConnection = new Dictionary<string, ConnectionInfo>();
            _udpTransport = new UdpTransport(port);
            
            _udpTransport.OnSegmentReceived += OnSegmentReceived;
            _udpTransport.StartServer();
        }

        public void OnSegmentReceived(byte[] PacketBinary, IPEndPoint ip)
        {
            if(_mapConnection.ContainsKey(ip.ToString()))
            {
                Segment segment = Segment.Deserialize(PacketBinary);
                if(/*TODO*/true)
                {
                    ReceiverHandleSYN(segment, ip);
                }
            }
        }

        private void ReceiverHandleSYN(Segment segment, IPEndPoint ip)
        {
            //Implémenter la logique de gestion du SYN
        }

        public void TransmitterInitiateConnection(IPEndPoint ip)
        {
            //Implémenter la logique d'envoi du SYN
        }
    }
}
