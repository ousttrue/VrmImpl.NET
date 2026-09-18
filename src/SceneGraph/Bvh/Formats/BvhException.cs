using System;

namespace VrmImpl.SceneGraph.Bvh
{
    public class BvhException : Exception
    {
        public BvhException(string msg) : base(msg) { }
    }
}
