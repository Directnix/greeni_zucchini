﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using zucchini_client.Model;
using zucchini_client.Network;

namespace zucchini_client
{
    public partial class Lobby : Form , IServerListener
    {
        private ApiCaller _api;
        private Player _self;

        private List<Room> _rooms = new List<Room>();

        private bool _inRoom = false;
        private List<Player> _playersInRoom = new List<Player>();
        private Room _currentRoom;

        public Lobby()
        {
            InitializeComponent();
        }

        private void Lobby_Load(object sender, EventArgs e)
        {
            pnl_room.Visible = false;

            _api = new ApiCaller(new Connection(this));
            _self = new Player("Directnix");

            new Thread(() => {
                _api.ConnectPlayer(_self);
                Thread.Sleep(100);
                _api.RefreshRooms(_self);
            }).Start();

            lb_rooms.DisplayMember = "Name";
            lb_rooms.ValueMember = "Uuid";

            lb_players.DisplayMember = "Name";
            lb_players.ValueMember = "Uuid";
        }

        /*
         *  Update UI 
         */

        private void UpdateRoomList() {
            lb_rooms.Invoke(new Action(() => lb_rooms.Items.Clear()));
            foreach (Room r in _rooms) {
                lb_rooms.Invoke(new Action(() => lb_rooms.Items.Add(new ListBoxItem { Name = r.Name, Uuid = r.Uuid })));
            }  
        }

        private void UpdatePlayerList()
        {
            if (!_inRoom)
                return;

            lb_players.Invoke(new Action(() => lb_players.Items.Clear()));
            foreach (Player p in _playersInRoom)
            {
                if(p.Host)
                    lb_players.Invoke(new Action(() => lb_players.Items.Add(new ListBoxItem { Name = $"*{p.Name}", Uuid = p.Uuid })));
                else
                    lb_players.Invoke(new Action(() => lb_players.Items.Add(new ListBoxItem { Name = p.Name, Uuid = p.Uuid })));
            }
        }

        private void GotoRoom(Room room) {
            _inRoom = true;
            btn_start.Enabled = false;

            this.Invoke(new MethodInvoker(() => {
                rtb_chat.Clear();
                pnl_room.Visible = true;
                pnl_lobby.Visible = false;

                lb_room_name.Text = room.Name;
            }));

            _currentRoom = room;

            new Thread(() => {
                Thread.Sleep(100);
                _api.FetchPlayersInRoom(room.Uuid, _self);
            }).Start();

            if (_self.Host)
                btn_start.Enabled = true;
        }

        private void GotoLobby() {
            pnl_room.Visible = false;
            pnl_lobby.Visible = true;

            _inRoom = false;
            _self.Host = false;
            _currentRoom = null;
        }

        private void AppendOnTextbox(string playerName, string text)
        {
            this.Invoke(new MethodInvoker(() => {
                    rtb_chat.AppendText($"[{playerName}] {text}\n");
            }));
        }

        /*
         *  Button Delegates 
         */

        private void btn_create_Click(object sender, EventArgs e)
        {
            var room = new Room(tb_create.Text, _self);
            _self.Host = true;
            _api.CreateRoom(room);
            _api.RefreshRooms(_self);
            GotoRoom(room);
        }

        private void btn_join_Click(object sender, EventArgs e)
        {
            try
            {
                if (lb_rooms.SelectedIndex >= 0 && lb_rooms.SelectedIndex < _rooms.Count)
                {
                    _api.JoinRoom(((ListBoxItem)lb_rooms.SelectedItem).Uuid, _self);

                    foreach(Room r in _rooms) {
                        if (((ListBoxItem)lb_rooms.SelectedItem).Uuid == r.Uuid) {
                            GotoRoom(r);
                        }
                    }
                }
            }
            catch (Exception ex) {
                Debug.WriteLine(ex.StackTrace);
            }
        }

        private void btn_refresh_Click(object sender, EventArgs e)
        {
            _api.RefreshRooms(_self);
        }

        private void btn_leave_Click(object sender, EventArgs e)
        {
            if (_currentRoom != null)
            {
                _api.LeaveRoom(_currentRoom.Uuid, _self);
                Thread.Sleep(50);
                _api.RefreshRooms(_self);
            }

            GotoLobby();
        }

        private void Lobby_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (_currentRoom != null)
                _api.LeaveRoom(_currentRoom.Uuid, _self);

            Thread.Sleep(1000);

            Environment.Exit(0);
        }

        private void btn_send_Click(object sender, EventArgs e)
        {
            _api.Message(tb_chat.Text.ToString(), _currentRoom.Uuid, _self);
            tb_chat.Clear();
        }

        /*
         *  Network Listeners 
         */

        public void OnConnected()
        {
            lb_connection.Invoke(new Action(()=> lb_connection.Text = "Connected to server."));
        }

        public void OnDataReceived(dynamic load)
        {
            switch ($"{load.id}") {
                case "room/refresh":
                    _rooms.Clear();
                    foreach (dynamic room in load.data.rooms) {
                        _rooms.Add(new Room($"{room.name}",$"{room.uuid}"));
                    }
                    UpdateRoomList();
                    break;
                case "room/players":
                    _playersInRoom.Clear();
                    foreach (dynamic player in load.data.players)
                    {
                        if(Boolean.Parse($"{player.isHost}"))
                            _playersInRoom.Add(new Player($"{player.name}", $"{player.uuid}", true));
                        else
                            _playersInRoom.Add(new Player($"{player.name}", $"{player.uuid}"));
                    }
                    UpdatePlayerList();
                    break;
                case "room/join":
                    if (_currentRoom != null)
                    {
                        _api.FetchPlayersInRoom(_currentRoom.Uuid, _self);
                        AppendOnTextbox($"{load.data.playerName}", $"joined the room!");
                    }
                    break;
                case "room/leave":
                    if (_currentRoom != null)
                    {
                        _api.FetchPlayersInRoom(_currentRoom.Uuid, _self);
                        AppendOnTextbox($"{load.data.playerName}", $"left the room!");
                    }
                    break;
                case "room/message":
                    AppendOnTextbox($"{load.data.playerName}", $"{load.data.message}");
                    break;
                case "room/newHost":
                    _self.Host = true;
                    btn_start.Enabled = true;
                    AppendOnTextbox($"{load.data.playerName}", $"you are the new host!!!");
                    break;
                case "room/noRoom":
                    GotoLobby();
                    MessageBox.Show("Room no longer excists!");
                    _api.RefreshRooms(_self);
                    break;
            }
        }

        public void OnErrorReceived(string trace)
        {
            lb_connection.Invoke(new Action(() => lb_connection.Text = "Cannot connect to server."));
        }
    }

    class ListBoxItem
    {
        public string Name { get; set; }
        public string Uuid { get; set; }
    }
}
