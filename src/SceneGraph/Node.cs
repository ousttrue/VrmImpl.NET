using System.Numerics;

namespace VrmImpl.SceneGraph;

public class Node
{
    public string Name = "";
    private readonly List<Node> _children = [];
    public IReadOnlyList<Node> Children => _children;
    public Node? Parent = default;
    public Vector3 Translation = Vector3.Zero;
    public Quaternion Rotation = Quaternion.Identity;
    public Vector3 Scale = Vector3.One;
    public Matrix4x4 LocalTransform =>
        Matrix4x4.Multiply(
            Matrix4x4.CreateScale(Scale),
            Matrix4x4.Multiply(
                Matrix4x4.CreateFromQuaternion(Rotation),
                Matrix4x4.CreateTranslation(Translation)
            )
        );
    public Matrix4x4 WorldTransform = Matrix4x4.Identity;
    public Drawlist.Mesh? Mesh = default;
    public Drawlist.Skin? Skin = default;

    public void RemoveChild(Node child)
    {
        _children.Remove(child);
        child.Parent = null;
    }

    public void AddChild(Node child)
    {
        if (child.Parent is Node parent)
        {
            parent.RemoveChild(child);
        }
        _children.Add(child);
        child.Parent = this;
    }

    public void CalcWorld(Matrix4x4 parent)
    {
        WorldTransform = Matrix4x4.Multiply(LocalTransform, parent);
        foreach (var child in Children)
        {
            child.CalcWorld(WorldTransform);
        }
    }
}
