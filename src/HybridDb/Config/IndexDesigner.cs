using System;
using System.Linq.Expressions;

namespace HybridDb.Config
{
    public class IndexDesigner<TIndex, TEntity>
    {
        readonly DocumentDesigner<TEntity> designer;

        public IndexDesigner(DocumentDesign design)
        {
            designer = new DocumentDesigner<TEntity>(design);
        }

        public IndexDesigner<TIndex, TEntity> With<TMember>(Expression<Func<TIndex, TMember>> namer, Expression<Func<TEntity, TMember>> projector, params Option[] options)
        {
            var name = ColumnNameBuilder.GetColumnNameByConventionFor(namer);
            designer.With(name, projector, options);
            return this;
        }
    }
}