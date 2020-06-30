using Sockety.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sockety.Filter
{
    public interface IAuthenticationFIlter : ISocketyFilter
    {
         bool Authentication(AuthenticationToken token);
    }
}
