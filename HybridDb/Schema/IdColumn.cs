using System.Data;

namespace HybridDb.Schema
{
    public abstract class Column
    {
        public string Name { get; protected set; }
        public SqlColumn SqlColumn { get; protected set; }

        protected bool Equals(Column other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(Name, other.Name);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Column);
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }
    }

    public class IdColumn : Column
    {
        public IdColumn()
        {
            Name = "Id";
            SqlColumn = new SqlColumn(DbType.Guid, isPrimaryKey: true);
        }

        public object GetValue(object document)
        {
            return ((dynamic) document).Id;
        }
    }
}