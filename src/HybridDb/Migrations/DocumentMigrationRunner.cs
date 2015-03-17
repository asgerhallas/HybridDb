using System;
using System.Linq;
using System.Threading.Tasks;
using HybridDb.Config;

namespace HybridDb.Migrations
{
    public class DocumentMigrationRunner
    {
        readonly IDocumentStore store;
        readonly Configuration configuration;

        public DocumentMigrationRunner(IDocumentStore store, Configuration configuration)
        {
            this.store = store;
            this.configuration = configuration;
        }

        public Task RunInBackground()
        {
            return Task.Factory.StartNew(RunSynchronously, TaskCreationOptions.LongRunning);
        }

        public void RunSynchronously()
        {
            foreach (var table in configuration.Tables.Values.OfType<DocumentTable>())
            {
                var baseDesign = configuration.DocumentDesigns.First(x => x.Table.Name == table.Name);

                QueryStats stats;
                foreach (var row in store.Query(table, out stats,
                    @where: "AwaitsReprojection = @AwaitsReprojection or Version < @version",
                    parameters: new { AwaitsReprojection = true, version = configuration.CurrentVersion }))
                {
                    var discriminator = ((string)row[table.DiscriminatorColumn]).Trim();

                    DocumentDesign concreteDesign;
                    if (!baseDesign.DecendentsAndSelf.TryGetValue(discriminator, out concreteDesign))
                    {
                        throw new InvalidOperationException(
                            string.Format("Discriminator '{0}' was not found in configuration.", discriminator));
                    }

                    var entity = DocumentSession.DeserializeAndMigrate(store, concreteDesign, row);
                    var projections = concreteDesign.Projections.ToDictionary(x => x.Key, x => x.Value.Projector(entity));
                    projections.Add(table.VersionColumn, configuration.CurrentVersion);

                    try
                    {
                        store.Update(table, (Guid)row[table.IdColumn], (Guid)row[table.EtagColumn], projections);
                    }
                    catch (ConcurrencyException)
                    {
                    }
                }
            }
        }
    }
}