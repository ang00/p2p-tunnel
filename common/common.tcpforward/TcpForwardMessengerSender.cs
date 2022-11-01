﻿using common.libs;
using common.server;
using common.server.model;
using System;
using System.Threading.Tasks;

namespace common.tcpforward
{
    public class TcpForwardMessengerSender
    {
        private readonly MessengerSender messengerSender;
        public TcpForwardMessengerSender(MessengerSender messengerSender)
        {
            this.messengerSender = messengerSender;
        }

        public bool SendRequest(TcpForwardInfo arg)
        {
            var bytes = arg.ToBytes();
            return messengerSender.SendOnly(new MessageRequestWrap
            {
                MessengerId = (int)TcpForwardMessengerIds.Request,
                Connection = arg.Connection,
                Payload = bytes
            }).Result;
        }

        public void SendResponse(TcpForwardInfo arg, IConnection connection)
        {
            var bytes = arg.ToBytes();
            _ = messengerSender.SendOnly(new MessageRequestWrap
            {
                MessengerId = (int)TcpForwardMessengerIds.Response,
                Connection = connection,
                Payload = bytes
            });
        }

        public async Task<MessageResponeInfo> GetPorts(IConnection Connection)
        {
            return await messengerSender.SendReply(new MessageRequestWrap
            {
                MessengerId = (int)TcpForwardMessengerIds.Ports,
                Connection = Connection,
                Payload = Helper.EmptyArray
            }).ConfigureAwait(false);
        }

        public async Task<MessageResponeInfo> UnRegister(IConnection Connection, TcpForwardUnRegisterParamsInfo data)
        {
            return await messengerSender.SendReply(new MessageRequestWrap
            {
                MessengerId = (int)TcpForwardMessengerIds.SignOut,
                Connection = Connection,
                Payload = data.ToBytes()
            }).ConfigureAwait(false);
        }
        public async Task<MessageResponeInfo> Register(IConnection Connection, TcpForwardRegisterParamsInfo data)
        {
            return await messengerSender.SendReply(new MessageRequestWrap
            {
                MessengerId = (int)TcpForwardMessengerIds.SignIn,
                Connection = Connection,
                Payload = data.ToBytes(),
            }).ConfigureAwait(false);
        }
    }
}
