using System;

namespace Sockety.Model
{
    public class SocketyException : Exception
    {
        public enum SOCKETY_EXCEPTION_ERROR
        {
            /// <summary>
            /// バッファサイズオーバ
            /// </summary>
            BUFFER_OVER,
            /// <summary>
            /// クライアント不明
            /// </summary>
            NO_CLIENT
        }

        public SOCKETY_EXCEPTION_ERROR SocketyErrorCode { get; private set; }

        public SocketyException(SOCKETY_EXCEPTION_ERROR socketyErrorCode)
        {
            SocketyErrorCode = socketyErrorCode;
        }
    }
}
