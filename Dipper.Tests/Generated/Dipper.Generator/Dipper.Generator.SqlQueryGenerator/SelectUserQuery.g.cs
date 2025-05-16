using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Npgsql;
using Dapper;

namespace Dipper.Generated.SqlQueries
{
    /// <summary>
    /// 
    /// </summary>
    public class SelectUserQuery
    {
        /// <summary>
        /// The SQL query text.
        /// </summary>
        public const string Sql = @"
-- name: SelectUser
-- result: Dipper.Tests.Models.Product
-- Create user record
SELECT * FROM users
WHERE (COALESCE id = @Id, TRUE) AND name = @Name;

";

        /// <summary>
        /// Input parameters for the query.
        /// </summary>
        public class Input
        {
            /// <summary>
            /// The Id parameter.
            /// </summary>
            public Guid Id { get; set; }

            /// <summary>
            /// The Name parameter.
            /// </summary>
            public string Name { get; set; }

        }

        // Result type: Dipper.Tests.Models.Product
    }
    /// <summary>
    /// Extension methods for executing the query with Dapper.
    /// </summary>
    public static partial class DapperExtensions
    {
        /// <summary>
        /// Executes the SelectUserQuery and returns the results.
        /// </summary>
        public static IEnumerable<Dipper.Tests.Models.Product> Query(this IDbConnection connection, SelectUserQuery.Input parameters, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            return connection.Query<Dipper.Tests.Models.Product>(SelectUserQuery.Sql, parameters, transaction, commandTimeout: commandTimeout);
        }

        /// <summary>
        /// Executes the SelectUserQuery asynchronously and returns the results.
        /// </summary>
        public static Task<IEnumerable<Dipper.Tests.Models.Product>> QueryAsync(this IDbConnection connection, SelectUserQuery.Input parameters, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            return connection.QueryAsync<Dipper.Tests.Models.Product>(SelectUserQuery.Sql, parameters, transaction, commandTimeout: commandTimeout);
        }

        /// <summary>
        /// Executes the SelectUserQuery and returns a single result.
        /// </summary>
        public static Dipper.Tests.Models.Product QuerySingle(this IDbConnection connection, SelectUserQuery.Input parameters, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            return connection.QuerySingle<Dipper.Tests.Models.Product>(SelectUserQuery.Sql, parameters, transaction, commandTimeout: commandTimeout);
        }

        /// <summary>
        /// Executes the SelectUserQuery asynchronously and returns a single result.
        /// </summary>
        public static Task<Dipper.Tests.Models.Product> QuerySingleAsync(this IDbConnection connection, SelectUserQuery.Input parameters, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            return connection.QuerySingleAsync<Dipper.Tests.Models.Product>(SelectUserQuery.Sql, parameters, transaction, commandTimeout: commandTimeout);
        }
    }
}
