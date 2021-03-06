﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace zucchini_client.Network
{
    public class Connection
    {

        private IServerListener _server;
        private TcpClient _client;
        private NetworkStream _stream;

        public Connection(IPAddress ip, IServerListener server) {
            _server = server;
            try
            {
                _client = new TcpClient();
                _client.Connect(ip, 8080);
                _stream = _client.GetStream();

                _server.OnConnected();
                Read();
            }
            catch (Exception e){
                _server.OnErrorReceived(e.StackTrace);
            }
        }

        /*
        *  Server methods
        */
    
        public void Send(JObject json)
        {
            new Thread(() => {

                try
                {
                    byte[] bytesToSend = ASCIIEncoding.ASCII.GetBytes(json.ToString());
                    _stream.Write(bytesToSend, 0, bytesToSend.Length);
                }
                catch (Exception e) {
                    Debug.WriteLine(e.StackTrace);
                }
            }).Start();
        }

        public void Read() {
            new Thread(() => {
                while (true)
                {
                    byte[] bytesToRead = new byte[_client.ReceiveBufferSize];
                    int bytesRead = _stream.Read(bytesToRead, 0, _client.ReceiveBufferSize);

                    _server.OnDataReceived(JObject.Parse(Encoding.ASCII.GetString(bytesToRead, 0, bytesRead)));

                }
            }).Start();
        }
       
    }
}
