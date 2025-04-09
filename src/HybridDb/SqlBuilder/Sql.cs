#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace HybridDb.SqlBuilder
{
    public class Sql
    {
        readonly List<Fragment> fragments = new();

        public bool IsEmpty => fragments.Count == 0;
        public IReadOnlyList<Fragment> Fragments => fragments;

        /// <summary>
        /// Do not use ToString
        /// </summary>
        public new void ToString() { }

        public string ToString(IDocumentStore store) => Build(store, out _);

        public string Build(IDocumentStore store, out HybridDbParameters parameters)
        {
            parameters = new HybridDbParameters();

            return Build(store, parameters);
        }

        public string Build(IDocumentStore store, HybridDbParameters parameters)
        {
            var sql = new StringBuilder();

            string? previousValue = null;

            foreach (var fragment in fragments)
            {
                previousValue = BuildFragment(store, sql, parameters, fragment, previousValue);
            }

            return sql.ToString();
        }

        string? BuildFragment(IDocumentStore store, StringBuilder sql, HybridDbParameters parameters, Fragment fragment, string? previousValue)
        {
            switch (fragment)
            {
                case StringFragment stringFragment:
                    return AppendSql(stringFragment.Value);

                case ParameterFragment parameterFragment:
                    var parameter = parameterFragment.Parameter;

                    parameter.ParameterName = NormalizeParameterName(parameters.Count, parameter.ParameterName);

                    parameters.Add(parameter);

                    return AppendSql($"@{parameter.ParameterName}");

                case TableFragment tableFragment:
                    return AppendSql(store.Database.FormatTableNameAndEscape(tableFragment.TableName));

                case ColumnFragment columnFragment:
                    return AppendSql(store.Database.Escape(columnFragment.ColumnName));
            }

            return null;

            TokenType ParseTokenType(char value)
            {
                if (value == ' ') return TokenType.NoneOrSpace;
                if (value == ',') return TokenType.Comma;
                if (value == '.') return TokenType.Dot;
                if (value is '(' or '[') return TokenType.StartParenthesis;
                if (value is ')' or ']') return TokenType.EndParenthesis;

                return TokenType.Word;
            }

            string AppendSql(string next)
            {
                if (next == string.Empty) return string.Empty;

                var nextTokenType = ParseTokenType(next[0]);
                var previousTokenType = previousValue is not (null or "")
                    ? ParseTokenType(previousValue.Last())
                    : TokenType.NoneOrSpace;

                sql.Append((previousTokenType, nextTokenType) switch
                {
                    (TokenType.Comma, _) => " ",
                    (TokenType.Word, TokenType.Word) => " ",
                    (TokenType.Word, TokenType.StartParenthesis) => " ",
                    (TokenType.EndParenthesis, TokenType.Word) => " ",
                    (_, _) => null
                });

                sql.Append(next);

                return next;
            }
        }

        public enum TokenType
        {
            NoneOrSpace,
            Word,
            Comma,
            Dot,
            StartParenthesis,
            EndParenthesis
        }

        string NormalizeParameterName(int currentParameterCount, string name) =>
            Regex.IsMatch(name, "[^a-zA-Z_]")
                // Anonymous name for complex expressions with strange chars
                ? $"Param_{currentParameterCount+1}"
                : $"{Capitalize(name)}_{currentParameterCount+1}";

        public static string Capitalize(string str) =>
            str.First().ToString().ToUpper() +
            new string(str.Skip(1).ToArray());

        public Sql Append(IReadOnlyList<Fragment> newFragments)
        {
            fragments.AddRange(newFragments);

            return this;
        }

        public static Sql Empty => new();
        public static Sql Parenthesize(Sql sql) => !sql.IsEmpty ? From("(").Append(sql).Append(")") : Empty;

        public static Sql From(SqlStringHandler handler) => Sql.Empty.Append(handler.fragments);
        public Sql Append(SqlStringHandler handler) => Append(handler.fragments);

        public static Sql From(SqlStringHandler handler, object param) => Sql.Empty.Append(handler, param);
        public Sql Append(SqlStringHandler handler, object param)
        {
            Append(handler.fragments);
            Append(param.ToHybridDbParameters().Parameters
                .Select(x => new ParameterFragment(x))
                .ToList());

            return this;
        }

        public static Sql From(Sql builder) => Empty.Append(builder);
        public Sql Append(Sql builder)
        {
            fragments.AddRange(builder.fragments);

            return this;
        }

        public static Sql From(IEnumerable<Sql> builders) => Empty.Append(builders);
        public Sql Append(IEnumerable<Sql> builders)
        {
            fragments.AddRange(builders.SelectMany(x => x.fragments));

            return this;
        }

        public Sql Append((Sql Sql1, Sql Sql2) sql)
        {
            Append(sql.Sql1);

            return sql.Sql2;
        }

        public static Sql From(bool predicate, SqlStringHandler handler, SqlStringHandler? elseHandler = null) => Empty.Append(predicate, handler);
        public Sql Append(bool predicate, SqlStringHandler handler, SqlStringHandler? elseHandler = null) =>
            predicate
                ? Append(handler)
                : elseHandler is { } @else
                    ? Append(@else)
                    : this;

        public static Sql From(bool predicate, Func<SqlStringHandler> handler) => Empty.Append(predicate, handler);
        public Sql Append(bool predicate, Func<SqlStringHandler> handler) => predicate ? Append(handler()) : this;

        public static Sql From(string infix, SqlStringHandler handler) => Empty.Append(infix, Empty.Append(handler));
        public Sql Append(string infix, SqlStringHandler handler) => Append(infix, Empty.Append(handler));

        public static Sql From(string infix, Sql sql) => Sql.Empty.Append(infix, sql);
        public Sql Append(string infix, Sql sql)
        {
            if (!IsEmpty && !sql.IsEmpty) Append([new StringFragment(infix)]);

            return Append(sql);
        }

        public static Sql Join(string separator, params Sql[] builders) => Join(separator, (IEnumerable<Sql>)builders);

        public static Sql Join(string separator, IEnumerable<Sql> builders)
        {
            var sql = new Sql();

            var nonEmptyBuilders = builders
                .Where(x => x.fragments.Count > 0)
                .Select((x, index) => (x, index));

            foreach (var (builder, index) in nonEmptyBuilders)
            {
                if (index > 0) sql.Append(separator);

                sql.Append(builder);
            }

            return sql;
        }
    }
}