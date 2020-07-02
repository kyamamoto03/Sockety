namespace Sockety
{
    class SocketySetting
    {
        /// <summary>
        /// 内部バッファサイズ
        /// </summary>
        public static int MAX_BUFFER = 1000000;

        /// <summary>
        /// UDPの送信最大サイズ
        /// </summary>
        public static int MAX_UDP_SIZE = 2000;
        /// <summary>
        /// UDPのバケット分割サイズ
        /// </summary>
        public static int UDP_SPLIT_SIZE = 30000;

        public static int HEART_BEAT_LOST_TIME = 10000;
    }
}
