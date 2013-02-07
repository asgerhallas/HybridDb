using System;
using System.Linq.Expressions;
using HybridDb.Schema;

namespace HybridDb
{
    public class TableBuilder<TEntity>
    {
        readonly ITable table;

        public TableBuilder(ITable table)
        {
            this.table = table;
        }

        public TableBuilder<TEntity> WithProjection<TMember>(Expression<Func<TEntity, TMember>> member)
        {
            var column = new ProjectionColumn<TEntity, TMember>(member);
            table.AddProjection(column);
            return this;
        }
    }
}