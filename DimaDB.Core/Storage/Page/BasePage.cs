namespace DimaDB.Core.Storage.Page;

public abstract class BasePage
{
    public const int PageSize = 4096;
    protected byte[] _buffer = new byte[4096];

    public abstract byte[] ToBytes();
}