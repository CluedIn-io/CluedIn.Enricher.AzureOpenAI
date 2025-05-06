using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CluedIn.Core;
using CluedIn.Core.DataStore;
using Microsoft.Data.SqlClient;

namespace CluedIn.ExternalSearch.Providers.AzureOpenAI
{
    internal class DistributedLock : IDisposable
    {
        private readonly SqlConnection _connection;
        private readonly SqlTransaction _transaction;

        public DistributedLock(ExecutionContext context, string lockName, bool exclusive = false)
        {
            var connectionStringKey = context.ApplicationContext.System.DataShards.GetDataShard(DataShardType.Locking).ReadConnectionString;
            var connectionString = context.ApplicationContext.System.ConnectionStrings.GetConnectionString(connectionStringKey);

            _connection = new SqlConnection(connectionString);
            _connection.Open();
            _transaction = _connection.BeginTransaction();
            using var cmd = new SqlCommand("sp_getapplock", _connection);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Transaction = _transaction;

            cmd.Parameters.Add(new SqlParameter("@Resource", SqlDbType.NVarChar, 255) { Value = GetType().FullName });
            cmd.Parameters.Add(new SqlParameter("@LockMode", SqlDbType.NVarChar, 32) { Value = exclusive ? "Exclusive" : "Shared" });
            cmd.Parameters.Add(new SqlParameter("@LockTimeout", SqlDbType.Int) { Value = -1 });
            cmd.Parameters.Add(new SqlParameter("@Result", SqlDbType.Int) { Direction = ParameterDirection.ReturnValue });

            cmd.ExecuteNonQuery();

            var queryResult = (int)cmd.Parameters["@Result"].Value;

            if (queryResult < 0)
            {
                throw new ApplicationException($"sp_getapplock returned {queryResult}");    // should never happen as @LockTimeout is -1
            }
        }

        public void Dispose()
        {
            _transaction.Rollback();
            _connection.Close();
        }
    }
}
