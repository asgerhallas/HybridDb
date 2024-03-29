﻿using System;
using Dapper;

namespace HybridDb.Commands
{
    public static class DocumentWriteCommand
    {
        public static void Execute(DocumentTransaction tx, SqlDatabaseCommand preparedCommand)
        {
            tx.Store.Stats.NumberOfRequests++;
            tx.Store.Stats.NumberOfCommands++;

            // NOTE: Sql parameter threshold is actually lower than the stated 2100 (or maybe extra 
            // params are added some where in the stack) so we cut it some slack and say 2000.
            if (preparedCommand.Parameters.Count >= 2000)
            {
                throw new InvalidOperationException("Cannot execute a single command with more than 2000 parameters.");
            }

            var rowcount = tx.SqlConnection.Execute(preparedCommand.Sql, preparedCommand.Parameters, tx.SqlTransaction);

            if (rowcount != preparedCommand.ExpectedRowCount)
            {
                throw new ConcurrencyException(
                    $"Someone beat you to it. Expected {preparedCommand.ExpectedRowCount} changes, but got {rowcount}. " +
                    $"The transaction is rolled back now, so no changes were actually made.");
            }

            tx.Store.Stats.LastWrittenEtag = tx.CommitId;
        }
    }
}