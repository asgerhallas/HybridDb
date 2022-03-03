using System;
using System.Collections.Generic;
using HybridDb.Config;
using HybridDb.Migrations.Documents;
using HybridDb.Migrations.Schema;

namespace HybridDb.Migrations
{
    public abstract class Migration
    {
        protected Migration(int version) => Version = version;

        public int Version { get; }

        /// <summary>
        /// <seealso cref="AfterAutoMigrations"/>
        /// </summary>
        [Obsolete("Use AfterAutoMigrations instead (or BeforeAutoMigrations if that's what you need).")]
        public virtual IEnumerable<DdlCommand> Upfront(Configuration configuration)
        {
            yield break;
        }

        /// <summary>
        /// Commands returned by this method will be run when HybridDb initializes, before HybridDb auto migrates
        /// the schema to match the configuration. This is useful for adding a new column with default data without triggering
        /// a reprojection, for example.
        ///
        /// <para>Note that if you deploy multiple migrations at the same time, all BeforeAutoMigrations commands will
        /// be run in order, but at the same time, before any AfterAutoMigrations, auto migrations and Background commands.</para>
        ///
        /// <para>Therefore BeforeAutoMigrations commands should never rely on changes being made in AfterAutoMigrations, auto migrations 
        /// or Background commands. And for Background commands this also applies between deploys.</para>
        ///
        /// There's two reasons for this not being safe between deploys either:
        /// <list type="number">
        ///   <item>You can not be sure that Background commands have completed executing for all documents before your next deploy.</item>
        ///   <item>You might want to migrate a database that's several versions behind and thus run multiple migrations together
        ///      that was not meant to be run together.</item>
        /// </list>
        /// </summary>
        public virtual IEnumerable<DdlCommand> BeforeAutoMigrations(Configuration configuration)
        {
            yield break;
        }

        /// <summary>
        /// Commands returned by this method will be run when HybridDb initializes, after HybridDb auto migrates
        /// the schema to match the configuration. This is useful for adding indexes on columns added via auto migrations,
        /// for example.
        ///
        /// <para>Note that if you deploy multiple migrations at the same time, all AfterAutoMigrations commands will
        /// be run in order, but at the same time, before any Background commands.</para>
        ///
        /// <para>Therefore AfterAutoMigrations commands should never rely on changes being made in Background commands.
        /// And this also applies between deploys.</para>
        ///
        /// There's two reasons for this not being safe between deploys either:
        /// <list type="number">
        ///   <item>You can not be sure that Background commands have completed executing for all documents before your next deploy.</item>
        ///   <item>You might want to migrate a database that's several versions behind and thus run multiple migrations together
        ///      that was not meant to be run together.</item>
        /// </list>
        /// </summary>
        public virtual IEnumerable<DdlCommand> AfterAutoMigrations(Configuration configuration) => 
            Upfront(configuration);

        /// <summary>
        /// Commands returned by this method will be run on individual rows in the background and also when a document is loaded/queried into a session.
        ///
        /// Please see the docs for <seealso cref="BeforeAutoMigrations"/> and <seealso cref="AfterAutoMigrations"/> for a warning about running multiple migrations together.
        /// </summary>
        public virtual IEnumerable<RowMigrationCommand> Background(Configuration configuration)
        {
            yield break;
        }
    }
}