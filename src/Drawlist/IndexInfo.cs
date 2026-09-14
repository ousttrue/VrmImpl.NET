namespace VrmImpl.Drawlist;

public record struct IndexInfo(uint IndexUnit, byte[] Data)
{
    public int Count
    {
        get
        {
            switch (IndexUnit)
            {
                case 1:
                    return Data.Length;
                case 2:
                    return Data.Length / 2;
                case 4:
                    return Data.Length / 4;
                default:
                    throw new Exception();
            }
        }
    }
}
