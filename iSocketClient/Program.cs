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
    class Work : Share.IClient
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

            client.Connect("localhost", 11000,this);
            while(true)
            {
                byte[] msg = Encoding.ASCII.GetBytes(DateTime.Now.ToString());
                byte[] response = client.clientReceiver.Send("Echo",msg);
                Console.WriteLine("{0}",
                    System.Text.Encoding.ASCII.GetString(response, 0, response.Length));

                System.Threading.Thread.Sleep(2000);
            }
        }

    }


}
