using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace HybridDb
{
    //public class TableBuilder<TEntity>
    //{
    //    readonly DocumentConfiguration table;

    //    public TableBuilder(DocumentConfiguration table)
    //    {
    //        this.table = table;
    //    }

    //    public TableBuilder<TEntity> Project<TMember>(Expression<Func<TEntity, TMember>> member)
    //    {
    //        var expression = member.ToString();
    //        var name = string.Join("", expression.Split('.').Skip(1));
    //        return Project(name, member);
    //    }

    //    public TableBuilder<TEntity> Project<TMember>(string columnName, Expression<Func<TEntity, TMember>> member)
    //    {
    //        //var column = new UserColumn<TEntity, TMember>(columnName, projector.Compile());
    //        //table.AddProjection(column);
    //        return this;
    //    }

    //    public TableBuilder<TEntity> Project<TMember>(string columnName, Expression<Func<TEntity, IEnumerable<TMember>>> member)
    //    {
    //        //var column = new CollectionProjectionColumn<TEntity, TMember>(columnName, projector.Compile());
    //        //table.AddProjection(column);
    //        return this;
    //    }

    //    //string ExtractSensibleColumnName<TMember>(Expression<Func<TEntity, TMember>> projection)
    //    //{
    //    //    var body = projection.Body;


    //    //}

    //    //Expression<Func<TEntity, TMember>> MakeNullSafe<TMember>(Expression<Func<TEntity, TMember>> projection)
    //    //{
    //    //    var body = projection.Body;

            
    //    //}
    //}
}