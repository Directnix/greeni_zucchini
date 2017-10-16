﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace zucchini_client.Network
{
    interface IServerListener
    {
        void OnConnected();
        void OnDataReceived(dynamic load);
        void OnErrorReceived(string trace);
    }
}
