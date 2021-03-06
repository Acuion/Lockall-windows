﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Lockall_Windows.Comm
{
    class TcpClientListener : ClientListener
    {
        readonly TcpListener _listener;

        private int ListensAtPort => ((IPEndPoint)_listener.LocalEndpoint).Port;
        private byte[] ListensAtIp { get; }

        public TcpClientListener()
        {
            IPAddress[] localIPs = Dns.GetHostAddresses(Dns.GetHostName());
            foreach (IPAddress addr in localIPs)
            {
                if (addr.AddressFamily == AddressFamily.InterNetwork)
                {
                    ListensAtIp = addr.GetAddressBytes();
                    break;
                }
            }

            _listener = new TcpListener(IPAddress.Any, 0);
            _listener.Start();
        }

        public override List<byte> ComputeHeader()
        {
            List<byte> result = new List<byte> {1};
            // wifi
            result.AddRange(ListensAtIp);
            result.AddRange(BitConverter.GetBytes(ListensAtPort));
            return result;
        }

        public override async Task<Stream> GetStream()
        {
            var client = await _listener.AcceptTcpClientAsync();
            return client.GetStream();
        }

        public override void Dispose()
        {
            _listener.Stop();
        }
    }
}
