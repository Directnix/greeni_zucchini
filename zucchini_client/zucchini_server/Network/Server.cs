﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace zucchini_server.Network
{
    class Server : IPlayerListener
    {
        //todo add list of rooms

        private TcpListener _server;

        public static bool RUNNING = true;
        public static int ROOM_SIZE = 4;

        public List<Player> Players { get; set; } = new List<Player>();
        public List<Room> Rooms { get; set; } = new List<Room>();

        private Api _api;

        private static Server _instance;

        public static Server Get()
        {
            if (_instance == null)
                _instance = new Server();
            return _instance;
        }

        private Server() {
            _api = new Api();

            _server = new TcpListener(GetLocalIPAddress(), 8080);
            _server.Start();

            Program.Print(PrintType.SUCC, $" Server started on ip: {GetLocalIPAddress().ToString()}");
            ReceivePlayers();
        }

        private void ReceivePlayers() {
            new Thread(() => {
                while (RUNNING)
                {
                    Players.Add(new Player(_server.AcceptTcpClient(), this));
                    Program.Print(PrintType.CONN, $"player connected");
                }
            }).Start();
        }

        public void SendToAllPlayersInRoom(Room room, JObject send) {
            foreach (Room r in Rooms) {
                if (r.Uuid == room.Uuid)
                {
                    foreach (Player p in room.Players)
                    {
                        p.Send(send);
                    }
                    return;
                }
            }
            Program.Print(PrintType.ERR, $"send to all, room not found: {room.Uuid}");
        }

        /*
         *  Function methods
         */

        private IPAddress GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip;
                }
            }
            throw new Exception("Local IP Address Not Found!");
        }

        /*
         *  Listener methods
         */

        public void OnDisconnect(Player player)
        {
            Players.Remove(player);
            Program.Print(PrintType.DISCON, $"player disconnected");
        }

        public void OnReceiveData(string data)
        {
            _api.Receive(JObject.Parse(data));
        }
    }
}
