namespace HybridDb.Migrations.Schema
{
    public abstract class DdlCommand
    {
        protected DdlCommand()
        {
            Safe = false;
            RequiresReprojectionOf = null;
        }

        public bool Safe { get; protected set; }
        public string RequiresReprojectionOf { get; protected set; }

        public abstract void Execute(DocumentStore store);

        public new abstract string ToString();
    }
}