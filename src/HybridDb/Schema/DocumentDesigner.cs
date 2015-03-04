using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace HybridDb.Schema
{
    public class DocumentDesigner<TEntity>
    {
        private readonly DocumentDesign design;

        public DocumentDesigner(DocumentDesign design)
        {
            this.design = design;
        }

        //public DocumentDesign(Configuration configuration, DocumentTable table) : base(configuration, table, typeof (TEntity)) {}

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
                column = new Column(name, new SqlColumn(typeof(TMember)));
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
                if (!newProjection.ReturnType.IsCastableTo(existingProjection.ReturnType))
                    throw new InvalidOperationException("Projection must be of same type.");

                design.Projections[name] = newProjection;
            }

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