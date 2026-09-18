using System.IO;

namespace VrmImpl.SceneGraph.Bvh
{
    public class BvhEndSite : BvhNode
    {
        public BvhEndSite() : base("")
        {
        }

        public override void Parse(StringReader r)
        {
            r.ReadLine(); // offset
        }
    }
}
