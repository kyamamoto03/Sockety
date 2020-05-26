using System;
using iSocket.Client;

namespace iSocketClient
{
    class Program
    {
        static void Main(string[] args)
        {
            var client = new Client();

            client.clientReceiver.ReceiveAction = (x) =>
            {
                Console.WriteLine("{0}",
                    System.Text.Encoding.ASCII.GetString(x.PackData, 0, x.PackData.Length));
            };

            client.Connect("localhost", 11000);
            
        }
    }
}
