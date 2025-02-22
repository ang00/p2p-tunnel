﻿using client.messengers.clients;
using common.libs;
using common.libs.extends;
using common.server;
using common.server.servers.rudp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace client.realize.messengers.clients
{
    /// <summary>
    /// 客户单缓存
    /// </summary>
    public sealed class ClientInfoCaching : IClientInfoCaching
    {
        private readonly ConcurrentDictionary<ulong, ClientInfo> clients = new ConcurrentDictionary<ulong, ClientInfo>();
        private readonly ConcurrentDictionary<string, ClientInfo> clientsByName = new ConcurrentDictionary<string, ClientInfo>();

        /// <summary>
        /// 下线
        /// </summary>
        public Action<ClientInfo> OnOffline { get; set; } = (param) => { };
        /// <summary>
        /// 下线后
        /// </summary>
        public Action<ClientInfo> OnOfflineAfter { get; set; } = (param) => { };
        /// <summary>
        /// 上线
        /// </summary>
        public Action<ClientInfo> OnOnline { get; set; } = (param) => { };
        /// <summary>
        /// 添加
        /// </summary>
        public Action<ClientInfo> OnAdd { get; set; } = (param) => { };
        /// <summary>
        /// 删除
        /// </summary>
        public Action<ClientInfo> OnRemove { get; set; } = (param) => { };

        /// <summary>
        /// 添加
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        public bool Add(ClientInfo client)
        {
            bool result = clients.TryAdd(client.Id, client);
            if (result)
            {
                clientsByName.TryAdd(client.Name, client);
            }

            OnAdd?.Invoke(client);
            return result;
        }

        /// <summary>
        /// 获取
        /// </summary>
        /// <param name="id"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        public bool Get(ulong id, out ClientInfo client)
        {
            return clients.TryGetValue(id, out client);
        }
        /// <summary>
        /// 根据名字获取
        /// </summary>
        /// <param name="name"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        public bool GetByName(string name, out ClientInfo client)
        {
            return clientsByName.TryGetValue(name, out client);
        }

        /// <summary>
        /// 所有客户端
        /// </summary>
        /// <returns></returns>
        public IEnumerable<ClientInfo> All()
        {
            return clients.Values;
        }
        /// <summary>
        /// 所有客户端id
        /// </summary>
        /// <returns></returns>
        public IEnumerable<ulong> AllIds()
        {
            return clients.Keys;
        }

        /// <summary>
        /// 设置状态
        /// </summary>
        /// <param name="id"></param>
        /// <param name="val"></param>
        public void SetConnecting(ulong id, bool val)
        {
            if (clients.TryGetValue(id, out ClientInfo client))
            {
                client.SetConnecting(val);
            }
        }

        /// <summary>
        /// 下线
        /// </summary>
        /// <param name="id"></param>
        /// <param name="offlineType"></param>
        public void Offline(ulong id, ClientOfflineTypes offlineType = ClientOfflineTypes.Manual)
        {
            if (clients.TryGetValue(id, out ClientInfo client))
            {
                if (client.ConnectType != ClientConnectTypes.Unknow)
                {
                    OnOffline?.Invoke(client);
                }
                client.Offline(offlineType);
                if (client.ConnectType != ClientConnectTypes.Unknow)
                {
                    OnOfflineAfter?.Invoke(client);
                }
            }
        }

        /// <summary>
        /// 删除
        /// </summary>
        /// <param name="id"></param>
        public void Remove(ulong id)
        {
            if (clients.TryRemove(id, out ClientInfo client))
            {
                client.Offline();
                clientsByName.TryRemove(client.Name, out _);
                //udpservers.TryRemove(id, out _);
                //tunnelPorts.TryRemove(id, out _);
                OnRemove?.Invoke(client);
            }
        }

        /// <summary>
        /// 上线
        /// </summary>
        /// <param name="id"></param>
        /// <param name="connection"></param>
        /// <param name="connectType"></param>
        /// <param name="onlineType"></param>
        public void Online(ulong id, IConnection connection, ClientConnectTypes connectType, ClientOnlineTypes onlineType)
        {
            if (clients.TryGetValue(id, out ClientInfo client))
            {
                connection.ConnectId = id;
                client.Online(connection, connectType, onlineType);
                OnOnline?.Invoke(client);
            }
        }

        /// <summary>
        /// 新端口
        /// </summary>
        /// <param name="id"></param>
        /// <param name="port"></param>
        public void AddTunnelPort(ulong id, int port)
        {
            if (clients.TryGetValue(id, out ClientInfo client))
            {
                client.TunnelPort = port;
            }
        }
        /// <summary>
        /// 新端口
        /// </summary>
        /// <param name="id"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        public bool GetTunnelPort(ulong id, out int port)
        {
            port = 0;
            if (clients.TryGetValue(id, out ClientInfo client))
            {
                port = client.TunnelPort;
                return port > 0;
            }
            return false;
        }

        /// <summary>
        /// 新通道
        /// </summary>
        /// <param name="id"></param>
        /// <param name="server"></param>
        public void AddUdpserver(ulong id, UdpServer server)
        {
            if (clients.TryGetValue(id, out ClientInfo client))
            {
                client.TunnelServer?.Disponse();
                client.TunnelServer = server;
            }
        }
        /// <summary>
        /// 新通道
        /// </summary>
        /// <param name="id"></param>
        /// <param name="server"></param>
        /// <returns></returns>
        public bool GetUdpserver(ulong id, out UdpServer server)
        {
            server = null;
            if (clients.TryGetValue(id, out ClientInfo client))
            {
                if (client.TunnelServer == null) return false;
                server = (UdpServer)client.TunnelServer;
                return true;
            }
            return false;
        }
        /// <summary>
        /// 删除新通道
        /// </summary>
        /// <param name="id"></param>
        /// <param name="clear"></param>
        public void RemoveUdpserver(ulong id, bool clear = false)
        {
            if (clients.TryGetValue(id, out ClientInfo client))
            {
                if (clear)
                {
                    client?.TunnelServer.Disponse();
                }
                client.TunnelServer = null;
            }
        }

        /// <summary>
        /// 清除所有
        /// </summary>
        public void Clear()
        {
            var _clients = clients.Values;
            foreach (var item in _clients)
            {
                OnOffline?.Invoke(item);
                item.Offline(ClientOfflineTypes.Manual);
                OnOfflineAfter?.Invoke(item);
                clients.TryRemove(item.Id, out _);
                OnRemove?.Invoke(item);
            }
            clientsByName.Clear();
        }
    }
}
