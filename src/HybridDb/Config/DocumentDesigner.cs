using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using HybridDb.Linq;

namespace HybridDb.Config
{
    public class DocumentDesigner<TEntity>
    {
        readonly DocumentDesign design;
        readonly Func<Expression, string> columnNameConvention;

        public DocumentDesigner(DocumentDesign design, Func<Expression, string> columnNameConvention)
        {
            this.design = design;
            this.columnNameConvention = columnNameConvention;
        }

        public DocumentDesigner<TEntity> Key(Func<TEntity, string> projector)
        {
            design.GetKey = entity => projector((TEntity) entity);
            return this;
        }

        public DocumentDesigner<TEntity> With<TMember>(Expression<Func<TEntity, TMember>> projector, params Option[] options) => 
            With(columnNameConvention(projector), projector, x => x, options);

        public DocumentDesigner<TEntity> With<TMember>(string name, Expression<Func<TEntity, TMember>> projector, params Option[] options) => 
            With(name, projector, x => x, options);

        public DocumentDesigner<TEntity> With<TMember, TReturn>(Expression<Func<TEntity, TMember>> projector, Func<TMember, TReturn> converter, params Option[] options) =>
            With(columnNameConvention(projector), projector, converter, options);


        public DocumentDesigner<TEntity> With<TMember, TReturn>(string name, Expression<Func<TEntity, TMember>> projector, Func<TMember, TReturn> converter, params Option[] options)
        {
            if (typeof (TReturn) == typeof (string))
            {
                options = options.Concat(new MaxLength(1024)).ToArray();
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

                column = new Column<TReturn>(name, lengthOption?.Length);
                design.Table.Add((Column<TReturn>) column);
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
                var value = (TMember) compiledProjector(document);

                if (value == null) return null;

                return converter(value);
            });

            if (!newProjection.ReturnType.IsCastableTo(column.Type))
            {
                throw new InvalidOperationException(
                    $"Can not override projection for {name} of type {column.Type} " +
                    $"with a projection that returns {newProjection.ReturnType} (on {typeof (TEntity)}).");
            }

            Projection existingProjection;
            if (!design.Projections.TryGetValue(name, out existingProjection))
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
            extender(new IndexDesigner<TIndex, TEntity>(design, columnNameConvention));
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