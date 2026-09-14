using System.Diagnostics.Tracing;
using System.Numerics;

namespace VrmImpl.SceneGraph;

public record Vector3Curve(float[] Inputs, Vector3[] Outputs)
{
    public Vector3 GetValue(double time)
    {
        if (time <= Inputs[0])
        {
            return Outputs[0];
        }
        if (time >= Inputs[Inputs.Length - 1])
        {
            return Outputs[Outputs.Length - 1];
        }
        int i = 1;
        for (; i < Inputs.Length; ++i)
        {
            if (Inputs[i] >= time)
            {
                break;
            }
        }
        return Vector3.Lerp(
            Outputs[i - 1],
            Outputs[i],
            ((float)time - Inputs[i - 1]) / (Inputs[i] - Inputs[i - 1])
        );
    }
}

public record QuaternionCurve(float[] Inputs, Quaternion[] Outputs)
{
    public Quaternion GetValue(double time)
    {
        if (time <= Inputs[0])
        {
            return Outputs[0];
        }
        if (time >= Inputs[Inputs.Length - 1])
        {
            return Outputs[Outputs.Length - 1];
        }
        int i = 1;
        for (; i < Inputs.Length; ++i)
        {
            if (Inputs[i] >= time)
            {
                break;
            }
        }
        return Quaternion.Slerp(
            Outputs[i - 1],
            Outputs[i],
            ((float)time - Inputs[i - 1]) / (Inputs[i] - Inputs[i - 1])
        );
    }
}

public record class NodeAnimation(Node Target)
{
    public Vector3Curve? T;
    public QuaternionCurve? R;
    public Vector3Curve? S;

    public double GetDuration()
    {
        var duration = 0.0;
        if (T is not null && T.Inputs.Last() > duration)
        {
            duration = T.Inputs.Last();
        }
        if (R is not null && R.Inputs.Last() > duration)
        {
            duration = R.Inputs.Last();
        }
        if (S is not null && S.Inputs.Last() > duration)
        {
            duration = S.Inputs.Last();
        }
        return duration;
    }

    public void SetTime(double elapsed)
    {
        if (T is Vector3Curve translationCurve)
        {
            var t = translationCurve.GetValue(elapsed);
            Target.Translation = t;
        }
        if (R is QuaternionCurve rotationCurve)
        {
            var r = rotationCurve.GetValue(elapsed);
            Target.Rotation = r;
        }
        if (S is Vector3Curve scaleCurve)
        {
            var s = scaleCurve.GetValue(elapsed);
            Target.Scale = s;
        }
    }
}

public class Animation
{
    public readonly List<NodeAnimation> NodeAnimations = [];
    private double _elapsed = 0;
    private double _duration = 0;

    public void CalcDuration()
    {
        foreach (var nodeAnimation in NodeAnimations)
        {
            var duration = nodeAnimation.GetDuration();
            if (duration > _duration)
            {
                _duration = duration;
            }
        }
    }

    public void UpdateDelta(double delta)
    {
        _elapsed += delta;
        while (_duration > 0 && _elapsed > _duration)
        {
            _elapsed -= _duration;
        }
        foreach (var nodeAnimation in NodeAnimations)
        {
            nodeAnimation.SetTime(_elapsed);
        }
    }
}
