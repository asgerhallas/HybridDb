using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace HybridDb.Schema
{
    public class DocumentConfiguration
    {
        public DocumentConfiguration(Table table, Type type)
        {
            Table = table;
            Type = type;
            Projections = new Dictionary<Column, Func<object, object>>
            {
                {Table.IdColumn, document => ((dynamic) document).Id}
            };
        }

        public Table Table { get; private set; }
        public Type Type { get; private set; }
        public Dictionary<Column, Func<object, object>> Projections { get; private set; }
    }

    public class DocumentConfiguration<TEntity> : DocumentConfiguration
    {
        public DocumentConfiguration(Table table) : base(table, typeof(TEntity)) { }

        public DocumentConfiguration<TEntity> Project<TMember>(Expression<Func<TEntity, TMember>> projector, bool makeNullSafe = true)
        {
            var expression = projector.ToString();
            var name = string.Join("", expression.Split('.').Skip(1));
            return Project(name, projector, makeNullSafe);
        }

        public DocumentConfiguration<TEntity> Project<TMember>(string columnName, Expression<Func<TEntity, TMember>> projector, bool makeNullSafe)
        {
            var column = new UserColumn(columnName, new SqlColumn(typeof(TMember)));
            Table.Register(column);

            if (makeNullSafe) projector = InjectNullChecks(projector);
            var compiledProjector = Cast(projector).Compile();
            Projections.Add(column, compiledProjector);

            return this;
        }

        public DocumentConfiguration<TEntity> Project<TMember>(string columnName, Expression<Func<TEntity, IEnumerable<TMember>>> projector)
        {
            //var column = new UserColumn(columnName, new SqlColumn(typeof(TMember)));
            //Table.Register(column);
            
            //var compiledProjector = Cast(projector).Compile();
            //Projections.Add(column, compiledProjector);

            return this;
        }

        public static Expression<Func<object, object>> Cast<TModel, TProperty>(Expression<Func<TModel, TProperty>> expression)
        {
            var parameterAsObject = Expression.Parameter(typeof (object));

            var wrappedCall =
                Expression.Convert(
                    Expression.Invoke(
                        expression,
                        Expression.Convert(parameterAsObject, typeof (TModel))),
                    typeof (object));

            return Expression.Lambda<Func<object, object>>(wrappedCall, parameterAsObject);
        }

        public static Expression<Func<TModel, TProperty>> InjectNullChecks<TModel, TProperty>(Expression<Func<TModel, TProperty>> expression)
        {
            return (Expression<Func<TModel, TProperty>>) new NullCheckInjector().Visit(expression);
        }
    }
}