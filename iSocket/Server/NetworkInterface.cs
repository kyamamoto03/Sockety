using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;


namespace iSocket.Server
{
    internal class NetworkInterface
    {
        /// <summary>
        /// IPアドレスを取得(IPV4のみ)
        /// </summary>
        internal static List<IPAddress> IPAddresses
        {
            get
            {
                var rets = new List<IPAddress>();
                // 物理インターフェース情報をすべて取得
                var interfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();

                // 各インターフェースごとの情報を調べる
                foreach (var adapter in interfaces)
                {
                    // 有効なインターフェースのみを対象とする
                    if (adapter.OperationalStatus != OperationalStatus.Up)
                    {
                        continue;
                    }
                    else if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    {
                        continue;
                    }

                    // インターフェースに設定されたIPアドレス情報を取得
                    var properties = adapter.GetIPProperties();

                    // 設定されているすべてのユニキャストアドレスについて
                    foreach (var unicast in properties.UnicastAddresses)
                    {
                        if (unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            // IPv4アドレス
                            rets.Add(unicast.Address);
                        }
                    }
                }

                return rets;
            }
        }

    }
}
