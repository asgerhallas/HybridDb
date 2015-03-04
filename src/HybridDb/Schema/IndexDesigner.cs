using System;
using System.Linq.Expressions;

namespace HybridDb.Schema
{
    public class IndexDesigner<TIndex, TEntity>
    {
        readonly DocumentDesigner<TEntity> designer;

        public IndexDesigner(DocumentDesign design)
        {
            designer = new DocumentDesigner<TEntity>(design);
        }

        public IndexDesigner<TIndex, TEntity> With<TMember>(Expression<Func<TIndex, TMember>> namer, Expression<Func<TEntity, TMember>> projector, bool makeNullSafe = true)
        {
            var name = string.Format("{0}_{1}", typeof(TIndex).Name, ColumnNameBuilder.GetColumnNameByConventionFor(namer));
            designer.With(name, projector, makeNullSafe);
            return this;
        }
    }
}