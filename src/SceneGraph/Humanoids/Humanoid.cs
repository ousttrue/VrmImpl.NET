using System.Numerics;
using VrmImpl.SceneGraph;

public class Humanoid
{
    public record struct HumanBone(
        Node Joint,
        Quaternion InitialGlobalRotation,
        Quaternion InitialLocalRotation
    )
    {
        public static HumanBone Create(Node joint, Quaternion rootWorldInverse)
        {
            Matrix4x4.Decompose(joint.WorldTransform, out var s, out var r, out var t);
            return new(joint, r * rootWorldInverse, joint.Rotation);
        }

        public void Apply(Quaternion srcRotation)
        {
            Joint.Rotation =
                InitialLocalRotation
                * (Quaternion.Inverse(InitialGlobalRotation) * srcRotation * InitialGlobalRotation);
        }
    }

    public readonly HumanBone DstRoot;
    public readonly HumanBone?[] Dst = new HumanBone?[HumanBoneHeadTailPairs.Values.Length];
    public readonly Node SrcRoot;
    public readonly Vector3 SrcHipsInitialPosition = default;
    public readonly Vector3 DstHipsInitialPosition = default;
    public readonly float HipsHeightScale;

    /// <summary>
    /// Dst と Src で必須でないボーンの不一致がありうる。
    /// 1対１の場合は Srcs.Length == 1
    /// src 側にボーンが存在しない場合は Srcs.Length == 0
    /// dst 側にボーンが存在しない場合は後続のボーンが複数の src を受ける
    ///
    ///   dst  src
    /// 0 [o]<-[o]
    /// 1 [o]<-[ ] no effect
    /// 2 [ ]<-[o1]
    ///   [o]<-[o2] = o2 * o1
    /// </summary>
    public readonly Node?[] Src = new Node?[HumanBoneHeadTailPairs.Values.Length];

    public Humanoid(Node dst, Node src)
    {
        src.CalcWorld(Matrix4x4.Identity);
        dst.CalcWorld(Matrix4x4.Identity);

        Matrix4x4.Decompose(dst.WorldTransform, out var ds, out var dr, out var dt);
        var dri = Quaternion.Inverse(dr);
        Matrix4x4.Decompose(src.WorldTransform, out var ss, out var sr, out var st);
        DstRoot = HumanBone.Create(dst.GetHumanBone(HumanBoneType.Hips)!, dri);
        SrcRoot = src;
        DstHipsInitialPosition = dst.GetHumanBone(HumanBoneType.Hips)!.Translation;
        SrcHipsInitialPosition = src.GetHumanBone(HumanBoneType.Hips)!.Translation;
        HipsHeightScale = DstHipsInitialPosition.Y / SrcHipsInitialPosition.Y;
        for (int i = 0; i < HumanBoneHeadTailPairs.Values.Length; ++i)
        {
            var (head, _) = HumanBoneHeadTailPairs.Values[i];

            if (dst.GetHumanBone(head) is Node dstNode)
            {
                Dst[i] = HumanBone.Create(dstNode, dri);
            }
            if (src.GetHumanBone(head) is Node srcNode)
            {
                Src[i] = srcNode;
            }
        }
    }

    public void Process()
    {
        // hips
        {
            var dst = DstRoot;
            var src = SrcRoot;

            dst.Apply(src.Rotation);

            dst.Joint.Translation =
                DstHipsInitialPosition
                + (src.Translation - SrcHipsInitialPosition) * HipsHeightScale;
        }

        for (int i = 0; i < Dst.Length; ++i)
        {
            if (Dst[i] is HumanBone dst)
            {
                if (Src[i] is Node src)
                {
                    dst.Apply(src.Rotation);
                }
            }
        }
    }
}
