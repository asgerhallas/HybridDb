using System;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Registration;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading;

namespace HybridDb.MigrationRunner
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Contains("--run"))
            {
                var connectionString = ConfigurationManager.ConnectionStrings["HybridDBConnectionString"].ConnectionString;
                var store = new DocumentStore(connectionString);

                var runner = new Runner(store);

                var registration = new RegistrationBuilder();
                registration.ForTypesDerivedFrom<Migration.Migration>().Export<Migration.Migration>();

                var catalog = new DirectoryCatalog(".", registration);
                var container = new CompositionContainer(catalog);
                container.ComposeParts(runner);

                while (true)
                {
                    catalog.Refresh();
                    runner.Run();
                    Thread.Sleep(1000);
                }
            }

            var assembly = System.Reflection.Assembly.GetEntryAssembly();
            var executable = assembly.Location;
            var setup = new AppDomainSetup
            {
                ApplicationBase = Path.GetDirectoryName(assembly.Location),
                ShadowCopyFiles = "true"
            };

            var domain = AppDomain.CreateDomain("MigrationRunnerShadow", null, setup);
            domain.ExecuteAssembly(executable, new[] { "--run" });
        }
    }
}
