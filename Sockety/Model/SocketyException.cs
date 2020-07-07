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
            NO_CLIENT,
            ERROR,
            /// <summary>
            /// サーバのレスポンスが一定期間(SocketySetting.SERVER_RESPONSE_TIME_OUT)なかった時のエラー
            /// </summary>
            SERVER_RESPONSE_ERROR
        }

        public SOCKETY_EXCEPTION_ERROR SocketyErrorCode { get; private set; }

        public SocketyException(SOCKETY_EXCEPTION_ERROR socketyErrorCode)
        {
            SocketyErrorCode = socketyErrorCode;
        }
    }
}
