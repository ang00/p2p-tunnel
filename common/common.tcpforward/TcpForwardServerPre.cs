﻿using common.libs;
using common.libs.extends;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace common.tcpforward
{
    public sealed class TcpForwardServerPre : ITcpForwardServer
    {
        public Func<TcpForwardInfo, Task<bool>> OnRequest { get; set; } = (info) => Task.FromResult(true);
        public Action<ListeningChangeInfo> OnListeningChange { get; set; } = (param) => { };
        private readonly ServersManager serversManager = new ServersManager();
        private readonly ClientsManager clientsManager = new ClientsManager();

        private NumberSpaceUInt32 requestIdNs = new NumberSpaceUInt32(0);
        SemaphoreSlim Semaphore = new SemaphoreSlim(1);

        private int receiveBufferSize = 8 * 1024;

        public TcpForwardServerPre()
        {
        }
        public void Init(int numConnections, int receiveBufferSize)
        {
            this.receiveBufferSize = receiveBufferSize;
        }
        public void Start(ushort port, TcpForwardAliveTypes aliveType)
        {
            if (serversManager.Contains(port))
            {
                return;
            }

            BindAccept(port, aliveType);
            OnListeningChange?.Invoke(new ListeningChangeInfo { Port = port, State = true });
        }

        private void BindAccept(ushort port, TcpForwardAliveTypes aliveType)
        {
            var endpoint = new IPEndPoint(IPAddress.Any, port);
            var socket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(endpoint);
            socket.Listen(int.MaxValue);

            serversManager.TryAdd(new ServerInfo
            {
                SourcePort = port,
                Socket = socket
            });

            SocketAsyncEventArgs acceptEventArg = new SocketAsyncEventArgs();
            ForwardAsyncUserToken token = new ForwardAsyncUserToken
            {
                SourceSocket = socket,
                SourcePort = port,
            };
            token.Request.AliveType = aliveType;
            token.Request.SourcePort = port;
            acceptEventArg.UserToken = token;
            acceptEventArg.Completed += IO_Completed;
            StartAccept(acceptEventArg);
        }
        private void StartAccept(SocketAsyncEventArgs acceptEventArg)
        {
            try
            {
                acceptEventArg.AcceptSocket = null;
                ForwardAsyncUserToken token = ((ForwardAsyncUserToken)acceptEventArg.UserToken);
                if (token.SourceSocket.AcceptAsync(acceptEventArg) == false)
                {
                    ProcessAccept(acceptEventArg);
                }
            }
            catch (Exception)
            {
            }
        }

        private void IO_Completed(object sender, SocketAsyncEventArgs e)
        {
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Accept:
                    ProcessAccept(e);
                    break;
                case SocketAsyncOperation.Receive:
                    ProcessReceive(e);
                    break;
                default:
                    Logger.Instance.Error(e.LastOperation.ToString());
                    break;
            }
        }

        private void ProcessAccept(SocketAsyncEventArgs e)
        {
            BindReceive(e);
            StartAccept(e);
        }

        private void BindReceive(SocketAsyncEventArgs e)
        {
            try
            {
                ForwardAsyncUserToken acceptToken = (e.UserToken as ForwardAsyncUserToken);

                uint id = requestIdNs.Increment();
                ForwardAsyncUserToken token = new ForwardAsyncUserToken
                {
                    SourceSocket = e.AcceptSocket,
                    Request = new TcpForwardInfo
                    {
                        RequestId = id,
                        AliveType = acceptToken.Request.AliveType,
                        SourcePort = acceptToken.SourcePort,
                        DataType = TcpForwardDataTypes.Connect,
                        StateType = TcpForwardStateTypes.Success
                    },
                    SourcePort = acceptToken.SourcePort
                };
                clientsManager.TryAdd(token);

                SocketAsyncEventArgs readEventArgs = new SocketAsyncEventArgs
                {
                    UserToken = token,
                    SocketFlags = SocketFlags.None
                };
                token.Saea = readEventArgs;

                token.SourceSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, true);
                token.SourceSocket.SendTimeout = 5000;
                //token.SourceSocket.SendBufferSize = receiveBufferSize;
                //token.SourceSocket.ReceiveBufferSize = receiveBufferSize;
                token.PoolBuffer = new byte[receiveBufferSize];
                readEventArgs.SetBuffer(token.PoolBuffer, 0, receiveBufferSize);
                readEventArgs.Completed += IO_Completed;

                if (token.Request.AliveType == TcpForwardAliveTypes.Tunnel)
                {
                    token.FirstPacket = false;
                    Receive(token, Helper.EmptyArray).Wait();
                }
                else
                {
                    if (token.SourceSocket.ReceiveAsync(readEventArgs) == false)
                    {
                        ProcessReceive(readEventArgs);
                    }
                }
            }
            catch (Exception)
            {
            }
        }
        private async void ProcessReceive(SocketAsyncEventArgs e)
        {
            try
            {
                ForwardAsyncUserToken token = (ForwardAsyncUserToken)e.UserToken;
                if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
                {
                    await Receive(token, e.Buffer.AsMemory(e.Offset, e.BytesTransferred));
                    if (token.FirstPacket == true)
                    {
                        token.FirstPacket = false;
                        return;
                    }

                    if (token.SourceSocket.Available > 0)
                    {
                        while (token.SourceSocket.Available > 0)
                        {
                            int length = await token.SourceSocket.ReceiveAsync(e.Buffer.AsMemory(), SocketFlags.None);
                            if (length > 0)
                            {
                                await Receive(token, e.Buffer.AsMemory(0, length));
                            }
                        }
                    }
                    if (token.SourceSocket.Connected)
                    {
                        if (token.SourceSocket.ReceiveAsync(e) == false)
                        {
                            ProcessReceive(e);
                        }
                    }
                    else
                    {
                        CloseClientSocket(e);
                    }
                }
                else
                {
                    CloseClientSocket(e);
                }
            }
            catch (Exception)
            {
                CloseClientSocket(e);
                //Logger.Instance.DebugError(ex);
            }
        }

        private async Task Receive(ForwardAsyncUserToken token, Memory<byte> data)
        {
            await Semaphore.WaitAsync();
            token.Request.Buffer = data;
            bool res = await OnRequest(token.Request);
            token.Request.Buffer = Helper.EmptyArray;
            if (res == false)
            {
                clientsManager.TryRemove(token.Request.RequestId, out _);
                // CloseClientSocket(token);
            }
            Semaphore.Release();
        }

        private void CloseClientSocket(SocketAsyncEventArgs e)
        {
            ForwardAsyncUserToken token = e.UserToken as ForwardAsyncUserToken;
            CloseClientSocket(token);
        }
        private void CloseClientSocket(ForwardAsyncUserToken token)
        {
            clientsManager.TryRemove(token.Request.RequestId, out _);
            token.Request.StateType = TcpForwardStateTypes.Close;
            _ = Receive(token, Helper.EmptyArray);
        }
      
        public async Task Response(TcpForwardInfo model)
        {
            if (clientsManager.TryGetValue(model.RequestId, out ForwardAsyncUserToken token))
            {
                try
                {
                    if (model.StateType == TcpForwardStateTypes.Success)
                    {
                        if (token.Request.DataType == TcpForwardDataTypes.Connect)
                        {
                            token.Request.DataType = TcpForwardDataTypes.Forward;
                            token.Request.TargetEndpoint = Helper.EmptyArray;
                            if (token.Request.ForwardType == TcpForwardTypes.Proxy)
                            {
                                await token.SourceSocket.SendAsync(HttpConnectMethodHelper.ConnectSuccessMessage(), SocketFlags.None).WaitAsync(TimeSpan.FromSeconds(5));
                            }
                            else if (token.Request.Cache.Length > 0)
                            {
                                _ = Receive(token, token.Request.Cache);
                                token.Request.Cache = Helper.EmptyArray;
                            }
                            if (token.SourceSocket.ReceiveAsync(token.Saea) == false)
                            {
                                ProcessReceive(token.Saea);
                            }

                        }
                        if (model.Buffer.Length > 0)
                        {
                            try
                            {
                                await token.SourceSocket.SendAsync(model.Buffer, SocketFlags.None).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
                            }
                            catch (Exception)
                            {
                                clientsManager.TryRemove(model.RequestId, out _);
                            }
                        }
                    }
                    else
                    {
                        if (token.Request.ForwardType == TcpForwardTypes.Proxy)
                        {
                            await token.SourceSocket.SendAsync(HttpConnectMethodHelper.ConnectErrorMessage(), SocketFlags.None).WaitAsync(TimeSpan.FromSeconds(5));
                        }
                        else if (model.Buffer.Length > 0)
                        {
                            await token.SourceSocket.SendAsync(model.Buffer, SocketFlags.None).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
                        }
                        clientsManager.TryRemove(model.RequestId, out _);
                    }
                }
                catch (Exception)
                {
                    clientsManager.TryRemove(model.RequestId, out _);
                }
            }
            await Task.CompletedTask;
        }
       
        public void Stop(ushort sourcePort)
        {
            if (serversManager.TryRemove(sourcePort, out ServerInfo model))
            {
                OnListeningChange?.Invoke(new ListeningChangeInfo
                {
                    Port = model.SourcePort,
                    State = false
                });

                clientsManager.Clear(model.SourcePort);
            }
        }
        public void Stop()
        {
            serversManager.Clear();
            clientsManager.Clear();
            OnListeningChange?.Invoke(new ListeningChangeInfo { Port = 0, State = true });
        }
    }

    sealed class ForwardAsyncUserToken
    {
        public Socket SourceSocket { get; set; }
        public ushort SourcePort { get; set; } = 0;
        public TcpForwardInfo Request { get; set; } = new TcpForwardInfo { };
        public SocketAsyncEventArgs Saea { get; set; }
        public bool FirstPacket { get; set; } = true;

        public byte[] PoolBuffer { get; set; }
    }
    sealed class ClientsManager
    {
        private ConcurrentDictionary<ulong, ForwardAsyncUserToken> clients = new();

        public bool TryAdd(ForwardAsyncUserToken model)
        {
            return clients.TryAdd(model.Request.RequestId, model);
        }
        public bool TryGetValue(ulong id, out ForwardAsyncUserToken c)
        {
            return clients.TryGetValue(id, out c);
        }
        public bool TryRemove(ulong id, out ForwardAsyncUserToken c)
        {
            bool res = clients.TryRemove(id, out c);
            if (res)
            {
                try
                {
                    c.SourceSocket.SafeClose();

                    c.PoolBuffer = Helper.EmptyArray;

                    c.Saea.Dispose();
                    GC.Collect();
                    //  GC.SuppressFinalize(c);
                }
                catch (Exception)
                {
                }
            }
            return res;
        }
        public void Clear(int sourcePort)
        {
            IEnumerable<ulong> requestIds = clients.Where(c => c.Value.SourcePort == sourcePort).Select(c => c.Key);
            foreach (var requestId in requestIds)
            {
                TryRemove(requestId, out _);
            }
        }
        public void Clear()
        {
            IEnumerable<ulong> requestIds = clients.Select(c => c.Key);
            foreach (var requestId in requestIds)
            {
                TryRemove(requestId, out _);
            }
        }
    }

    sealed class ServersManager
    {
        public ConcurrentDictionary<int, ServerInfo> services = new();
        public bool TryAdd(ServerInfo model)
        {
            return services.TryAdd(model.SourcePort, model);
        }
        public bool Contains(int port)
        {
            return services.ContainsKey(port);
        }
        public bool TryGetValue(int port, out ServerInfo c)
        {
            return services.TryGetValue(port, out c);
        }
        public bool TryRemove(int port, out ServerInfo c)
        {
            bool res = services.TryRemove(port, out c);
            if (res)
            {
                try
                {
                    c.Socket.SafeClose();
                    GC.Collect();
                    //  GC.SuppressFinalize(c);
                }
                catch (Exception)
                {
                }
            }
            return res;
        }
        public void Clear()
        {
            foreach (var item in services.Values)
            {
                try
                {
                    item.Socket.SafeClose();
                    GC.Collect();
                    // GC.SuppressFinalize(item);
                }
                catch (Exception)
                {
                }
            }
            services.Clear();
        }

    }
    sealed class ServerInfo
    {
        public ushort SourcePort { get; set; }
        public Socket Socket { get; set; }
    }
}