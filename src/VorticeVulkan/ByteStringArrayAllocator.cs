using System.Collections;
using System.Runtime.InteropServices;
using System.Text;

namespace VrmImpl.VorticeVulkan;

public unsafe class ByteStringArrayAllocator : IDisposable, IEnumerable
{
    List<string> _list = [];
    byte** _array;

    public IEnumerator GetEnumerator()
    {
        return _list.GetEnumerator();
    }

    public void Deconstruct(out uint x, out byte** y)
    {
        Dispose();

        _array = (byte**)Marshal.AllocHGlobal(sizeof(byte*) * _list.Count);
        for (int i = 0; i < _list.Count; ++i)
        {
            _array[i] = (byte*)Marshal.StringToHGlobalAnsi(_list[i]);
        }
        x = (uint)_list.Count;
        y = _array;
    }

    public void Dispose()
    {
        if (_array != null)
        {
            for (int i = 0; i < _list.Count; ++i)
            {
                Marshal.FreeHGlobal((nint)_array[i]);
            }
            Marshal.FreeHGlobal((nint)_array);
        }
    }

    public void Add(string p)
    {
        _list.Add(p);
    }

    public void Add(nint p)
    {
        _list.Add(Marshal.PtrToStringAnsi(p) ?? throw new Exception());
    }

    public void Add(ReadOnlySpan<byte> p)
    {
        Add(Encoding.UTF8.GetString(p));
    }

    public void AddSpan(ReadOnlySpan<IntPtr> pp)
    {
        foreach (var p in pp)
        {
            Add(p);
        }
    }

    public void AddSpan(byte** _pp, uint count)
    {
        var pp = (IntPtr*)_pp;
        AddSpan(new ReadOnlySpan<nint>(pp, (int)count));
    }
}
