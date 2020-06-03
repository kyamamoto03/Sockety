using System;
using System.Collections.Generic;
using System.Text;

namespace Sockety.Model
{
    public class SocketyException : Exception
    {
        public enum SOCKETY_EXCEPTION_ERROR
        {
            BUFFER_OVER
        }

        public SOCKETY_EXCEPTION_ERROR SocketyErrorCode { get; private set; }

        public SocketyException(SOCKETY_EXCEPTION_ERROR socketyErrorCode)
        {
            SocketyErrorCode = socketyErrorCode;
        }
    }
}
