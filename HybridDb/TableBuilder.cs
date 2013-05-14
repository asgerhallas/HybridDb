using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using HybridDb.Schema;

namespace HybridDb
{
    public class TableBuilder<TEntity>
    {
        readonly Table table;

        public TableBuilder(Table table)
        {
            this.table = table;
        }

        public TableBuilder<TEntity> WithProjection<TMember>(Expression<Func<TEntity, TMember>> member)
        {
            var expression = member.ToString();
            var name = string.Join("", expression.Split('.').Skip(1));
            return WithProjection(name, member);
        }

        public TableBuilder<TEntity> WithProjection<TMember>(string columnName, Expression<Func<TEntity, TMember>> member)
        {
            var column = new ProjectionColumn<TEntity, TMember>(columnName, member.Compile());
            table.AddProjection(column);
            return this;
        }

        public TableBuilder<TEntity> WithProjection<TMember>(string columnName, Expression<Func<TEntity, IEnumerable<TMember>>> member)
        {
            var column = new CollectionProjectionColumn<TEntity, TMember>(columnName, member.Compile());
            table.AddProjection(column);
            return this;
        }

        //string ExtractSensibleColumnName<TMember>(Expression<Func<TEntity, TMember>> projection)
        //{
        //    var body = projection.Body;


        //}

        //Expression<Func<TEntity, TMember>> MakeNullSafe<TMember>(Expression<Func<TEntity, TMember>> projection)
        //{
        //    var body = projection.Body;

            
        //}
    }
}