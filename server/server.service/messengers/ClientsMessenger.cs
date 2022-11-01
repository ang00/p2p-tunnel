﻿using common.libs;
using common.libs.extends;
using common.server;
using common.server.model;
using server.messengers.register;

namespace server.service.messengers
{
    [MessengerIdRange((int)ClientsMessengerIds.Min, (int)ClientsMessengerIds.Max)]
    public class ClientsMessenger : IMessenger
    {
        private readonly IClientRegisterCaching clientRegisterCache;
        private readonly NumberSpace numberSpaceTunnelName = new NumberSpace((ulong)TunnelDefaults.MAX + 1);
        public ClientsMessenger(IClientRegisterCaching clientRegisterCache)
        {
            this.clientRegisterCache = clientRegisterCache;
        }

        [MessengerId((int)ClientsMessengerIds.IP)]
        public byte[] Ip(IConnection connection)
        {
            return connection.Address.Address.GetAddressBytes();
        }
        [MessengerId((int)ClientsMessengerIds.Port)]
        public byte[] Port(IConnection connection)
        {
            return connection.Address.Port.ToBytes();
        }

        [MessengerId((int)ClientsMessengerIds.AddTunnel)]
        public byte[] AddTunnel(IConnection connection)
        {
            TunnelRegisterInfo model = new TunnelRegisterInfo();
            model.DeBytes(connection.ReceiveRequestWrap.Payload);
            if (clientRegisterCache.Get(connection.ConnectId, out RegisterCacheInfo source))
            {
                if (model.TunnelName == (ulong)TunnelDefaults.MIN)
                {
                    model.TunnelName = numberSpaceTunnelName.Increment();
                }

                source.AddTunnel(new TunnelRegisterCacheInfo
                {
                    IsDefault = true,
                    LocalPort = model.LocalPort,
                    Port = model.Port,
                    Servertype = connection.ServerType,
                    TunnelName = model.TunnelName,
                });
            }
            return model.TunnelName.ToBytes();
        }

        [MessengerId((int)ClientsMessengerIds.RemoveTunnel)]
        public void RemoveTunnel(IConnection connection)
        {
            if (clientRegisterCache.Get(connection.ConnectId, out RegisterCacheInfo source))
            {
                ulong tunnelName = connection.ReceiveRequestWrap.Payload.Span.ToUInt64();
                source.RemoveTunnel(tunnelName);
            }
        }

        
    }
}
