﻿using System.Collections.Immutable;
using Quobject.EngineIoClientDotNet.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Quobject.EngineIoClientDotNet.Thread;
using WebSocket4Net;
using Quobject.EngineIoClientDotNet.Parser;

namespace Quobject.EngineIoClientDotNet.Client.Transports
{
    public class WebSocket : Transport
    {
        public static readonly string NAME = "websocket";

        private WebSocket4Net.WebSocket ws;

        public WebSocket(Options opts)
            : base(opts)
        {
            Name = NAME;
        }

        protected override void DoOpen()
        {
            //var headers = new Dictionary<string, string>();
            //Emit(EVENT_REQUEST_HEADERS, headers);
            //var wsHeaders = new List<KeyValuePair<string, string>>();

            //ws = new WebSocket4Net.WebSocket(this.Uri(), "", null, wsHeaders, "", "", WebSocketVersion.Rfc6455);
            ws = new WebSocket4Net.WebSocket(this.Uri());           
            
            ws.Opened += ws_Opened;
            ws.Closed += ws_Closed;
            ws.MessageReceived += ws_MessageReceived;
            ws.Error += ws_Error;
            ws.Open();                            
        }

        private void ws_Opened(object sender, EventArgs e)
        {
            this.OnOpen();
        }

        void ws_Closed(object sender, EventArgs e)
        {
            this.OnClose();
        }

        void ws_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            this.OnData(e.Message);
        }

        void ws_Error(object sender, SuperSocket.ClientEngine.ErrorEventArgs e)
        {
            this.OnError("websocket error", e.Exception);
        }

        protected override void Write(System.Collections.Immutable.ImmutableList<Parser.Packet> packets)
        {
            Writable = false;
            foreach (var packet in packets)
            {
                Parser.Parser.EncodePacket(packet, new WriteEncodeCallback( this));
            }

            // fake drain
            // defer to next tick to allow Socket to clear writeBuffer
            EasyTimer.SetTimeout(() =>
            {
                Writable = true;
                Emit(EVENT_DRAIN);
            }, 1);
        }

        public class WriteEncodeCallback : IEncodeCallback
        {
            private WebSocket webSocket;

            public WriteEncodeCallback(WebSocket webSocket)
            {
                this.webSocket = webSocket;
            }

            public void Call(object data)
            {
                if (data is string)
                {
                    webSocket.ws.Send((string) data);
                }
                else if (data is byte[])
                {                    
                    var d = (byte[]) data;
                    webSocket.ws.Send(d, 0, d.Length);
                }
            }
        }



        protected override void DoClose()
        {
            if (ws != null)
            {
                ws.Opened -= ws_Opened;
                ws.Closed -= ws_Closed;
                ws.MessageReceived -= ws_MessageReceived;
                ws.Error -= ws_Error;
                ws.Close();
            }
        }



        public string Uri()
        {
            ImmutableDictionary<string, string> query = this.Query;
            if (query == null)
            {
                query = ImmutableDictionary<string, string>.Empty;
            }
            string schema = this.Secure ? "wss" : "ws";
            string portString = "";

            if (this.TimestampRequests)
            {
                query = query.Add(this.TimestampParam, DateTime.Now.Ticks.ToString() + "-" + Transport.Timestamps++);
            }

            string _query = ParseQS.Encode(query);

            if (this.Port > 0 && (("wss" == schema && this.Port != 443)
                    || ("ws" == schema && this.Port != 80)))
            {
                portString = ":" + this.Port;
            }

            if (_query.Length > 0)
            {
                _query = "?" + _query;
            }

            return schema + "://" + this.Hostname + portString + this.Path + _query;
        }
    }
}
