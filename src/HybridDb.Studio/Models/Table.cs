using HybridDb.Schema;

namespace HybridDb.Studio.Models
{
    public class Table
    {
        public Table(DocumentTable documentTable)
        {
            DocumentTable = documentTable;
        }

        public DocumentTable DocumentTable { get; private set; }

        public string Name
        {
            get { return DocumentTable.Name; }
        }
    }
}