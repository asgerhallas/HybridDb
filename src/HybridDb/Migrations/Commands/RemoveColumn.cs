using System;
using HybridDb.Config;

namespace HybridDb.Migrations.Commands
{
    public class RemoveColumn : SchemaMigrationCommand
    {
        public RemoveColumn(Table table, string name)
        {
            Unsafe = name == "Document" || name == "Id";

            Table = table;
            Name = name;
        }

        public Table Table { get; private set; }
        public string Name { get; private set; }

        public override void Execute(Database database)
        {
            // Bug in SQL server 2012 prevents us to query SYS.DEFAULT_CONSTRAINTS for temp tables. Furthermore it is only possible to query
            // the table sys.objects$ and the value parent_object_id of sys.objects if using DAC, for which reason it is not possible to get
            // column constraints on a specific column.
            // See: https://connect.microsoft.com/SQLServer/feedback/details/765777/sys-default-constraints-empty-for-temporary-tables-in-tempdb
            // See: http://www.sqlservercentral.com/Forums/Topic1359991-3077-1.aspx
            if(database.TableMode == TableMode.UseTempTables)
                throw new InvalidOperationException("It is currently not possible to remove columns on temp tables.");

            var tableName = database.FormatTableNameAndEscape(Table.Name);
            var dropConstraints = "DECLARE @ConstraintName nvarchar(200) " +
                                  "SELECT @ConstraintName = Name FROM SYS.DEFAULT_CONSTRAINTS " +
                                  "WHERE PARENT_OBJECT_ID = OBJECT_ID('" + tableName + "') " +
                                  "AND PARENT_COLUMN_ID = (SELECT column_id FROM sys.columns WHERE NAME = N'" + Name + "' AND object_id = OBJECT_ID(N'" + tableName + "')) " +
                                  "IF @ConstraintName IS NOT NULL " +
                                  "EXEC('ALTER TABLE " + tableName + " DROP CONSTRAINT ' + @ConstraintName) ";

            //Solutions for dropping column constraints on temp tables, when either SQL server bug is fixed or DAC connection is used
            //var queryForTempTableAsItShouldBe = "DECLARE @ConstraintName nvarchar(200) " +
            //                   "SELECT @ConstraintName = Name FROM tempdb.SYS.DEFAULT_CONSTRAINTS " +
            //                   "WHERE PARENT_OBJECT_ID = OBJECT_ID(N'tempdb.." + tableName + "') " +
            //                   "AND PARENT_COLUMN_ID = (SELECT column_id FROM tempdb.sys.columns WHERE NAME = N'" + Name + "' AND object_id = OBJECT_ID(N'tempdb.." + tableName + "')) " +
            //                   "IF @ConstraintName IS NOT NULL " +
            //                   "EXEC('ALTER TABLE tempdb.." + tableName + " DROP CONSTRAINT ' + @ConstraintName) ";
            //var queryForTempTableAsAlternative = "DECLARE @ConstraintName nvarchar(200) " +
            //                   "SELECT @ConstraintName = Name FROM tempdb.sys.objects " +
            //                   "WHERE type = 'D ' AND parent_object_id <> 0 " +
            //                   "AND PARENT_COLUMN_ID = (SELECT column_id FROM tempdb.sys.columns WHERE NAME = N'" + Name + "' AND object_id = OBJECT_ID(N'tempdb.." + tableName + "')) " +
            //                   "IF @ConstraintName IS NOT NULL " +
            //                   "EXEC('ALTER TABLE tempdb.." + tableName + " DROP CONSTRAINT ' + @ConstraintName) ";

            database.RawExecute(dropConstraints);                
            database.RawExecute(string.Format("alter table {0} drop column {1};", database.FormatTableNameAndEscape(Table.Name), database.Escape(Name)));
        }
    }
}