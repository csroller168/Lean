﻿using Newtonsoft.Json;
using QuantConnect.Data.Market;
using QuantConnect.Logging;
using QuantConnect.Packets;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebSocketSharp;

namespace QuantConnect.Brokerages
{

    /// <summary>
    /// Provides shared brokerage websockets implementation
    /// </summary>
    public abstract class BaseWebsocketsBrokerage : Brokerage
    {

        #region Declarations
        /// <summary>
        /// The list of queued ticks 
        /// </summary>
        public List<Tick> Ticks = new List<Tick>();
        /// <summary>
        /// The websockets client instance
        /// </summary>
        protected IWebSocket WebSocket;
        /// <summary>
        /// The rest client instance
        /// </summary>
        protected IRestClient RestClient;
        /// <summary>
        /// standard json parsing settings
        /// </summary>
        protected JsonSerializerSettings JsonSettings = new JsonSerializerSettings { FloatParseHandling = FloatParseHandling.Decimal };
        /// <summary>
        /// A list of currently active orders
        /// </summary>
        public ConcurrentDictionary<int, Orders.Order> CachedOrderIDs = new ConcurrentDictionary<int, Orders.Order>();
        /// <summary>
        /// A list of currently subscribed channels
        /// </summary>
        protected Dictionary<string, Channel> ChannelList = new Dictionary<string, Channel>();
        private string _market { get; set; }
        /// <summary>
        /// The api secret
        /// </summary>
        protected string ApiSecret;
        /// <summary>
        /// The api key
        /// </summary>
        protected string ApiKey;
        /// <summary>
        /// Timestamp of most recent heeartbeat message
        /// </summary>
        protected DateTime LastHeartbeatUtcTime = DateTime.UtcNow;
        const int _heartbeatTimeout = 300;
        Thread _connectionMonitorThread;
        CancellationTokenSource _cancellationTokenSource;
        private readonly object _lockerConnectionMonitor = new object();
        private volatile bool _connectionLost;
        #endregion

        /// <summary>
        /// Creates an instance of a websockets brokerage
        /// </summary>
        /// <param name="wssUrl">the webco</param>
        /// <param name="websocket"></param>
        /// <param name="restClient"></param>
        /// <param name="apiKey"></param>
        /// <param name="apiSecret"></param>
        /// <param name="market"></param>
        /// <param name="name"></param>
        public BaseWebsocketsBrokerage(string wssUrl, IWebSocket websocket, IRestClient restClient, string apiKey, string apiSecret, string market, string name) : base(name)
        {
            WebSocket = websocket;
            WebSocket.Initialize(wssUrl);
            RestClient = restClient;
            _market = market;
            ApiSecret = apiSecret;
            ApiKey = apiKey;
        }

        /// <summary>
        /// Handles websocket received messages
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public abstract void OnMessage(object sender, MessageEventArgs e);

        /// <summary>
        /// Creates wss connection, monitors for disconnection and re-connects when necessary
        /// </summary>
        public override void Connect()
        {
            WebSocket.OnMessage += OnMessage;
            WebSocket.OnError += OnError;

            WebSocket.Connect();
            _cancellationTokenSource = new CancellationTokenSource();
            _connectionMonitorThread = new Thread(() =>
            {
                var nextReconnectionAttemptUtcTime = DateTime.UtcNow;
                double nextReconnectionAttemptSeconds = 1;

                lock (_lockerConnectionMonitor)
                {
                    LastHeartbeatUtcTime = DateTime.UtcNow;
                }

                try
                {
                    while (!_cancellationTokenSource.IsCancellationRequested)
                    {

                        TimeSpan elapsed;
                        lock (_lockerConnectionMonitor)
                        {
                            elapsed = DateTime.UtcNow - LastHeartbeatUtcTime;
                        }

                        if (!_connectionLost && elapsed > TimeSpan.FromSeconds(_heartbeatTimeout))
                        {
                            _connectionLost = true;
                            nextReconnectionAttemptUtcTime = DateTime.UtcNow.AddSeconds(nextReconnectionAttemptSeconds);

                            OnMessage(BrokerageMessageEvent.Disconnected("Connection with server lost. This could be because of internet connectivity issues."));
                        }
                        else if (_connectionLost)
                        {
                            try
                            {
                                if (elapsed <= TimeSpan.FromSeconds(_heartbeatTimeout))
                                {
                                    _connectionLost = false;
                                    nextReconnectionAttemptSeconds = 1;

                                    OnMessage(BrokerageMessageEvent.Reconnected("Connection with server restored."));
                                }
                                else
                                {
                                    if (DateTime.UtcNow > nextReconnectionAttemptUtcTime)
                                    {
                                        try
                                        {
                                            Reconnect();
                                        }
                                        catch (Exception)
                                        {
                                            // double the interval between attempts (capped to 1 minute)
                                            nextReconnectionAttemptSeconds = Math.Min(nextReconnectionAttemptSeconds * 2, 60);
                                            nextReconnectionAttemptUtcTime = DateTime.UtcNow.AddSeconds(nextReconnectionAttemptSeconds);
                                        }
                                    }
                                }
                            }
                            catch (Exception exception)
                            {
                                Log.Error(exception);
                            }
                        }

                        Thread.Sleep(10000);
                    }
                }
                catch (Exception exception)
                {
                    Log.Error(exception);
                }
            });
            _connectionMonitorThread.Start();
            while (!_connectionMonitorThread.IsAlive)
            {
                Thread.Sleep(1);
            }
        }

        /// <summary>
        /// Handles websocket errors
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void OnError(object sender, ErrorEventArgs e)
        {
            Log.Debug(e.Message);
        }

        /// <summary>
        /// Handles reconnections in the event of connection loss
        /// </summary>
        protected virtual void Reconnect()
        {
            var subscribed = GetSubscribed();

            WebSocket.OnError -= this.OnError;
            try
            {
                //try to clean up state
                if (IsConnected)
                {
                    WebSocket.Close();
                }
                if (!IsConnected)
                {
                    WebSocket.Connect();
                }
            }
            finally
            {
                WebSocket.OnError += this.OnError;
                this.Subscribe(null, subscribed);
            }
        }

        /// <summary>
        /// Handles the creation of websocket subscriptions
        /// </summary>
        /// <param name="job"></param>
        /// <param name="symbols"></param>
        public abstract void Subscribe(LiveNodePacket job, IEnumerable<Symbol> symbols);

        /// <summary>
        /// Gets a list of current subscriptions
        /// </summary>
        /// <returns></returns>
        protected virtual IList<Symbol> GetSubscribed()
        {
            IList<Symbol> list = new List<Symbol>();
            lock (ChannelList)
            {
                foreach (var item in ChannelList)
                {
                    list.Add(Symbol.Create(item.Value.Symbol, SecurityType.Forex, _market));
                }
            }
            return list;
        }

        /// <summary>
        /// Represents a subscription channel
        /// </summary>
        protected class Channel
        {
            /// <summary>
            /// The name of the channel
            /// </summary>
            public string Name { get; set; }
            /// <summary>
            /// The ticker symbol of the channel
            /// </summary>
            public string Symbol { get; set; }
        }

    }

}
