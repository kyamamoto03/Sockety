using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sockety.Filter
{
    public class SocketyFilters
    {
        public List<ISocketyFilter> Filters { get; } = new List<ISocketyFilter>();

        public void Add(ISocketyFilter f)
        {
            Filters.Add(f);
        }
        public void Remoce(ISocketyFilter f)
        {
            Filters.Remove(f);
        }

        public T Get<T>()
        {
            foreach(var f in Filters)
            {
                if(f is T)
                {
                    return (T)f;
                }
            }

            return default(T); ;
        }
    }
}
