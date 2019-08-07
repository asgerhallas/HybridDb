namespace HybridDb.Migrations.Schema
{
    public abstract class DdlCommand
    {
        protected DdlCommand()
        {
            Unsafe = false;
            RequiresReprojectionOf = null;
        }

        public bool Unsafe { get; protected set; }
        public string RequiresReprojectionOf { get; protected set; }

        public new abstract string ToString();
    }
}