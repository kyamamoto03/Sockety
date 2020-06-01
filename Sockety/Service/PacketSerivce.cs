using Sockety.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sockety.Service
{
    internal class PacketSerivce<T> where T : IService
    {
        T UserClass;

        internal void SetUp(T u)
        {
            UserClass = u;
        }
        /// <summary>
        /// SocketyPacketUDPをSocketySetting.UDP_SPLIT_SIZEサイズで分割する
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        internal static List<SocketyPacketUDP> PacketSplit(ClientInfo clientInfo,byte[] data)
        {
            List<SocketyPacketUDP> ret = new List<SocketyPacketUDP>();
            var packetID = Guid.NewGuid();

            int packetSize = data.Length;
            int index = 0;
            int packetNo = 0;
            do
            {
                var packet = new SocketyPacketUDP { MethodName = "Udp", clientInfo = clientInfo, PacketID = packetID,PacketNo = packetNo + 1 };

                int size;
                if (SocketySetting.UDP_SPLIT_SIZE < data.Length - index)
                {
                    size = SocketySetting.UDP_SPLIT_SIZE;
                    index += SocketySetting.UDP_SPLIT_SIZE;
                }
                else
                {
                    size = data.Length - index;
                }
                byte[] splitBuffer = new byte[size];
                Array.Copy(data, packetNo * SocketySetting.UDP_SPLIT_SIZE, splitBuffer, 0, size);
                packet.PackData = splitBuffer;

                ret.Add(packet);
                packetNo++;
                packetSize -= size;

            } while (packetSize > 0);

            //総数を入れる
            ret.ForEach(x => x.PacketCount = ret.Count);

            return ret;
        }

        /// <summary>
        /// 分割されたSocketyPacketUDPの受信バッファ
        /// </summary>
        private Dictionary<Guid, List<SocketyPacketUDP>> SocketyPacketUDPBuffer = new Dictionary<Guid, List<SocketyPacketUDP>>();

        internal void ReceiverSocketyPacketUDP(SocketyPacketUDP p)
        {
            lock (SocketyPacketUDPBuffer)
            {
                if (SocketyPacketUDPBuffer.ContainsKey(p.PacketID) == false)
                {
                    //新たにPacketを登録
                    var l = new List<SocketyPacketUDP>();
                    l.Add(p);
                    SocketyPacketUDPBuffer.Add(p.PacketID, l);
                }
                else
                {
                    //既存のPacketに追加
                    SocketyPacketUDPBuffer[p.PacketID].Add(p);
                }

                var pakcetList = SocketyPacketUDPBuffer[p.PacketID];
                if (p.PacketCount == pakcetList.Count)
                {
                    //パケットが全部そろったら
                    int length = 0;
                    pakcetList.ForEach(x =>
                    {
                        if (x.PackData != null)
                        {
                            length += x.PackData.Length;
                        }
                    });

                    byte[] data = new byte[length];
                    int i = 0;
                    pakcetList.OrderBy(x => x.PacketNo).ToList().ForEach(x =>
                    {
                        if (x.PackData != null)
                        {
                            Array.Copy(x.PackData, 0, data, i, x.PackData.Length);
                            i += x.PackData.Length;
                        }
                    });
                    SocketyPacketUDPBuffer.Remove(p.PacketID);
                    //データが準備出来たのでユーザクラスをInvokeする
                    UserClass.UdpReceive(pakcetList[0].clientInfo, data);
                }
            }
        }

    }
}
