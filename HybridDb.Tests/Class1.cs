using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HybridDb.Schema;

namespace HybridDb.Tests
{
    class PseudoStore
    {
        public dynamic Migration { get; set; } 
    }

    class Class1
    {
        public Class1()
        {
            //var store = new PseudoStore();
            
            //// Capitalize postalCode for indexing
            //store.Migration.AddIndex<DocumentStoreTests.Case>(x => x.Address.PostalCode);
            
            //// Convert fields to tags
            //store.Migration.Do<JObject>("Case", jsonDoc =>
            //{
            //    ConvertFieldsToTags(@case, "Energy10.Core.Domain.Model.Case.BuildingUnits.HeatDistributionPipe, Energy10.Core.Domain", new Dictionary<string, string>
            //    {
            //        { "material", "Material" },
            //        { "dimension", "Dimension" }
            //    });
            //});

            //// Rename indexed property
            //store.Migration.Do<JObject>(new DynamicTable<JObject>("Cases"), jsonDoc =>
            //{
            //    jsonDoc["Address"] = jsonDoc["WrongAddress"];
            //    jsonDoc.Remove("WrongAddress");
            //});
            //store.Migration.RenameIndex("WrongAddress", "Address");

            //// M059_field_information_for_window_type
            //store.Migration.Do<FieldInformationContainer>(container =>
            //{
            //    container.AddFieldInfo
            //})
            
            //// Add ComapnyRef to Users
            //store.Migration.Do("Users", (userDoc, indexes) =>
            //{
            //    var companyColumns = store.Get(new Table<JObject>("Companies"), indexes["CompanyId"]);
            //    var companyDoc = store.Deserialize<JObject>(companyColumns.Document);
            //    userDoc["Company"].ProperyOrField("Name", companyDoc["Name"]);
            //    indexes["CompanyName"] = companyDoc["Name"];
            //});

            //store.Migration.UpdateIndex<User>(x => x.Company.Name);
        }
    }
}
