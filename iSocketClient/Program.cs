using System;
using System.Text;
using iSocket.Client;

namespace iSocketClient
{
    class Program
    {
        static void Main(string[] args)
        {
            Work w = new Work();
            w.Start();
        }
    }
    class Work 
    {
        public void UserJoin(byte[] packet)
        {
            Console.WriteLine("{0}",
                    System.Text.Encoding.ASCII.GetString(packet, 0, packet.Length));
        }
        public void Push(byte[] a)
        {
            Console.WriteLine("Push!");
        }

        public void Start()
        {
            var client = new Client<Work>();


            ///再接続処理
            client.ConnectionReset = () =>
            {
                while (true)
                {
                    if (client.ReConnect() == true)
                    {
                        return;
                    }
                }
            };

            client.Connect("192.168.2.12", 11000,"ConsoleApp",this);
            while(true)
            {
                var str = DateTime.Now.ToString();
                var ret = client.clientReceiver.Send("Echo", Encoding.ASCII.GetBytes(str));
                var retstr = Encoding.ASCII.GetString(ret, 0, ret.Length);
                Console.WriteLine(retstr);

                System.Threading.Thread.Sleep(2000);
            }
        }

    }


}
