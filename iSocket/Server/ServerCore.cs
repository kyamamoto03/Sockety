using iSocket.Model;
using MessagePack;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace iSocket.Server
{
    public class ServerCore<T> : IDisposable
    {
        #region IDisposable
        public void Dispose()
        {
            if (MainListener != null && MainListener.Connected == true)
            {
                MainListener.Disconnect(false);
                MainListener = null;
            }
        }
        #endregion

        private Socket MainListener;
        private T Parent;

        public void Start(IPEndPoint localEndPoint, CancellationTokenSource _stoppingCts,T parent)
        {
            Parent = parent;
            // メイン接続のTCP/IPを作成
            MainListener = new Socket(localEndPoint.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp);

            MainListener.Bind(localEndPoint);
            MainListener.Listen(10);

            Task.Run(async () => { 
                while (!_stoppingCts.IsCancellationRequested)
                {
                    try
                    {
                        Console.WriteLine("Waiting for a connection...");
                        Socket handler = await MainListener.AcceptAsync();
                        //クライアント情報を受信
                        var clientInfo = ClientInfoReceive(handler);
                        if (ClientInfoManagement(clientInfo) == false)
                        {
                            Console.WriteLine($"ClientInfo ClientID:{clientInfo.ClientID} Name:{clientInfo.Name}");
                        }
                        else
                        {
                            Console.WriteLine($"ReConnect ClientInfo ClientID:{clientInfo.ClientID} Name:{clientInfo.Name}");
                        }

                        // クライアントが接続したので、受付スレッドを開始する
                        var clientHub = new ClientHub<T>(handler, clientInfo,Parent);
                        clientHub.Run();

                        ISocketClient<T>.GetInstance().ClientHubs.Add(clientHub);
                    }catch(Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }
                }
            });
        }

        private bool ClientInfoManagement(ClientInfo clientInfo)
        {
            var clients = ISocketClient<T>.GetInstance().ClientHubs;

            if (clients.Any(x => x.ClientInfo.ClientID == clientInfo.ClientID) == true)
            {
                //すでにClientIDがあるので再接続
                return false;
            }
            return true;
        }

        public void BroadCastNoReturn(string ClientMethodName,byte[] data)
        {
            ISocketClient<T>.GetInstance().ClientHubs.ForEach(x =>
            {
                x.SendNonReturn(ClientMethodName, data);
            });
        }

        private ClientInfo ClientInfoReceive(Socket handler)
        {
            byte[] bytes = new Byte[1024];
            handler.Receive(bytes);
            ClientInfo clientInfo = MessagePackSerializer.Deserialize<ClientInfo>(bytes);

            return clientInfo;
        }
    }
}
