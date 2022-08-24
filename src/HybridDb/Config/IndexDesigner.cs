using System;
using System.Linq.Expressions;

namespace HybridDb.Config
{
    public class IndexDesigner<TIndex, TEntity>
    {
        private readonly Configuration configuration;
        readonly DocumentDesigner<TEntity> designer;

        public IndexDesigner(DocumentDesign design, Configuration configuration)
        {
            this.configuration = configuration;
            designer = new DocumentDesigner<TEntity>(design, configuration);
        }

        public IndexDesigner<TIndex, TEntity> With<TMember>(Expression<Func<TIndex, TMember>> namer, Expression<Func<TEntity, TMember>> projector, params Option[] options)
        {
            var name = configuration.ColumnNameConvention(namer);
            designer.With(name, projector, options);
            return this;
        }
    }
}