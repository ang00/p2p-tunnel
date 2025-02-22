﻿using common.libs.extends;
using System;
using System.Buffers;
using System.Net;

namespace common.socks5
{
    /// <summary>
    /// 
    /// </summary>
    public sealed class Socks5Info
    {
        #region 数据字段
        /// <summary>
        /// socks5步骤，执行到哪一步了
        /// </summary>
        public Socks5EnumStep Socks5Step { get; set; } = Socks5EnumStep.Request;
        /// <summary>
        /// 认证类型
        /// </summary>
        public Socks5EnumAuthType AuthType { get; set; } = Socks5EnumAuthType.NoAuth;
        /// <summary>
        /// 版本
        /// </summary>
        public byte Version { get; set; }
        /// <summary>
        /// 请求id
        /// </summary>
        public uint Id { get; set; }

        /// <summary>
        /// 来源地址，数据从目标端回来的时候回给谁
        /// </summary>
        public IPEndPoint SourceEP { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public IPEndPoint TargetEP { get; set; }

        /// <summary>
        /// 携带的数据
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public Memory<byte> Data { get; set; }
        #endregion

        #region 辅助字段
        [System.Text.Json.Serialization.JsonIgnore]
        public object Tag { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public byte[] Response { get; set; } = new byte[1];

        public ulong ClientId { get; set; }
        #endregion

        public byte[] ToBytes(out int length)
        {
            length = 1 // step + Version
                + 1 //authType
                + 4  // id
                + 1  //source length
                + 1  // target length
                + Data.Length;

            int sepLength = 0, tepLength = 0;
            if (SourceEP != null)
            {
                sepLength = SourceEP.Address.Length();
                length += sepLength + 2;
            }

            if (TargetEP != null)
            {
                tepLength = TargetEP.Address.Length();
                length += tepLength + 2;
            }

            byte[] bytes = ArrayPool<byte>.Shared.Rent(length);
            var memory = bytes.AsMemory(0, length);
            var span = memory.Span;
            int index = 0;
            bytes[index] = (byte)((byte)Socks5Step << 4 | Version);
            index += 1;
            bytes[index] = (byte)AuthType;
            index += 1;

            Id.ToBytes(memory.Slice(index));
            index += 4;

            bytes[index] = (byte)sepLength;
            index += 1;
            if (sepLength > 0)
            {
                SourceEP.Address.TryWriteBytes(span.Slice(index), out _);
                index += sepLength;

                ((ushort)SourceEP.Port).ToBytes(memory.Slice(index));
                index += 2;
            }

            bytes[index] = (byte)tepLength;
            index += 1;
            if (tepLength > 0)
            {
                TargetEP.Address.TryWriteBytes(span.Slice(index), out _);
                index += tepLength;

                ((ushort)TargetEP.Port).ToBytes(memory.Slice(index));
                index += 2;
            }

            if (Data.Length > 0)
            {
                Data.CopyTo(memory.Slice(index));
            }
            return bytes;
        }

        public void DeBytes(Memory<byte> bytes)
        {
            var span = bytes.Span;
            int index = 0;

            Socks5Step = (Socks5EnumStep)(span[index] >> 4);
            Version = (byte)(span[index] & 0b1111);
            index += 1;
            AuthType = (Socks5EnumAuthType)span[index];
            index += 1;

            Id = span.Slice(index).ToUInt32();
            index += 4;

            byte epLength = span[index];
            index += 1;
            if (epLength > 0)
            {
                IPAddress ip = new IPAddress(span.Slice(index, epLength));
                index += epLength;
                SourceEP = new IPEndPoint(ip, span.Slice(index, 2).ToUInt16());
                index += 2;
            }

            byte targetepLength = span[index];
            index += 1;
            if (targetepLength > 0)
            {
                IPAddress ip = new IPAddress(span.Slice(index, targetepLength));
                index += targetepLength;
                TargetEP = new IPEndPoint(ip, span.Slice(index, 2).ToUInt16());
                index += 2;
            }

            Data = bytes.Slice(index);
        }

        public static Socks5Info Debytes(Memory<byte> data)
        {
            Socks5Info info = new Socks5Info();
            info.DeBytes(data);
            return info;
        }

        public void Return(byte[] data)
        {
            ArrayPool<byte>.Shared.Return(data);
        }

    }
    /// <summary>
    /// 当前处于socks5协议的哪一步
    /// </summary>
    public enum Socks5EnumStep : byte
    {
        /// <summary>
        /// 第一次请求，处理认证方式
        /// </summary>
        Request = 1,
        /// <summary>
        /// 如果有认证
        /// </summary>
        Auth = 2,
        /// <summary>
        /// 发送命令，CONNECT BIND 还是  UDP ASSOCIATE
        /// </summary>
        Command = 3,
        /// <summary>
        /// 转发
        /// </summary>
        Forward = 4,
        /// <summary>
        /// 
        /// </summary>
        ForwardUdp = 5,

        None = 0
    }

    /// <summary>
    /// socks5的连接地址类型
    /// </summary>
    public enum Socks5EnumAddressType : byte
    {
        /// <summary>
        /// 
        /// </summary>
        IPV4 = 1,
        /// <summary>
        /// 
        /// </summary>
        Domain = 3,
        /// <summary>
        /// 
        /// </summary>
        IPV6 = 4
    }

    /// <summary>
    /// socks5的认证类型
    /// </summary>
    public enum Socks5EnumAuthType : byte
    {
        /// <summary>
        /// 
        /// </summary>
        NoAuth = 0x00,
        /// <summary>
        /// 
        /// </summary>
        GSSAPI = 0x01,
        /// <summary>
        /// 
        /// </summary>
        Password = 0x02,
        /// <summary>
        /// 
        /// </summary>
        IANA = 0x03,
        /// <summary>
        /// 
        /// </summary>
        UnKnow = 0x80,
        /// <summary>
        /// 
        /// </summary>
        NotSupported = 0xff,
    }
    /// <summary>
    /// socks5的认证状态0成功 其它失败
    /// </summary>
    public enum Socks5EnumAuthState : byte
    {
        /// <summary>
        /// 
        /// </summary>
        Success = 0x00,
        /// <summary>
        /// 
        /// </summary>
        UnKnow = 0xff,
    }
    /// <summary>
    /// socks5的请求指令
    /// </summary>
    public enum Socks5EnumRequestCommand : byte
    {
        /// <summary>
        /// 连接上游服务器
        /// </summary>
        Connect = 1,
        /// <summary>
        /// 绑定，客户端会接收来自代理服务器的链接，著名的FTP被动模式
        /// </summary>
        Bind = 2,
        /// <summary>
        /// UDP中继
        /// </summary>
        UdpAssociate = 3
    }
    /// <summary>
    /// socks5的请求的回复数据的指令
    /// </summary>
    public enum Socks5EnumResponseCommand : byte
    {
        /// <summary>
        /// 代理服务器连接目标服务器成功
        /// </summary>
        ConnecSuccess = 0,
        /// <summary>
        /// 代理服务器故障
        /// </summary>
        ServerError = 1,
        /// <summary>
        /// 代理服务器规则集不允许连接
        /// </summary>
        ConnectNotAllow = 2,
        /// <summary>
        /// 网络无法访问
        /// </summary>
        NetworkError = 3,
        /// <summary>
        /// 目标服务器无法访问（主机名无效）
        /// </summary>
        ConnectFail = 4,
        /// <summary>
        /// 连接目标服务器被拒绝
        /// </summary>
        DistReject = 5,
        /// <summary>
        /// TTL已过期
        /// </summary>
        TTLTimeout = 6,
        /// <summary>
        /// 不支持的命令
        /// </summary>
        CommandNotAllow = 7,
        /// <summary>
        /// 不支持的目标服务器地址类型
        /// </summary>
        AddressNotAllow = 8,
        /// <summary>
        /// 未分配
        /// </summary>
        Unknow = 8,
    }
    /// <summary>
    /// socks5相关的消息id
    /// </summary>
    [Flags]
    public enum Socks5MessengerIds : ushort
    {
        /// <summary>
        /// 
        /// </summary>
        Min = 800,
        /// <summary>
        /// 
        /// </summary>
        Request = 802,
        /// <summary>
        /// 
        /// </summary>
        Response = 803,
        /// <summary>
        /// 
        /// </summary>
        GetSetting = 804,
        /// <summary>
        /// 
        /// </summary>
        Setting = 805,
        /// <summary>
        /// 
        /// </summary>
        Max = 899,
    }

}
