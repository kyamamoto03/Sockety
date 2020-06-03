namespace Sockety.Model
{
    public interface IService
    {
        void UdpReceive(ClientInfo clientInfo, byte[] obj);
    }
}
