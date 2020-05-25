using System;
using iSocket.Client;

namespace iSocketClient
{
    class Program
    {
        static void Main(string[] args)
        {
            var client = new Client();
            client.Connect("localhost", 11000);
            
        }
    }
}
