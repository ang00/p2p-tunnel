﻿using common.libs;
using common.server;
using common.server.model;
using server.messengers.register;
using System.Net;
using System.Threading.Tasks;

namespace server.service.messengers
{
    [MessengerIdRange((int)PunchHoleMessengerIds.Min,(int)PunchHoleMessengerIds.Max)]
    public class PunchHoleMessenger : IMessenger
    {
        private readonly IClientRegisterCaching clientRegisterCache;
        private readonly MessengerSender messengerSender;

        public PunchHoleMessenger(IClientRegisterCaching clientRegisterCache, MessengerSender messengerSender)
        {
            this.clientRegisterCache = clientRegisterCache;
            this.messengerSender = messengerSender;
        }

        [MessengerId((int)PunchHoleMessengerIds.Execute)]
        public async Task<byte[]> Execute(IConnection connection)
        {
            PunchHoleParamsInfo model = new PunchHoleParamsInfo();
            model.DeBytes(connection.ReceiveRequestWrap.Payload);

            //A已注册
            if (clientRegisterCache.Get(connection.ConnectId, out RegisterCacheInfo source))
            {
                //B已注册
                if (clientRegisterCache.Get(model.ToId, out RegisterCacheInfo target))
                {
                    //是否在同一个组
                    if (source.GroupId == target.GroupId)
                    {
                        if (model.PunchForwardType == PunchForwardTypes.NOTIFY)
                        {
                            if (source.GetTunnel(model.TunnelName, out TunnelRegisterCacheInfo tunnel) == false)
                            {
                                return Helper.FalseArray;
                            }
                            model.Data = new PunchHoleNotifyInfo
                            {
                                Ip = source.OnLineConnection.Address.Address,
                                LocalIps = source.LocalIps,
                                LocalPort = tunnel.LocalPort,
                                Port = tunnel.Port,
                                IsDefault = tunnel.IsDefault,
                                Index = model.Index,
                            }.ToBytes();
                        }

                        model.FromId = connection.ConnectId;
                        var res = await messengerSender.SendOnly(new MessageRequestWrap
                        {
                            Connection = connection.ServerType == ServerType.UDP ? target.UdpConnection : target.TcpConnection,
                            Payload = model.ToBytes(),
                            MessengerId = connection.ReceiveRequestWrap.MessengerId,
                            RequestId = connection.ReceiveRequestWrap.RequestId,
                        }).ConfigureAwait(false);
                        return res ? Helper.TrueArray : Helper.FalseArray;
                    }
                }
            }
            return Helper.FalseArray;
        }
    }
}
