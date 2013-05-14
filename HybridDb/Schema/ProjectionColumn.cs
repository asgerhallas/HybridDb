using System;
using System.Collections;
using System.Collections.Generic;

namespace HybridDb.Schema
{
    public abstract class ProjectionColumn : Column
    {
        public abstract object GetValue(object document);
    }

    public class ProjectionColumn2 : ProjectionColumn
    {
        readonly Func<object, object> projector;

        public ProjectionColumn2(string columnName, Type type, Func<object, object> projector)
        {
            this.projector = projector;

            Name = columnName;
            SqlColumn = new SqlColumn(type);
        }

        public override object GetValue(object document)
        {
            return projector(document);
        }
    }

    public class ProjectionColumn<TEntity, TMember> : ProjectionColumn
    {
        readonly Func<TEntity, TMember> projector;

        public ProjectionColumn(string columnName, Func<TEntity, TMember> projector)
        {
            this.projector = projector;
            
            Name = columnName;
            SqlColumn = new SqlColumn(typeof(TMember));
        }

        public override object GetValue(object document)
        {
            return projector((TEntity)document);
        }
    }

    public abstract class CollectionProjectionColumn : ProjectionColumn
    {
        public abstract IProjectionTable Table { get; }
    }

    public class CollectionProjectionColumn<TEntity, TMember> : CollectionProjectionColumn
    {
        readonly Func<TEntity, IEnumerable<TMember>> projector;

        public CollectionProjectionColumn(string columnName, Func<TEntity, IEnumerable<TMember>> projector)
        {
            this.projector = projector;
            
            Name = columnName;
            SqlColumn = new SqlColumn(typeof(TMember));
        }

        public override object GetValue(object document)
        {
            return projector((TEntity)document);
        }

        public override IProjectionTable Table
        {
            get { throw new NotImplementedException(); }
        }
    }
}