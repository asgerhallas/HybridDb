using System;
using System.Linq.Expressions;
using System.Reflection;
using Dapper;

namespace HybridDb.Config
{
    public class DocumentDesignerOptions<TEntity>
    {
        internal string Name { get; set; }
        internal int? Length { get; set; } = typeof(TEntity) == typeof(string) ? 850 : null;
        internal bool DisableNullCheckInjection { get; set; }
    }

    public static class DocumentDesignerOptionsEx
    {
        public static DocumentDesignerOptions<TEntity> DisableNullCheckInjection<TEntity>(this DocumentDesignerOptions<TEntity> options)
        {
            options.DisableNullCheckInjection = true;

            return options;
        }

        public static DocumentDesignerOptions<string> UseMaxLength(this DocumentDesignerOptions<string> options)
        {
            options.Length = -1;

            return options;
        }

        public static DocumentDesignerOptions<string> UseLength(this DocumentDesignerOptions<string> options, uint length)
        {
            options.Length = (int)length;

            return options;
        }

        public static DocumentDesignerOptions<TEntity> Name<TEntity>(this DocumentDesignerOptions<TEntity> options, string name)
        {
            options.Name = name ?? throw new ArgumentNullException(nameof(name));

            return options;
        }
    }

    public delegate DocumentDesignerOptions<TValue> OptionsBuilder<TValue>(DocumentDesignerOptions<TValue> options);

    public class DocumentDesigner<TEntity>
    {
        readonly DocumentDesign design;
        readonly Configuration configuration;

        public DocumentDesigner(DocumentDesign design, Configuration configuration)
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

        // TODO: Make overloads for JSON (with combining
        // TODO: Use the right columnname in linq provider
        // TODO: Generate ColumnName classes/extension-methods to use in queries?

        public DocumentDesigner<TEntity> Column(Expression<Func<TEntity, string>> projector, OptionsBuilder<string> options = null) => AddColumn(projector, options);
        public DocumentDesigner<TEntity> Column(Expression<Func<TEntity, bool>> projector, OptionsBuilder<bool> options = null) => AddColumn(projector, options);
        public DocumentDesigner<TEntity> Column(Expression<Func<TEntity, bool?>> projector, OptionsBuilder<bool?> options = null) => AddColumn(projector, options);
        public DocumentDesigner<TEntity> Column(Expression<Func<TEntity, int>> projector, OptionsBuilder<int> options = null) => AddColumn(projector, options);
        public DocumentDesigner<TEntity> Column(Expression<Func<TEntity, int?>> projector, OptionsBuilder<int?> options = null) => AddColumn(projector, options);
        public DocumentDesigner<TEntity> Column(Expression<Func<TEntity, long>> projector, OptionsBuilder<long> options = null) => AddColumn(projector, options);
        public DocumentDesigner<TEntity> Column(Expression<Func<TEntity, long?>> projector, OptionsBuilder<long?> options = null) => AddColumn(projector, options);
        public DocumentDesigner<TEntity> Column(Expression<Func<TEntity, double>> projector, OptionsBuilder<double> options = null) => AddColumn(projector, options);
        public DocumentDesigner<TEntity> Column(Expression<Func<TEntity, double?>> projector, OptionsBuilder<double?> options = null) => AddColumn(projector, options);
        public DocumentDesigner<TEntity> Column(Expression<Func<TEntity, decimal>> projector, OptionsBuilder<decimal> options = null) => AddColumn(projector, options);
        public DocumentDesigner<TEntity> Column(Expression<Func<TEntity, decimal?>> projector, OptionsBuilder<decimal?> options = null) => AddColumn(projector, options);
        public DocumentDesigner<TEntity> Column(Expression<Func<TEntity, DateTimeOffset>> projector, OptionsBuilder<DateTimeOffset> options = null) => AddColumn(projector, options);
        public DocumentDesigner<TEntity> Column(Expression<Func<TEntity, DateTimeOffset?>> projector, OptionsBuilder<DateTimeOffset?> options = null) => AddColumn(projector, options);
        public DocumentDesigner<TEntity> Column(Expression<Func<TEntity, DateTime>> projector, OptionsBuilder<DateTime> options = null) => AddColumn(projector, options);
        public DocumentDesigner<TEntity> Column(Expression<Func<TEntity, DateTime?>> projector, OptionsBuilder<DateTime?> options = null) => AddColumn(projector, options);
        public DocumentDesigner<TEntity> Column(Expression<Func<TEntity, DateOnly>> projector, OptionsBuilder<DateOnly> options = null) => AddColumn(projector, options);
        public DocumentDesigner<TEntity> Column(Expression<Func<TEntity, DateOnly?>> projector, OptionsBuilder<DateOnly?> options = null) => AddColumn(projector, options);
        public DocumentDesigner<TEntity> Column(Expression<Func<TEntity, Guid>> projector, OptionsBuilder<Guid> options = null) => AddColumn(projector, options);
        public DocumentDesigner<TEntity> Column(Expression<Func<TEntity, Guid?>> projector, OptionsBuilder<Guid?> options = null) => AddColumn(projector, options);
        public DocumentDesigner<TEntity> Column(Expression<Func<TEntity, Enum>> projector, OptionsBuilder<Enum> options = null) => AddColumn(projector, options);

        public DocumentDesigner<TEntity> JsonColumn<TMember>(
            Expression<Func<TEntity, TMember>> projector,
            OptionsBuilder<string> optionsBuilder = null
        )
        {
            optionsBuilder ??= x => x;

            SqlMapper.AddTypeHandler(new JsonTypeHandler<TMember>(configuration.Serializer));

            return AddColumn(projector, x => configuration.Serializer.Serialize(x), x => optionsBuilder(x).UseMaxLength());
        }

        DocumentDesigner<TEntity> AddColumn<TValue>(
            Expression<Func<TEntity, TValue>> projector,
            OptionsBuilder<TValue> optionsBuilder = null
        ) => AddColumn(projector, x => x, optionsBuilder);

        DocumentDesigner<TEntity> AddColumn<TProjector, TValue>(
            Expression<Func<TEntity, TProjector>> projector,
            Func<TProjector, TValue> converter,
            OptionsBuilder<TValue> optionsBuilder = null)
        {
            var options = GetOptions(optionsBuilder);
            var name = GetName(projector, options);
            var checkedProjector = !options.DisableNullCheckInjection
                ? MaybeInjectNullCheck(name, projector, options)
                : projector;

            var column = AddColumn(name, options);
            var compiledProjector = Compile(name, checkedProjector, converter);

            var newProjection = Projection.From<TValue>(compiledProjector);

            if (!newProjection.ReturnType.IsCastableTo(column.Type))
            {
                throw new InvalidOperationException(
                    $"Can not override projection for {column.Name} of type {column.Type} " +
                    $"with a projection that returns {newProjection.ReturnType} (on {typeof (TEntity)}).");
            }

            if (!design.Projections.TryGetValue(column.Name, out _))
            {
                if (design.Parent != null && !column.IsPrimaryKey)
                {
                    column.Nullable = true;
                }

                design.Projections.Add(column, newProjection);
            }
            else
            {
                design.Projections[column.Name] = newProjection;
            }

            return this;
        }

        string GetName<TValue, TOptions>(Expression<Func<TEntity, TValue>> projector, DocumentDesignerOptions<TOptions> options) => options.Name ?? configuration.ColumnNameConvention(projector);

        static DocumentDesignerOptions<TValue> GetOptions<TValue>(OptionsBuilder<TValue> optionsBuilder)
        {
            optionsBuilder ??= x => x;

            var options = optionsBuilder(new DocumentDesignerOptions<TValue>());

            return options;
        }

        Column AddColumn<TValue>(string name, DocumentDesignerOptions<TValue> options)
        {
            if (SqlTypeMap.ForNetType(Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue)) == null)
            {
                throw new ArgumentException($"""
                    There's no built in converter from {typeof(TValue)} to an SqlType.
                    Use WithJson if you want to project is a JSON.
                    """);
            }

            //TODO
            //if (canBeNull && !column.IsPrimaryKey)
            //{
            //    column.Nullable = true;
            //}

            var column = design.Table[name];

            if (DocumentTable.IdColumn.Equals(column))
            {
                throw new ArgumentException("You can not make a projection for IdColumn. Use Document.Key() method instead.");
            }

            return column ?? design.Table.Add(new Column<TValue>(name, options.Length));
        }

        Expression<Func<TEntity, TMember>> MaybeInjectNullCheck<TMember, TReturn>(
            string name,
            Expression<Func<TEntity, TMember>> projector,
            DocumentDesignerOptions<TReturn> options
        ) where TMember:class
        {

        }


        Expression<Func<TEntity, TMember?>> MaybeInjectNullCheck<TMember, TReturn>(
            string name,
            Expression<Func<TEntity, TMember>> projector,
            DocumentDesignerOptions<TReturn> options
        ) where TMember:struct
        {
            if (options.DisableNullCheckInjection) return projector;

            var nullCheckInjector = new NullCheckInjector();
            var nullCheckedProjector = (Expression<Func<TEntity, TMember>>)nullCheckInjector.Visit(projector);

            if (nullCheckInjector.CanBeTrustedToNeverReturnNull) return projector;

            return nullCheckedProjector;
        }

        protected static Func<object, object> Compile<TMember, TValue>(
            string name,
            Expression<Func<TEntity, TMember>> projector,
            Func<TMember, TValue> converter)
        {
            var compiled = projector.Compile();

            return entity =>
            {
                try
                {
                    return converter(compiled((TEntity)entity));
                }
                catch (Exception ex)
                {
                    throw new TargetInvocationException(
                        $"""
                        The projector for column {name} threw an exception.
                        The projector code is {projector}.
                        """, ex);
                }
            };
        }



        public static Expression<Func<T1, T3>> Chain<T1, T2, T3>(
            Expression<Func<T1, T2>> expr1,
            Func<T2, T3> expr2)
        {
            var parameter = Expression.Parameter(typeof(T1));

            //var leftVisitor = new ReplaceExpressionVisitor(expr1.Parameters[0], parameter);
            //var left = leftVisitor.Visit(expr1.Body);

            //var rightVisitor = new ReplaceExpressionVisitor(expr2.Parameters[0], parameter);
            //var right = rightVisitor.Visit(expr2.Body);

            return Expression.Lambda<Func<T1, T3>>(
                Expression.Invoke(
                    Expression.Constant(expr2),
                    Expression.Invoke(expr1, parameter)),
                parameter);
        }

        class ReplaceExpressionVisitor(Expression oldValue, Expression newValue) : ExpressionVisitor
        {
            public override Expression Visit(Expression node) => node == oldValue ? newValue : base.Visit(node);
        }

        #region Deprecated With-methods

        [Obsolete("", true)]
        public DocumentDesigner<TEntity> Extend(Action<object> extender) => throw new NotSupportedException();

        [Obsolete($"""
            The method '.With(projector, converter, params options[])' is deprecated, use '.Column()' instead.

            A sample rewrite could be from:
                
                .Column(x => x.Property, x => x.ToUpper(), new MaxLength(), new DisableNullCheckInjection());

            To:
                
                .Column(x => x.Property.ToUpper(), x => x.UseMaxLength().DisableNullCheckInjection());
                
            IMPORTANT: Default naming for columns has changed to only include properties (not methods, extension methods or operators and so on).
            
            That means that '.Column(x => x.Property.ToUpper())' was previously named 'PropertyToUpper' as default, 
            but is now named 'Property' and should thus be rewritten to '.Column(x => x.Property.ToUpper(), x => x.Name("PropertyToUpper"))'
            to maintain old behavior.
            """, true)]
        public DocumentDesigner<TEntity> With<TMember, TReturn>(
            Expression<Func<TEntity, TMember>> projector,
            Func<TMember, TReturn> converter,
            params Option[] options
        ) => throw new NotSupportedException();

        [Obsolete($"""
            The method '.With(projector, params options[])' is deprecated, use '.Column()' instead.

            A sample rewrite could be from:
                
                .Column(x => x.Property, new MaxLength(), new DisableNullCheckInjection());

            To:
                
                .Column(x => x.Property, x => x.UseMaxLength().DisableNullCheckInjection());
                
            IMPORTANT: Default naming for columns has changed to only include properties (not methods, extension methods or operators and so on).

            That means that '.Column(x => x.Property.ToUpper())' was previously named 'PropertyToUpper' as default,
            but is now named 'Property' and should thus be rewritten to '.Column(x => x.Property.ToUpper(), x => x.Name("PropertyToUpper"))'
            to maintain old behavior.
            """, true)]
        public DocumentDesigner<TEntity> With<TMember>(
            Expression<Func<TEntity, TMember>> projector,
            params Option[] options
        ) => throw new NotSupportedException();

        [Obsolete($"""
            The method '.With(name, projector, params options[])' is deprecated, use '.Column()' instead.

            A sample rewrite could be from:
                
                .With("MyColumnName", x => x.Property, new MaxLength(), new DisableNullCheckInjection());

            To:
                
                .Column(x => x.Property, x => x.UseName("MyColumnName").UseMaxLength().DisableNullCheckInjection());
            """, true)]
        public DocumentDesigner<TEntity> With<TMember>(
            string name,
            Expression<Func<TEntity, TMember>> projector,
            params Option[] options
        ) => throw new NotSupportedException();

        [Obsolete($"""
            The method '.With(name, projector, converter, params options[])' is deprecated, use '.Column()' instead.

            A sample rewrite could be from:
                
                .With("MyColumnName", x => x.Property, x => x.ToUpper(), new MaxLength(), new DisableNullCheckInjection());

            To:
                
                .Column(x => x.Property.ToUpper(), x => x.UseName("MyColumnName").UseMaxLength().DisableNullCheckInjection());
            """, true)]
        public DocumentDesigner<TEntity> With<TMember, TReturn>(
            string name,
            Expression<Func<TEntity, TMember>> projector,
            Func<TMember, TReturn> converter,
            params Option[] options
        ) => throw new NotSupportedException();

        #endregion
    }
}