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

namespace Dipper.Generator;

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
        sourceBuilder.AppendLine("namespace Dipper.Generated.SqlQueries");
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
        return SqlToCSharpTypeMap.GetValueOrDefault(sqlType.ToLower(), "string");
    }

    /// <summary>
    /// Replace parameters in the format @Name:type with @Name for actual SQL execution
    /// </summary>
    private string CleanupSqlForConstant(string sql)
    {
        return Regex.Replace(sql, @"@(\w+):(\w+)", m => $"@{m.Groups[1].Value}");
    }
}