namespace VrmImpl.VorticeVulkan;

public class ActionDisposer(Action action) : IDisposable
{
    public void Dispose()
    {
        action();
    }
}
