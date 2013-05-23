using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;

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

            var finalProjector = makeNullSafe 
                ? Compile(name, InjectNullChecks(projector)) 
                : Compile(name, projector);
            
            Projections.Add(column, finalProjector);

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

        Func<object, object> Compile<TMember>(string name, Expression<Func<TEntity, TMember>> func)
        {
            return x =>
            {
                try
                {
                    var compiled = func.Compile();
                    return (object) compiled((TEntity) x);
                }
                catch (Exception ex)
                {
                    throw new TargetInvocationException(
                        string.Format("The projector for column {0} threw an exception.\nThe projector code is {1}.", name, func), ex);
                }
            };
        }
    }
}