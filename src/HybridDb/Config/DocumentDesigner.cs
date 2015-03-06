using System;
using System.Linq.Expressions;
using System.Reflection;

namespace HybridDb.Config
{
    public class DocumentDesigner<TEntity>
    {
        private readonly DocumentDesign design;

        public DocumentDesigner(DocumentDesign design)
        {
            this.design = design;
        }

        public DocumentDesigner<TEntity> With<TMember>(Expression<Func<TEntity, TMember>> projector, bool makeNullSafe = true)
        {
            var name = ColumnNameBuilder.GetColumnNameByConventionFor(projector);
            return With(name, projector, makeNullSafe);
        }

        public DocumentDesigner<TEntity> With<TMember>(string name, Expression<Func<TEntity, TMember>> projector, bool makeNullSafe = true)
        {
            var column = design.Table[name];
            if (column == null)
            {
                column = new Column(name, typeof(TMember));
                design.Table.Register(column);
            }

            Func<object, object> compiledProjector;
            if (makeNullSafe)
            {
                var nullCheckInjector = new NullCheckInjector();
                var nullCheckedProjector = (Expression<Func<TEntity, object>>)nullCheckInjector.Visit(projector);

                if (!nullCheckInjector.CanBeTrustedToNeverReturnNull)
                {
                    column.SqlColumn.Nullable = true;
                }

                compiledProjector = Compile(name, nullCheckedProjector);
            }
            else
            {
                compiledProjector = Compile(name, projector);
            }

            var newProjection = Projection.From<TMember>(compiledProjector);

            if (!newProjection.ReturnType.IsCastableTo(column.Type))
            {
                throw new InvalidOperationException(string.Format(
                    "Can not override projection for {0} of type {1} with a projection that returns {2} (on {3}).",
                    name, column.Type, newProjection.ReturnType, typeof(TEntity)));
            }

            Projection existingProjection;
            if (!design.Projections.TryGetValue(name, out existingProjection))
            {
                if (design.Parent != null)
                {
                    column.SqlColumn.Nullable = true;
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
            extender(new IndexDesigner<TIndex, TEntity>(design));
            return this;
        }

        protected static Func<object, object> Compile<TMember>(string name, Expression<Func<TEntity, TMember>> projector)
        {
            var compiled = projector.Compile();
            return entity =>
            {
                try
                {
                    return (object)compiled((TEntity)entity);
                }
                catch (Exception ex)
                {
                    throw new TargetInvocationException(
                        string.Format("The projector for column {0} threw an exception.\nThe projector code is {1}.", name, projector), ex);
                }
            };
        }
    }
}