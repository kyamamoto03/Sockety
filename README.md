[Japanese Document](README-ja.md)
# Sockety
It is a network framework written in .NET Core.
It wraps Socket and allows for easy server-client communication.
## Communication Method.
When a client connects to a server in Sockety, it connects TCP and UDP.
## Function.
Disconnection detection (internal use)
Client to server communication (with return value from server)
Server to client push communication
Lightweight communication using UDP
Encrypted communication using SSL (TCP only)
UDP communication using AES
Authentication at the time of communication using an authentication token
It is.
[UDP avoids NAT with [HolePunching](https://qiita.com/k-yamamoto/items/1bc295f83c873921b408)

# Development History
Net Core, which requires multi-platform communication, and MagicOnion is used for this purpose.
MagicOnion uses gRPC for its communication core and uses native code. MagicOnion uses gRPC for the communication core and uses native code.
It's not really necessary to run on Windows ARM, but it's for Hololens2.

# QuickStart
server-side

```
int PortNumber = 11000;
//SSL Setting
var serverSetting = new ServerSetting { UseSSL = true }
serverSetting.LoadCertificateFile("++++.pfx", "certPassword");

//Authentication Filter.
var authtificationFilter = new LocalAuthentificationFilter();
serverCore.SocketyFilters.Add(authtificationFilter);

Add(authtificationFilter); //UDP Port Setting
serverCore.InitUDP(11000, 11120);
//Sockety Start
serverCore.Start(localEndPoint: new IPEndPoint(IPAddress.Any, PortNumber);, _stoppingCts: _stoppingCts, parent: this, _serverSetting: serverSetting);
```

```
Client.Connect("localhost",11000, "MyApp",this);
```


Client (calling party) TCP

```
client.Send("Join", Encoding.ASCII.GetBytes($"{DateTime.Now.ToString()}"));
```
you can call a method on the server (in this case, Join) and pass in the current time as an argument.

Server (the one being called) TCP

```
public byte[] Join(ClientInfo sendclientInfo,byte[] JoinDate)
```
The receiving end of the server looks like this
ClientInfo→Source client information
JoinDate→client.Send's second argument

Client (calling party) UDP

```
client.UdpSend(Encoding.ASCII.GetBytes(DateTime.Now.ToString()));
```
Server (the called party) UDP
```
public void UdpReceive(ClientInfo sender, byte[] obj)
{
    //Broadcasting client data to all clients
    serverCore.BroadCastUDPNoReturn(sender, obj);

    string str = Encoding.ASCII.GetString(obj);
}
```

# Package.

```
dotnet add package Sockety
```
Get it at


# Usage.
I will describe the details of how to use it later, but please refer to the Example first.
Sockety (body)
Example/SocketyServer (sample server)
Example/SocketyClient (sample client)
Example/SocketyClientUWP (sample client version)

# Repositoty
[Github](https://github.com/kyamamoto03/Sockety)
[Nuget](https://www.nuget.org/packages/Sockety/)
