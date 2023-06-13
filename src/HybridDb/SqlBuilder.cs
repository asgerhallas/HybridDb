﻿using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using HybridDb.Config;

namespace HybridDb
{
    public class SqlBuilder
    {
        readonly StringBuilder fragments;
        public readonly HybridDbParameters parameters;

        public SqlBuilder()
        {
            fragments = new StringBuilder();
            parameters = new HybridDbParameters();
        }

        public HybridDbParameters Parameters => parameters;

        public SqlBuilder Append(string sql, params SqlParameter[] args)
        {
            foreach (var arg in args)
            {
                parameters.Add(arg);
            }

            if (fragments.Length != 0) fragments.Append(" ");

            fragments.Append(sql);

            return this;
        }

        public SqlBuilder Append(bool predicate, string sql, params SqlParameter[] args)
        {
            if (predicate) Append(sql, args);
            return this;
        }

        public SqlBuilder Append(bool predicate, string sql, string orSql, params SqlParameter[] args) => 
            predicate ? Append(sql, args) : Append(orSql, args);

        public SqlBuilder Append(SqlBuilder builder)
        {
            fragments.Append(builder.fragments);
            parameters.Add(builder.parameters);
            return this;
        }

        public override string ToString() => string.Join(" ", fragments);
    }
}