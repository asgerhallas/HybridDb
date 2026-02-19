using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Dapper;
using HybridDb.Linq.Old;

namespace HybridDb.Config
{
    public class DocumentDesigner<TEntity>
    {
        readonly DocumentDesign design;
        readonly Configuration configuration;

        public DocumentDesigner(DocumentDesign design, Configuration configuration )
        {
            this.design = design;
            this.configuration = configuration;

            SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
        }

        public DocumentDesigner<TEntity> Key(Func<TEntity, string> projector)
        {
            design.GetKey = entity => projector((TEntity) entity);
            return this;
        }

        public DocumentDesigner<TEntity> With<TMember>(Expression<Func<TEntity, TMember>> projector, params Option[] options) => 
            With(configuration.ColumnNameConvention(projector), projector, x => x, options);

        public DocumentDesigner<TEntity> With<TMember>(string name, Expression<Func<TEntity, TMember>> projector, params Option[] options) => 
            With(name, projector, x => x, options);

        public DocumentDesigner<TEntity> With<TMember, TReturn>(Expression<Func<TEntity, TMember>> projector, Func<TMember, TReturn> converter, params Option[] options) =>
            With(configuration.ColumnNameConvention(projector), projector, converter, options);

        public DocumentDesigner<TEntity> With<TMember, TReturn>(
            string name, Expression<Func<TEntity, TMember>> projector,
            Func<TMember, TReturn> converter,
            params Option[] options)
        {
            if (SqlTypeMap.ForNetType(Nullable.GetUnderlyingType(typeof(TReturn)) ?? typeof(TReturn)) == null)
            {
                if (options.OfType<AsJson>().Any())
                {
                    SqlMapper.AddTypeHandler(new JsonTypeHandler<TReturn>(configuration.Serializer));

                    return With(projector, (x) => configuration.Serializer.Serialize(x), options.Concat(new MaxLength()).ToArray());
                }

                throw new HybridDbException(
                    $"""
                    Invalid configuration. No matching SQL type for '{typeof(TReturn).Name}'.
                    Use option new AsJson() if you want to serialize the projection to JSON in a nvarchar(max) column.
                    """);

            }

            if (typeof (TReturn) == typeof (string))
            {
                options = options.Concat(new MaxLength(850)).ToArray();
            }

            var column = design.Table[name];

            if (DocumentTable.IdColumn.Equals(column))
            {
                throw new ArgumentException("You can not make a projection for IdColumn. Use Document.Key() method instead.");
            }

            if (column == null)
            {
                var lengthOption = options
                    .OfType<MaxLength>()
                    .FirstOrDefault();
                
                column = design.Table.Add(new Column<TReturn>(name, lengthOption?.Length));
            }

            Func<object, object> compiledProjector;
            if (!options.OfType<DisableNullCheckInjection>().Any())
            {
                var nullCheckInjector = new NullCheckInjector();
                var nullCheckedProjector = (Expression<Func<TEntity, object>>)nullCheckInjector.Visit(projector);

                if (!nullCheckInjector.CanBeTrustedToNeverReturnNull && !column.IsPrimaryKey)
                {
                    column.Nullable = true;
                }

                compiledProjector = Compile(name, nullCheckedProjector);
            }
            else
            {
                compiledProjector = Compile(name, projector);
            }

            var newProjection = Projection.From<TReturn>(document =>
            {
                var value = compiledProjector(document);

                if (value == null) return null;

                return converter((TMember)value);
            });

            if (!newProjection.ReturnType.IsCastableTo(column.Type))
            {
                throw new InvalidOperationException(
                    $"Can not override projection for {name} of type {column.Type} " +
                    $"with a projection that returns {newProjection.ReturnType} (on {typeof (TEntity)}).");
            }

            if (!design.Projections.TryGetValue(name, out _))
            {
                if (design.Parent != null && !column.IsPrimaryKey)
                {
                    column.Nullable = true;
                }

                design.Projections.Add(column, newProjection);
            }
            else
            {
                design.Projections[name] = newProjection;
            }

            return this;
        }

        public DocumentDesigner<TEntity> Extend<TIndex>(Action<IndexDesigner<TIndex, TEntity>> extender)
        {
            extender(new IndexDesigner<TIndex, TEntity>(design, configuration));
            return this;
        }

        protected static Func<object, object> Compile<TMember>(string name, Expression<Func<TEntity, TMember>> projector)
        {
            var compiled = projector.Compile();

            return entity =>
            {
                try
                {
                    return compiled((TEntity)entity);
                }
                catch (Exception ex)
                {
                    throw new TargetInvocationException(
                        $"The projector for column {name} threw an exception.\nThe projector code is {projector}.", ex);
                }
            };
        }
    }
}