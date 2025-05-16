using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Dips;

[Generator]
public class SqlQueryGenerator : IIncrementalGenerator
{
    private static readonly Dictionary<string, string> SqlToCSharpTypeMap = new()
    {
        { "text", "string" },
        { "uuid", "Guid" },
        { "int4", "int" },
        { "int8", "long" },
        { "numeric", "decimal" },
        { "bool", "bool" },
        { "timestamp", "DateTime" },
        { "timestamptz", "DateTimeOffset" },
        { "json", "string" },
        { "jsonb", "string" },
        { "date", "DateTime" },
        { "time", "TimeSpan" },
        { "float4", "float" },
        { "float8", "double" },
        { "bytea", "byte[]" },
        { "char", "char" },
        { "varchar", "string" }
    };

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Register the SQL files as additional texts
        var sqlFiles = context.AdditionalTextsProvider
            .Where((file) => file.Path.EndsWith(".sql", StringComparison.OrdinalIgnoreCase));

        // Transform the SQL files into their generated C# representations
        var generatedSources = sqlFiles.Select((file, ct) => new
        {
            FileName = Path.GetFileNameWithoutExtension(file.Path),
            Content = file.GetText(ct)!.ToString()
        }).Select((file, ct) => GenerateSource(file.FileName, file.Content, ct));

        // Register the source output
        context.RegisterSourceOutput(generatedSources,
            (spc, source) =>
            {
                spc.AddSource($"{source.ClassName}.g.cs", SourceText.From(source.SourceCode, Encoding.UTF8));
            });
    }

    private (string ClassName, string SourceCode) GenerateSource(string fileName, string sqlContent,
        CancellationToken ct)
    {
        // Parse SQL metadata
        var name = GetMetadataValue(sqlContent, "name");
        if (string.IsNullOrEmpty(name))
        {
            name = fileName;
        }

        var resultType = GetMetadataValue(sqlContent, "result");
        var comment = GetMetadataValue(sqlContent, sqlContent);

        // Parse parameters
        var parameters = ParseParameters(sqlContent);

        // Generate output class name
        var className = $"{name}Query";

        // Build the source code
        var sourceBuilder = new StringBuilder();
        sourceBuilder.AppendLine("using System;");
        sourceBuilder.AppendLine("using System.Collections.Generic;");
        sourceBuilder.AppendLine("using System.Data;");
        sourceBuilder.AppendLine("using System.Linq;");
        sourceBuilder.AppendLine("using System.Text;");
        sourceBuilder.AppendLine("using System.Threading.Tasks;");
        sourceBuilder.AppendLine("using Npgsql;");
        sourceBuilder.AppendLine("using Dapper;");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("namespace Dips.Generator.SqlQueries");
        sourceBuilder.AppendLine("{");

        // Generate the Query class
        sourceBuilder.AppendLine($"    /// <summary>");
        sourceBuilder.AppendLine($"    /// {comment}");
        sourceBuilder.AppendLine($"    /// </summary>");
        sourceBuilder.AppendLine($"    public class {className}");
        sourceBuilder.AppendLine("    {");

        // SQL constant
        sourceBuilder.AppendLine("        /// <summary>");
        sourceBuilder.AppendLine("        /// The SQL query text.");
        sourceBuilder.AppendLine("        /// </summary>");
        sourceBuilder.AppendLine("        public const string Sql = @\"");
        sourceBuilder.AppendLine(CleanupSqlForConstant(sqlContent));
        sourceBuilder.AppendLine("\";");
        sourceBuilder.AppendLine();

        // Generate Input class
        GenerateInputClass(sourceBuilder, parameters);

        // Generate optional Output class if resultType is specified
        if (!string.IsNullOrEmpty(resultType))
        {
            sourceBuilder.AppendLine($"        // Result type: {resultType}");
        }

        // Generate where conditions class if the SQL contains WHERE
        //if (sqlContent.Contains("WHERE") || sqlContent.Contains("where"))
        //{
        //    GenerateWhereClass(sourceBuilder);
        //}

        // Close the main query class
        sourceBuilder.AppendLine("    }");

        // Generate extension methods for Dapper
        GenerateDapperExtensions(sourceBuilder, className, resultType);

        // Close namespace
        sourceBuilder.AppendLine("}");

        return (className, sourceBuilder.ToString());
    }

    private void GenerateInputClass(StringBuilder sourceBuilder, List<(string Name, string Type)> parameters)
    {
        sourceBuilder.AppendLine("        /// <summary>");
        sourceBuilder.AppendLine("        /// Input parameters for the query.");
        sourceBuilder.AppendLine("        /// </summary>");
        sourceBuilder.AppendLine("        public class Input");
        sourceBuilder.AppendLine("        {");

        foreach (var param in parameters)
        {
            string csharpType = MapSqlTypeToCSharp(param.Type);
            sourceBuilder.AppendLine($"            /// <summary>");
            sourceBuilder.AppendLine($"            /// The {param.Name} parameter.");
            sourceBuilder.AppendLine($"            /// </summary>");
            sourceBuilder.AppendLine($"            public {csharpType} {param.Name} {{ get; set; }}");
            sourceBuilder.AppendLine();
        }

        sourceBuilder.AppendLine("        }");
        sourceBuilder.AppendLine();
    }

    private void GenerateWhereClass(StringBuilder sourceBuilder)
    {
        sourceBuilder.AppendLine("        /// <summary>");
        sourceBuilder.AppendLine("        /// Where conditions for the query.");
        sourceBuilder.AppendLine("        /// </summary>");
        sourceBuilder.AppendLine("        public class Where");
        sourceBuilder.AppendLine("        {");
        sourceBuilder.AppendLine("            private readonly StringBuilder _whereClause = new StringBuilder();");
        sourceBuilder.AppendLine(
            "            private readonly Dictionary<string, object> _parameters = new Dictionary<string, object>();");
        sourceBuilder.AppendLine("            private bool _hasCondition = false;");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("            /// <summary>");
        sourceBuilder.AppendLine("            /// Adds a condition to the where clause if the value is not null.");
        sourceBuilder.AppendLine("            /// </summary>");
        sourceBuilder.AppendLine(
            "            public Where AddConditionIfNotNull<T>(string fieldName, string operation, T value, string paramName = null)");
        sourceBuilder.AppendLine("            {");
        sourceBuilder.AppendLine("                if (value != null)");
        sourceBuilder.AppendLine("                {");
        sourceBuilder.AppendLine("                    string pName = paramName ?? $\"p{_parameters.Count}\";");
        sourceBuilder.AppendLine("                    if (!_hasCondition)");
        sourceBuilder.AppendLine("                    {");
        sourceBuilder.AppendLine("                        _whereClause.Append(\" WHERE \");");
        sourceBuilder.AppendLine("                        _hasCondition = true;");
        sourceBuilder.AppendLine("                    }");
        sourceBuilder.AppendLine("                    else");
        sourceBuilder.AppendLine("                    {");
        sourceBuilder.AppendLine("                        _whereClause.Append(\" AND \");");
        sourceBuilder.AppendLine("                    }");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("                    _whereClause.Append($\"{fieldName} {operation} @{pName}\");");
        sourceBuilder.AppendLine("                    _parameters.Add(pName, value);");
        sourceBuilder.AppendLine("                }");
        sourceBuilder.AppendLine("                return this;");
        sourceBuilder.AppendLine("            }");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("            /// <summary>");
        sourceBuilder.AppendLine(
            "            /// Adds a condition to the where clause if the value satisfies a predicate.");
        sourceBuilder.AppendLine("            /// </summary>");
        sourceBuilder.AppendLine(
            "            public Where AddConditionIf<T>(string fieldName, string operation, T value, Func<T, bool> predicate, string paramName = null)");
        sourceBuilder.AppendLine("            {");
        sourceBuilder.AppendLine("                if (predicate(value))");
        sourceBuilder.AppendLine("                {");
        sourceBuilder.AppendLine(
            "                    return AddConditionIfNotNull(fieldName, operation, value, paramName);");
        sourceBuilder.AppendLine("                }");
        sourceBuilder.AppendLine("                return this;");
        sourceBuilder.AppendLine("            }");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("            /// <summary>");
        sourceBuilder.AppendLine("            /// Gets the WHERE clause SQL.");
        sourceBuilder.AppendLine("            /// </summary>");
        sourceBuilder.AppendLine("            public string GetSql() => _whereClause.ToString();");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("            /// <summary>");
        sourceBuilder.AppendLine("            /// Gets the parameters for the WHERE clause.");
        sourceBuilder.AppendLine("            /// </summary>");
        sourceBuilder.AppendLine("            public object GetParameters() => _parameters;");
        sourceBuilder.AppendLine("        }");
        sourceBuilder.AppendLine();
    }

    private void GenerateDapperExtensions(StringBuilder sourceBuilder, string className, string resultType)
    {
        sourceBuilder.AppendLine("    /// <summary>");
        sourceBuilder.AppendLine("    /// Extension methods for executing the query with Dapper.");
        sourceBuilder.AppendLine("    /// </summary>");
        sourceBuilder.AppendLine("    public static partial class DapperExtensions");
        sourceBuilder.AppendLine("    {");

        // Define the return type
        string returnType = string.IsNullOrEmpty(resultType) ? "IEnumerable<dynamic>" : $"IEnumerable<{resultType}>";
        string asyncReturnType = string.IsNullOrEmpty(resultType)
            ? "Task<IEnumerable<dynamic>>"
            : $"Task<IEnumerable<{resultType}>>";
        string singleReturnType = string.IsNullOrEmpty(resultType) ? "dynamic" : resultType;
        string asyncSingleReturnType = string.IsNullOrEmpty(resultType) ? "Task<dynamic>" : $"Task<{resultType}>";

        // Extension method for query execution
        sourceBuilder.AppendLine("        /// <summary>");
        sourceBuilder.AppendLine($"        /// Executes the {className} and returns the results.");
        sourceBuilder.AppendLine("        /// </summary>");
        sourceBuilder.AppendLine(
            $"        public static {returnType} Query(this IDbConnection connection, {className}.Input parameters, IDbTransaction transaction = null, int? commandTimeout = null)");
        sourceBuilder.AppendLine("        {");
        sourceBuilder.AppendLine(
            $"            return connection.Query{(string.IsNullOrEmpty(resultType) ? "" : $"<{resultType}>")}({className}.Sql, parameters, transaction, commandTimeout: commandTimeout);");
        sourceBuilder.AppendLine("        }");
        sourceBuilder.AppendLine();

        // Extension method for query execution with WHERE clause
        //sourceBuilder.AppendLine("        /// <summary>");
        //sourceBuilder.AppendLine(
        //    $"        /// Executes the {className} with where conditions and returns the results.");
        //sourceBuilder.AppendLine("        /// </summary>");
        //sourceBuilder.AppendLine(
        //    $"        public static {returnType} Execute{className}(this IDbConnection connection, {className}.Input parameters, {className}.Where whereConditions, IDbTransaction transaction = null, int? commandTimeout = null)");
        //sourceBuilder.AppendLine("        {");
        //sourceBuilder.AppendLine("            var sql = $\"{" + className + ".Sql}{whereConditions.GetSql()}\";");
        //sourceBuilder.AppendLine("            var combinedParams = new DynamicParameters(parameters);");
        //sourceBuilder.AppendLine("            combinedParams.AddDynamicParams(whereConditions.GetParameters());");
        //sourceBuilder.AppendLine(
        //    $"            return connection.Query{(string.IsNullOrEmpty(resultType) ? "" : $"<{resultType}>")}(sql, combinedParams, transaction, commandTimeout: commandTimeout);");
        //sourceBuilder.AppendLine("        }");
        //sourceBuilder.AppendLine();

        // Async extension method
        sourceBuilder.AppendLine("        /// <summary>");
        sourceBuilder.AppendLine($"        /// Executes the {className} asynchronously and returns the results.");
        sourceBuilder.AppendLine("        /// </summary>");
        sourceBuilder.AppendLine(
            $"        public static {asyncReturnType} QueryAsync(this IDbConnection connection, {className}.Input parameters, IDbTransaction transaction = null, int? commandTimeout = null)");
        sourceBuilder.AppendLine("        {");
        sourceBuilder.AppendLine(
            $"            return connection.QueryAsync{(string.IsNullOrEmpty(resultType) ? "" : $"<{resultType}>")}({className}.Sql, parameters, transaction, commandTimeout: commandTimeout);");
        sourceBuilder.AppendLine("        }");
        sourceBuilder.AppendLine();

        // Async extension method with WHERE clause
        //sourceBuilder.AppendLine("        /// <summary>");
        //sourceBuilder.AppendLine(
        //    $"        /// Executes the {className} asynchronously with where conditions and returns the results.");
        //sourceBuilder.AppendLine("        /// </summary>");
        //sourceBuilder.AppendLine(
        //    $"        public static {asyncReturnType} Execute{className}Async(this IDbConnection connection, {className}.Input parameters, {className}.Where whereConditions, IDbTransaction transaction = null, int? commandTimeout = null)");
        //sourceBuilder.AppendLine("        {");
        //sourceBuilder.AppendLine("            var sql = $\"{" + className + ".Sql}{whereConditions.GetSql()}\";");
        //sourceBuilder.AppendLine("            var combinedParams = new DynamicParameters(parameters);");
        //sourceBuilder.AppendLine("            combinedParams.AddDynamicParams(whereConditions.GetParameters());");
        //sourceBuilder.AppendLine(
        //    $"            return connection.QueryAsync{(string.IsNullOrEmpty(resultType) ? "" : $"<{resultType}>")}(sql, combinedParams, transaction, commandTimeout: commandTimeout);");
        //sourceBuilder.AppendLine("        }");
        //sourceBuilder.AppendLine();

        // Single result extension method
        sourceBuilder.AppendLine("        /// <summary>");
        sourceBuilder.AppendLine($"        /// Executes the {className} and returns a single result.");
        sourceBuilder.AppendLine("        /// </summary>");
        sourceBuilder.AppendLine(
            $"        public static {singleReturnType} QuerySingle(this IDbConnection connection, {className}.Input parameters, IDbTransaction transaction = null, int? commandTimeout = null)");
        sourceBuilder.AppendLine("        {");
        sourceBuilder.AppendLine(
            $"            return connection.QuerySingle{(string.IsNullOrEmpty(resultType) ? "" : $"<{resultType}>")}({className}.Sql, parameters, transaction, commandTimeout: commandTimeout);");
        sourceBuilder.AppendLine("        }");
        sourceBuilder.AppendLine();

        // Async single result extension method
        sourceBuilder.AppendLine("        /// <summary>");
        sourceBuilder.AppendLine($"        /// Executes the {className} asynchronously and returns a single result.");
        sourceBuilder.AppendLine("        /// </summary>");
        sourceBuilder.AppendLine(
            $"        public static {asyncSingleReturnType} QuerySingleAsync(this IDbConnection connection, {className}.Input parameters, IDbTransaction transaction = null, int? commandTimeout = null)");
        sourceBuilder.AppendLine("        {");
        sourceBuilder.AppendLine(
            $"            return connection.QuerySingleAsync{(string.IsNullOrEmpty(resultType) ? "" : $"<{resultType}>")}({className}.Sql, parameters, transaction, commandTimeout: commandTimeout);");
        sourceBuilder.AppendLine("        }");

        sourceBuilder.AppendLine("    }");
    }

    private string GetMetadataValue(string sqlContent, string metadataName)
    {
        var regex = new Regex($"--\\s*{metadataName}:\\s*(.+)$", RegexOptions.Multiline);
        var match = regex.Match(sqlContent);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    private List<(string Name, string Type)> ParseParameters(string sqlContent)
    {
        var result = new List<(string Name, string Type)>();
        var regex = new Regex(@"@(\w+):(\w+)", RegexOptions.Multiline);

        foreach (Match match in regex.Matches(sqlContent))
        {
            var paramName = match.Groups[1].Value;
            var paramType = match.Groups[2].Value;

            if (result.All(p => p.Name != paramName))
                result.Add((paramName, paramType));
        }

        return result;
    }

    private string MapSqlTypeToCSharp(string sqlType)
    {
        if (SqlToCSharpTypeMap.TryGetValue(sqlType.ToLower(), out var csharpType))
        {
            return csharpType;
        }

        // Default to string if type mapping not found
        return "string";
    }

    private string CleanupSqlForConstant(string sql)
    {
        // return sql;
        // Replace parameters in the format @Name:type with @Name for actual SQL execution
        return Regex.Replace(sql, @"@(\w+):(\w+)", m => $"@{m.Groups[1].Value}");
    }
}