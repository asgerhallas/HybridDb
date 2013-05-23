using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace HybridDb.Schema
{
    public class DocumentConfiguration
    {
        protected readonly Configuration configuration;

        public DocumentConfiguration(Configuration configuration, Table table, Type type)
        {
            this.configuration = configuration;
            Table = table;
            Type = type;
            Projections = new Dictionary<Column, Func<object, object>>
            {
                {Table.IdColumn, document => ((dynamic) document).Id}
            };
            UncompiledProjections = new Dictionary<Column, Expression<Func<object, object>>>();
        }

        public Table Table { get; private set; }
        public Type Type { get; private set; }
        public Dictionary<Column, Func<object, object>> Projections { get; private set; }
        public Dictionary<Column, Expression<Func<object, object>>> UncompiledProjections { get; private set; }
    }

    public class DocumentConfiguration<TEntity> : DocumentConfiguration
    {
        public DocumentConfiguration(Configuration configuration, Table table) : base(configuration, table, typeof(TEntity)) { }

        public DocumentConfiguration<TEntity> Project<TMember>(Expression<Func<TEntity, TMember>> projector, bool makeNullSafe = true)
        {
            var name = configuration.GetColumnNameByConventionFor(projector);

            var column = new UserColumn(name, new SqlColumn(typeof(TMember)));
            Table.Register(column);

            if (makeNullSafe)
            {
                var compiled = InjectNullChecks(projector).Compile();
                Projections.Add(column, x => compiled((TEntity)x));
            }
            else
            {
                var compiled = projector.Compile();
                Projections.Add(column, x => (object)compiled((TEntity)x));
            }

            return this;
        }

        public DocumentConfiguration<TEntity> Project<TMember>(Expression<Func<TEntity, IEnumerable<TMember>>> projector, bool makeNullSafe = true)
        {
            return this;
        }

        public static Expression<Func<TModel, object>> InjectNullChecks<TModel, TProperty>(Expression<Func<TModel, TProperty>> expression)
        {
            return (Expression<Func<TModel, object>>)new NullCheckInjector().Visit(expression);
        }
    }
}