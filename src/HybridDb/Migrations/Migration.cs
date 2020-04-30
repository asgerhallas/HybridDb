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
        /// Commands returned by this method will be run before HybridDb starts.
        ///
        /// <para>Note that if you run multiple migrations on the same database, all Upfront commands will be run before any of the Background commands.</para>
        ///
        /// <para>Therefore Upfront commands should never rely on changes being made in Background commands. Even between deploys.</para>
        ///
        /// There's two reasons for this not being safe between deploys either:
        /// <list type="number">
        ///   <item>You can not be sure that Background commands have completed executing for all documents before your next deploy.</item>
        ///   <item>You might want to migrate a database that's several versions behind and thus run multiple migrations together
        ///      that was not meant to be run together.</item>
        /// </list>
        /// </summary>
        public virtual IEnumerable<DdlCommand> Upfront(Configuration configuration)
        {
            yield break;
        }

        /// <summary>
        /// Commands returned by this method will be run on individual rows in the background and also when a document is loaded/queried into a session.
        ///
        /// Please see the docs for <seealso cref="Upfront"/> for a warning about running multiple migrations together.
        /// </summary>
        public virtual IEnumerable<RowMigrationCommand> Background(Configuration configuration)
        {
            yield break;
        }
    }
}