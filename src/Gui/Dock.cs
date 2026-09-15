namespace VrmImpl.Gui;

public delegate void DrawDelegate(ref bool p_open);

public class Dock(string name, string shortCut, DrawDelegate show)
{
    public readonly string MenuLabel = name;
    public readonly string MenuShortCut = shortCut;
    public readonly DrawDelegate Show = show;
    public bool IsOpen = true;

    public void Draw()
    {
        if (IsOpen)
        {
            Show(ref IsOpen);
        }
    }
}

public delegate void DrawDelegate<T>(ref bool p_open, T frameData);

public class Dock<T>(string name, string shortCut, DrawDelegate<T> show)
{
    public readonly string MenuLabel = name;
    public readonly string MenuShortCut = shortCut;
    public readonly DrawDelegate<T> Show = show;
    public bool IsOpen = true;

    public void Draw(T frameData)
    {
        if (IsOpen)
        {
            Show(ref IsOpen, frameData);
        }
    }
}
