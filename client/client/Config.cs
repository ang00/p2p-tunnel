﻿using common.libs;
using common.libs.database;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace client
{
    /// <summary>
    /// 配置信息
    /// </summary>
    [Table("appsettings")]
    public sealed class Config
    {
        /// <summary>
        /// 
        /// </summary>
        public Config() { }
        private readonly IConfigDataProvider<Config> configDataProvider;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="configDataProvider"></param>
        public Config(IConfigDataProvider<Config> configDataProvider)
        {
            this.configDataProvider = configDataProvider;

            Config config = ReadConfig().Result;
            Client = config.Client;
            Server = config.Server;

            if (Client.Name.Length == 0)
            {
                Client.Name = $"{Environment.MachineName}_{Environment.UserName}";
            }
        }
        /// <summary>
        /// 客户端配置
        /// </summary>
        public ClientConfig Client { get; set; } = new ClientConfig();
        /// <summary>
        /// 服务器配置
        /// </summary>
        public ServerConfig Server { get; set; } = new ServerConfig();

        /// <summary>
        /// 读取
        /// </summary>
        /// <returns></returns>
        public async Task<Config> ReadConfig()
        {
            return await configDataProvider.Load();
        }
        public async Task SaveConfig(Config config)
        {
            await configDataProvider.Save(config).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 客户端配置
    /// </summary>
    public sealed class ClientConfig
    {
        /// <summary>
        /// 短id
        /// </summary>
        public byte ShortId { get; set; }
        /// <summary>
        /// 分组编号
        /// </summary>
        public string GroupId { get; set; } = string.Empty;
        /// <summary>
        /// 分组待选列表
        /// </summary>
        public string[] GroupIds { get; set; } = Array.Empty<string>();

        /// <summary>
        /// 客户端名
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// 参数
        /// </summary>
        public Dictionary<string,string> Args { get; set; } = new Dictionary<string, string>();
        /// <summary>
        /// 自动注册
        /// </summary>
        public bool AutoReg { get; set; } = false;

        /// <summary>
        /// 使用ipv6
        /// </summary>
        public bool UseIpv6 { get; set; } = true;
        /// <summary>
        /// TcpBufferSize
        /// </summary>
        public int TcpBufferSize { get; set; } = 128 * 1024;
        /// <summary>
        /// 加密
        /// </summary>
        public bool Encode { get; set; } = false;
        /// <summary>
        /// 加密密码  32位
        /// </summary>
        public string EncodePassword { get; set; } = string.Empty;
        /// <summary>
        /// 掉线超时
        /// </summary>
        public int TimeoutDelay { get; set; } = 20000;
        /// <summary>
        /// 自动打洞
        /// </summary>
        public bool UsePunchHole { get; set; } = true;
        /// <summary>
        /// 使用udp
        /// </summary>
        public bool UseUdp { get; set; } = true;
        /// <summary>
        /// 使用tcp
        /// </summary>
        public bool UseTcp { get; set; } = true;
        /// <summary>
        /// 中继节点
        /// </summary>
        public bool UseRelay { get; set; } = true;
        public bool AutoRelay { get; set; } = true;
        /// <summary>
        /// 自动重连
        /// </summary>
        public bool UseReConnect { get; set; } = true;
        /// <summary>
        /// udp限速
        /// </summary>
        public int UdpUploadSpeedLimit { get; set; }

        public string[] Services { get; set; } = Array.Empty<string>();

        /// <summary>
        /// 绑定ip
        /// </summary>
        [JsonIgnore]
        public IPAddress BindIp
        {
            get
            {
                return UseIpv6 && NetworkHelper.IPv6Support ? IPAddress.IPv6Any : IPAddress.Any;
            }
        }

        /// <summary>
        /// 本地ip
        /// </summary>
        [JsonIgnore]
        public IPAddress LoopbackIp
        {
            get
            {
                return UseIpv6 && NetworkHelper.IPv6Support ? IPAddress.IPv6Loopback : IPAddress.Loopback;
            }
        }

        /// <summary>
        /// 获取客户端配置权限
        /// </summary>
        /// <returns></returns>
        public EnumClientAccess GetAccess()
        {
            return EnumClientAccess.None
                | (UseUdp ? EnumClientAccess.UseUdp : EnumClientAccess.None)
                | (UseTcp ? EnumClientAccess.UseTcp : EnumClientAccess.None)
                | (UsePunchHole ? EnumClientAccess.UsePunchHole : EnumClientAccess.None)
                | (UseRelay ? EnumClientAccess.UseRelay : EnumClientAccess.None)
                | (AutoRelay ? EnumClientAccess.UseAutoRelay : EnumClientAccess.None);

        }
    }

    public sealed class ServerItem
    {
        public string Img { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Ip { get; set; } = string.Empty;
        public ushort UdpPort { get; set; } = 5410;
        public ushort TcpPort { get; set; } = 59410;
    }

    /// <summary>
    /// 客户端权限类别
    /// </summary>
    [Flags]
    public enum EnumClientAccess : uint
    {
        /// <summary>
        /// 无
        /// </summary>
        None = 0,
        /// <summary>
        /// 使用udp
        /// </summary>
        UseUdp = 1,
        /// <summary>
        /// 使用tcp
        /// </summary>
        UseTcp = 2,
        /// <summary>
        /// 自动打洞
        /// </summary>
        UsePunchHole = 4,
        /// <summary>
        /// 中继节点
        /// </summary>
        UseRelay = 8,
        UseAutoRelay = 16,
        /// <summary>
        /// 全部
        /// </summary>
        All = 0xffffffff
    }

    /// <summary>
    /// 服务器配置
    /// </summary>
    public sealed class ServerConfig
    {
        /// <summary>
        /// ip
        /// </summary>
        public string Ip { get; set; } = string.Empty;
        /// <summary>
        /// udp端口
        /// </summary>
        public int UdpPort { get; set; } = 8099;
        /// <summary>
        /// tcp端口
        /// </summary>
        public int TcpPort { get; set; } = 8000;
        /// <summary>
        /// 加密
        /// </summary>
        public bool Encode { get; set; } = false;
        /// <summary>
        /// 加密密码 32位
        /// </summary>
        public string EncodePassword { get; set; } = string.Empty;

        /// <summary>
        /// 服务器选项列表
        /// </summary>
        public ServerItem[] Items { get; set; } = Array.Empty<ServerItem>();
    }
}
