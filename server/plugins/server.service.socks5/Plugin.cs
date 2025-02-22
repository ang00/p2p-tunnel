﻿using common.libs;
using common.server;
using common.socks5;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace server.service.socks5
{
    public sealed class Plugin : IPlugin
    {
        public void LoadAfter(ServiceProvider services, Assembly[] assemblys)
        {
            Logger.Instance.Warning(string.Empty.PadRight(Logger.Instance.PaddingWidth, '='));
            Logger.Instance.Info("socks5已加载");
            common.socks5.Config config = services.GetService<common.socks5.Config>();
            if (config.ConnectEnable)
            {
                Logger.Instance.Debug($"socks5已允许连接");
            }
            else
            {
                Logger.Instance.Info($"socks5未允许连接");
            }
            Logger.Instance.Warning(string.Empty.PadRight(Logger.Instance.PaddingWidth, '='));
        }

        public void LoadBefore(ServiceCollection services, Assembly[] assemblys)
        {
            services.AddSingleton<common.socks5.Config>();

            services.AddSingleton<ISocks5MessengerSender, Socks5MessengerSender>();
            services.AddSingleton<ISocks5ServerHandler, Socks5ServerHandler>();

            services.AddSingleton<ISocks5Validator, Socks5Validator>();
            services.AddSingleton<ISocks5AuthValidator, Socks5AuthValidator>();
        }
    }

}
