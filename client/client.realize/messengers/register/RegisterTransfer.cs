﻿using client.messengers.clients;
using client.messengers.singnin;
using client.realize.messengers.crypto;
using common.libs;
using common.libs.extends;
using common.server;
using common.server.model;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace client.realize.messengers.singnin
{
    public sealed class SignInTransfer : ISignInTransfer
    {
        private readonly SignInMessengerSender signinMessengerSender;
        private readonly ITcpServer tcpServer;
        private readonly IUdpServer udpServer;
        private readonly Config config;
        private readonly SignInStateInfo signInState;
        private readonly CryptoSwap cryptoSwap;
        private readonly IIPv6AddressRequest iPv6AddressRequest;
        private CancellationTokenSource cancellationToken = null;
        private int lockObject = 0;

        public SignInTransfer(
            SignInMessengerSender signinMessengerSender,
            ITcpServer tcpServer, IUdpServer udpServer,
            Config config, SignInStateInfo signInState,
            CryptoSwap cryptoSwap, IIPv6AddressRequest iPv6AddressRequest
        )
        {
            this.signinMessengerSender = signinMessengerSender;
            this.tcpServer = tcpServer;
            this.udpServer = udpServer;
            this.config = config;
            this.signInState = signInState;
            this.cryptoSwap = cryptoSwap;
            this.iPv6AddressRequest = iPv6AddressRequest;

            AppDomain.CurrentDomain.ProcessExit += (s, e) => Exit();

            tcpServer.OnDisconnect += (connection) => Disconnect(connection, signInState.Connection);
        }
        private void Disconnect(IConnection connection, IConnection regConnection)
        {
            if (IConnection.Equals(connection, regConnection) == false || signInState.LocalInfo.IsConnecting)
            {
                return;
            }

            Logger.Instance.Error($"{connection.ServerType} signin 断开~~~~${connection.Address}");
            if (Interlocked.CompareExchange(ref lockObject, 1, 0) == 0)
            {
                SignIn(true).ContinueWith((result) =>
                {
                    Interlocked.Exchange(ref lockObject, 0);
                });
            }
        }

        public void Exit()
        {
            if (cancellationToken != null && cancellationToken.IsCancellationRequested == false)
            {
                cancellationToken.Cancel();
            }
            Exit1();
        }
        private void Exit1()
        {
            signInState.Offline();
            udpServer.Stop();
            tcpServer.Stop();
            GCHelper.FlushMemory();
        }

        /// <summary>
        /// 注册
        /// </summary>
        /// <param name="autoReg">强行自动注册</param>
        /// <returns></returns>
        public async Task<CommonTaskResponseInfo<bool>> SignIn(bool autoReg = false)
        {
            cancellationToken = new CancellationTokenSource();
            CommonTaskResponseInfo<bool> success = new CommonTaskResponseInfo<bool> { Data = false, ErrorMsg = string.Empty };
            if (signInState.LocalInfo.IsConnecting)
            {
                success.ErrorMsg = "注册操作中...";
                return success;
            }

            return await Task.Run(async () =>
            {
                double interval = autoReg ? 5000 : 0;
                int times = autoReg ? 10000 : 2;
                for (int i = 0; i < times; i++)
                {
                    try
                    {
                        if (signInState.LocalInfo.IsConnecting)
                        {
                            success.ErrorMsg = "注册操作中...";
                            Logger.Instance.Error(success.ErrorMsg);
                            break;
                        }

                        //先退出
                        Exit1();

                        Logger.Instance.Info($"开始注册");
                        signInState.LocalInfo.IsConnecting = true;

                        IPAddress serverAddress = NetworkHelper.GetDomainIp(config.Server.Ip);
                        signInState.LocalInfo.Port = NetworkHelper.GetRandomPort();
                        config.Client.UseIpv6 = serverAddress.AddressFamily == AddressFamily.InterNetworkV6;
                        signInState.LocalInfo.Ipv6s = iPv6AddressRequest.GetIPV6();
                        signInState.OnBind?.Invoke(true);

                        TcpBind(serverAddress);
                        //交换密钥
                        await SwapCryptoTcp();


                        //注册
                        SignInResult result = await signinMessengerSender.SignIn().ConfigureAwait(false);
                        if (result.NetState.Code != MessageResponeCodes.OK)
                        {
                            Logger.Instance.Error($"注册失败，网络问题:{result.NetState.Code.GetDesc((byte)result.NetState.Code)}");
                            Logger.Instance.Error(success.ErrorMsg);
                            signInState.LocalInfo.IsConnecting = false;
                            await Task.Delay((int)interval, cancellationToken.Token);
                            continue;
                        }
                        if (result.Data.Code != SignInResultInfo.SignInResultInfoCodes.OK)
                        {
                            success.ErrorMsg = $"注册失败:{result.Data.Code.GetDesc((byte)result.Data.Code)}";
                            Logger.Instance.Error(success.ErrorMsg);
                            signInState.LocalInfo.IsConnecting = false;
                            break;
                        }


                        config.Client.ShortId = result.Data.ShortId;
                        config.Client.GroupId = result.Data.GroupId;
                        signInState.RemoteInfo.Access = result.Data.UserAccess;
                        signInState.Online(result.Data.ConnectionId, result.Data.Ip);
                        await signinMessengerSender.Notify().ConfigureAwait(false);

                        success.ErrorMsg = "注册成功~";
                        success.Data = true;
                        Logger.Instance.Debug(success.ErrorMsg);
                        break;
                    }
                    catch (TaskCanceledException tex)
                    {
                        Logger.Instance.DebugError(tex + "");
                        success.ErrorMsg = tex.Message;
                        signInState.LocalInfo.IsConnecting = false;
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.DebugError(ex + "");
                        success.ErrorMsg = ex.Message;
                        signInState.LocalInfo.IsConnecting = false;
                        await Task.Delay((int)interval, cancellationToken.Token);
                        continue;
                    }
                }
                if(success.Data == false)
                {
                    Exit1();
                }
                return success;
            });
        }
        private void TcpBind(IPAddress serverAddress)
        {
            //TCP 本地开始监听
            tcpServer.SetBufferSize(config.Client.TcpBufferSize);
            tcpServer.Start(signInState.LocalInfo.Port);

            //TCP 连接服务器
            IPEndPoint bindEndpoint = new IPEndPoint(config.Client.BindIp, signInState.LocalInfo.Port);
            Socket tcpSocket = new(bindEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            tcpSocket.KeepAlive(time: config.Client.TimeoutDelay / 1000 / 5);
            tcpSocket.IPv6Only(config.Client.BindIp.AddressFamily, false);
            tcpSocket.ReuseBind(bindEndpoint);
            tcpSocket.Connect(new IPEndPoint(serverAddress, config.Server.TcpPort));
            signInState.LocalInfo.LocalIp = (tcpSocket.LocalEndPoint as IPEndPoint).Address;
            signInState.Connection = tcpServer.BindReceive(tcpSocket, config.Client.TcpBufferSize);
        }
        private async Task SwapCryptoTcp()
        {
            if (config.Server.Encode == false)
            {
                return;
            }
            ICrypto crypto = await cryptoSwap.Swap(signInState.Connection, config.Server.EncodePassword);
            if (crypto == null)
            {
                throw new Exception("注册交换密钥失败，如果客户端设置了密钥，则服务器必须设置相同的密钥，如果服务器未设置密钥，则客户端必须留空");
            }

            signInState.Connection?.EncodeEnable(crypto);

#if DEBUG
            await cryptoSwap.Test(signInState.Connection);
#endif
        }
    }
}
