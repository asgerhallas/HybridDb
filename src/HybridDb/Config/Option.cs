namespace HybridDb.Config
{
    public interface Option
    {

    }

    public interface Option<in T> : Option
    {
        
    }
}