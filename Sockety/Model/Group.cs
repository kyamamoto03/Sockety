using MessagePack;
using System;

namespace Sockety.Model
{
    [MessagePackObject]
    public class Group
    {
        [Key(0)]
        public Guid GroupID { get; private set; }


        public static Group Create()
        {
            Group ret = new Group();
            ret.GroupID = Guid.NewGuid();

            return ret;
        }
    }
}
