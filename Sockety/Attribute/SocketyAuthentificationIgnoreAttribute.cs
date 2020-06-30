using System;
using System.Collections.Generic;
using System.Text;

namespace Sockety.Attribute
{
    /// <summary>
    /// 認証を必要としないメソッドに付ける
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class SocketyAuthentificationIgnoreAttribute : System.Attribute
    {
    }
}
