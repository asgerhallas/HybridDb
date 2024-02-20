using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Dapper;
using HybridDb.Linq.Bonsai;
using Newtonsoft.Json.Linq;

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

        // TODO: Make obsolete With work and run tests
        // TODO: Make overloads for each sql type
        // TODO: Make these tests work
        // TODO: Make overloads for JSON (with combining
        // TODO: Remove all usage in tests of with (except above tests)
        // TODO: Write obsolete description
        // TODO: Use the right columnname in linq provider
        // TODO: Generate ColumnName classes/extension-methods to use in queries?

        public DocumentDesigner<TEntity> Column(Expression<Func<TEntity, string>> projector, OptionsBuilder<string> options = null) => AddColumn(projector, options);

        public DocumentDesigner<TEntity> WithJson<TMember>(
            Expression<Func<TEntity, TMember>> projector,
            params Option<string>[] options
        ) => null; //WithJson(configuration.ColumnNameConvention(projector), projector, options);

        //public DocumentDesigner<TEntity> WithJson<TMember>(
        //    string name,
        //    Expression<Func<TEntity, TMember>> projector,
        //    params Option<string>[] options
        //) => WithJson(name, projector, x => x, options);

        //public DocumentDesigner<TEntity> WithJson<TMember, TReturn>(
        //    string name,
        //    Expression<Func<TEntity, TMember>> projector,
        //    Func<TMember, TReturn> converter,
        //    params Option<string>[] options
        //)
        //{
        //    SqlMapper.AddTypeHandler(new JsonTypeHandler<TReturn>(configuration.Serializer));

        //    Expression.Lambda(x => configuration.Serializer.Serialize(projector));

        //    return With(x => configuration.Serializer.Serialize(projector(x)), [..options, new AsJson(), new MaxLength()]);
        //}

        DocumentDesigner<TEntity> AddColumn<TValue>(Expression<Func<TEntity, TValue>> projector, OptionsBuilder<TValue> optionsBuilder = null)
        {
            optionsBuilder ??= x => x;

            var options = optionsBuilder(new DocumentDesignerOptions<TValue>());

            if (SqlTypeMap.ForNetType(Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue)) == null)
            {
                throw new ArgumentException($"""
                    There's no built in converter from {typeof(TValue)} to an SqlType.
                    Use WithJson if you want to project is a JSON.
                    """);
            }

            var name = options.Name ?? configuration.ColumnNameConvention(projector);

            var column = design.Table[name];

            if (DocumentTable.IdColumn.Equals(column))
            {
                throw new ArgumentException("You can not make a projection for IdColumn. Use Document.Key() method instead.");
            }

            column ??= design.Table.Add(new Column<TValue>(name, options.Length));

            var compiledProjector = CompileProjector(name, projector, options, column);

            var newProjection = Projection.From<TValue>(document => compiledProjector(document));

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

        static Func<object, object> CompileProjector<TMember, TReturn>(
            string name,
            Expression<Func<TEntity, TMember>> projector,
            DocumentDesignerOptions<TReturn> options,
            Column column
        )
        {
            if (options.DisableNullCheckInjection) return Compile(name, projector);

            var nullCheckInjector = new NullCheckInjector();
            var nullCheckedProjector = (Expression<Func<TEntity, object>>)nullCheckInjector.Visit(projector);

            if (!nullCheckInjector.CanBeTrustedToNeverReturnNull && !column.IsPrimaryKey)
            {
                column.Nullable = true;
            }

            return Compile(name, nullCheckedProjector);
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

        [Obsolete($"""
            The method '.With(projector, converter, params options[])' is deprecated, use '.Column()' instead.

            A sample rewrite could be from:
                
                .With(x => x.Property, x => x.ToUpper(), new MaxLength(), new DisableNullCheckInjection());

            To:
                
                .Column(x => x.Property.ToUpper(), x => x.UseMaxLength().DisableNullCheckInjection());
                
            IMPORTANT: Default naming for columns has changed to only include properties (not methods, extension methods or operators and so on).
            
            That means that '.With(x => x.Property.ToUpper())' was previously named 'PropertyToUpper' as default, 
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
                
                .With(x => x.Property, new MaxLength(), new DisableNullCheckInjection());

            To:
                
                .Column(x => x.Property, x => x.UseMaxLength().DisableNullCheckInjection());
                
            IMPORTANT: Default naming for columns has changed to only include properties (not methods, extension methods or operators and so on).

            That means that '.With(x => x.Property.ToUpper())' was previously named 'PropertyToUpper' as default,
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