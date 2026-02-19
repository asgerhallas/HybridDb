namespace HybridDb.Migrations.Schema
{
    public abstract class DdlCommand
    {
        public bool Safe { get; protected set; } = false;
        public string RequiresReprojectionOf { get; protected set; } = null;

        public abstract void Execute(DocumentStore store);

        public new abstract string ToString();
    }
}