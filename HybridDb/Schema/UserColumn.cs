using System;
using System.Collections;
using System.Collections.Generic;

namespace HybridDb.Schema
{
    public class UserColumn : Column
    {
        public UserColumn(string columnName, SqlColumn sqlColumn)
        {
            Name = columnName;
            SqlColumn = sqlColumn;
        }

        public UserColumn(string name, Type type)
        {
            Name = name;
            SqlColumn = new SqlColumn(type);
        }

        public UserColumn(string name)
        {
            Name = name;
            SqlColumn = new SqlColumn();
        }
    }

    //public class CollectionProjectionColumn : UserColumn
    //{
    //    public CollectionProjectionColumn(string columnName, SqlColumn sqlColumn) : base(columnName, sqlColumn) {}
    //    public IProjectionTable Table { get; }
    //}

    //public class CollectionProjectionColumn<TEntity, TMember> : CollectionProjectionColumn
    //{
    //    readonly Func<TEntity, IEnumerable<TMember>> projector;

    //    public CollectionProjectionColumn(string columnName, Func<TEntity, IEnumerable<TMember>> projector)
    //    {
    //        this.projector = projector;
            
    //        Name = columnName;
    //        SqlColumn = new SqlColumn(typeof(TMember));
    //    }

    //    //public override object GetValue(object document)
    //    //{
    //    //    return projector((TEntity)document);
    //    //}
    //}
}