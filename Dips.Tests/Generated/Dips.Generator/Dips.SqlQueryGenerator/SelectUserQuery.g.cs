using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Npgsql;
using Dapper;

namespace Dips.Generator.SqlQueries
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
-- result: Dips.Tests.Models.User
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

        // Result type: Dips.Tests.Models.User
    }
    /// <summary>
    /// Extension methods for executing the query with Dapper.
    /// </summary>
    public static partial class DapperExtensions
    {
        /// <summary>
        /// Executes the SelectUserQuery and returns the results.
        /// </summary>
        public static IEnumerable<Dips.Tests.Models.User> Query(this IDbConnection connection, SelectUserQuery.Input parameters, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            return connection.Query<Dips.Tests.Models.User>(SelectUserQuery.Sql, parameters, transaction, commandTimeout: commandTimeout);
        }

        /// <summary>
        /// Executes the SelectUserQuery asynchronously and returns the results.
        /// </summary>
        public static Task<IEnumerable<Dips.Tests.Models.User>> QueryAsync(this IDbConnection connection, SelectUserQuery.Input parameters, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            return connection.QueryAsync<Dips.Tests.Models.User>(SelectUserQuery.Sql, parameters, transaction, commandTimeout: commandTimeout);
        }

        /// <summary>
        /// Executes the SelectUserQuery and returns a single result.
        /// </summary>
        public static Dips.Tests.Models.User QuerySingle(this IDbConnection connection, SelectUserQuery.Input parameters, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            return connection.QuerySingle<Dips.Tests.Models.User>(SelectUserQuery.Sql, parameters, transaction, commandTimeout: commandTimeout);
        }

        /// <summary>
        /// Executes the SelectUserQuery asynchronously and returns a single result.
        /// </summary>
        public static Task<Dips.Tests.Models.User> QuerySingleAsync(this IDbConnection connection, SelectUserQuery.Input parameters, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            return connection.QuerySingleAsync<Dips.Tests.Models.User>(SelectUserQuery.Sql, parameters, transaction, commandTimeout: commandTimeout);
        }
    }
}
