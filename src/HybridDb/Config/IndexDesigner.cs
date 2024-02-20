using System;
using System.Linq.Expressions;

namespace HybridDb.Config
{
    public class IndexDesigner<TIndex, TEntity>(DocumentDesign design, Configuration configuration)
    {
        readonly DocumentDesigner<TEntity> designer = new(design, configuration);

        public IndexDesigner<TIndex, TEntity> With<TMember>(
            Expression<Func<TIndex, TMember>> namer,
            Expression<Func<TEntity, TMember>> projector,
            params Option<TMember>[] options)
        {
            var name = configuration.ColumnNameConvention(namer);
            //designer.With(name, projector, options);
            return this;
        }
    }
}