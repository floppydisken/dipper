# Dipper: The companion Dapper didn't know it needed

A true companion to Dapper for that near-native-sql experience. 
This source generator automatically converts SQL files into strongly typed query classes for use with Dapper.

The generator itself does as little as possible and lets the powerful language of SQL express itself.

This project is an experiment to explore writing .sql files and "compiling" those into strongly typed source files, 
then providing easy to use Dapper extensions for execution quickly turning complicated SQL calls into simple declarative code.

## TODO
- [ ] Is COALESCE enough to handle complex cases
- [ ] How do we handle deep hydration like the lovely EFCore (can we do it elegantly)? 
- [ ] How do we strongly type database column names. Figure out a strategy for one-source-of-truth for column names, 
    so we can discover when a model field does not match the model field.
- [ ] Generated source file naming clashes. Do we care?
- [ ] Make the type mapping extensible so we can add new and exciting types like enums
- [ ] Add a ? to the type syntax to allow for optional fields
- [ ] Release as a nuget package

## Features

- Parses SQL files with metadata comments
- Generates strongly typed input classes based on SQL parameters with type annotations
- Creates extension methods for executing queries with Dapper
- Supports synchronous and asynchronous execution
- Dynamically builds SQL queries based on parameter values
- Supports optional WHERE clauses and other conditional SQL fragments# SQL Query Generator for .NET

This source generator automatically converts SQL files into strongly typed query classes for use with Dapper.

## Features

- Parses SQL files with metadata comments
- Generates strongly typed input classes based on SQL parameters with type annotations
- Creates extension methods for executing queries with Dapper
- Supports synchronous and asynchronous execution
- Handles different return types (single results, collections, or void)

## How to Use

### 1. Add the Source Generator to Your Project

```xml
<ItemGroup>
  <ProjectReference Include="..\Dipper.Generator.csproj" 
                    OutputItemType="Analyzer" 
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

### 3. Use the Generated Classes

```csharp
// Get your database connection
using var connection = new NpgsqlConnection(connectionString);

// Create input parameters
var input = new GetUsersQuery.Input
{
    SearchTerm = "john",  // Optional search term
    IsActive = true       // Optional filter for active users
};

// Execute the query and get strongly typed results
var users = connection.Execute(input);

// Or async
var usersAsync = await connection.ExecuteAsync(input);

// View the SQL that will be executed with the parameters
string sql = GetUsersQuery.GetSql(input);
Console.WriteLine(sql);
```

## SQL File Format

The SQL files should follow this format:

```sql
-- name: QueryName (optional, defaults to filename)
-- result: ResultType (optional, specifies return type)
-- Any other comments
-- SQL statement with @ParameterName:type annotations;
SELECT * FROM users
WHERE id = @Id:uuid
```

### Conditional SQL Filtering with COALESCE

#### The Problem: Optional Query Parameters

When building dynamic SQL queries with optional filter parameters, we often need to handle cases where some parameters may be NULL or unspecified. The traditional approach of concatenating SQL strings can lead to complex, error-prone code:

```csharp
// Problematic approach
var sql = "SELECT * FROM products WHERE 1=1";
if (category != null) sql += " AND category = @Category";
if (maxPrice != null) sql += " AND price <= @MaxPrice";
if (color != null) sql += " AND color = @Color";
// More conditions...
```

This approach is vulnerable to SQL injection and becomes unwieldy as the number of parameters increases.

#### The Solution: COALESCE "Abuse"

We leverage PostgreSQL's COALESCE function in a clever way to create conditional filters that automatically activate or deactivate based on parameter values:

```sql
SELECT * FROM products
WHERE 
  (COALESCE(category = @Category:text, TRUE)) AND
  (COALESCE(price <= @Price:money, TRUE)) AND
  (COALESCE(color = @Color:text, TRUE))
```

##### How It Works

1. **For NULL parameters**: When a parameter is NULL, the comparison (e.g., `category = NULL`) evaluates to NULL, and `COALESCE(NULL, TRUE)` returns TRUE, effectively making the condition inactive.

2. **For non-NULL parameters**: The comparison (e.g., `category = 'Electronics'`) evaluates to TRUE or FALSE normally, and COALESCE returns this value, making the condition active.

#### OR Conditions

For OR conditions, we use the same technique but with FALSE as the default:

```sql
SELECT * FROM products
WHERE
  (COALESCE(color = 'red', FALSE)) OR
  (COALESCE(color = 'blue', FALSE)) OR
  (COALESCE(on_sale = TRUE, FALSE))
```

#### Complex Filtering Example

You can combine AND and OR conditions for sophisticated filtering:

```sql
SELECT * FROM products
WHERE 
  -- These conditions are TRUE when the parameter is NULL
  (COALESCE(category = @Category:text, TRUE)) AND
  (COALESCE(price <= @Price:money, TRUE)) AND
  
  -- At least one of these must be TRUE (if all parameters are NULL, no products match)
  (
    (COALESCE(color = @Color:text, FALSE)) OR 
    (COALESCE(brand = @Brand:text, FALSE)) OR
    (COALESCE(on_sale = @OnSale:bool, FALSE))
  )
```

#### Benefits

- **"Clean-enough", maintainable code**: Single SQL query without complex string concatenation and without introducing 
    new templating syntax for conditional where fields
- **Readability**: Makes the intention of optional filtering clear. The caveat being that COALESCE is not immediately obvious

#### When to Use

This approach is particularly useful for:
- Search interfaces with multiple optional filters
- API endpoints that accept optional query parameters
- Reports with configurable filtering criteria

But can be used for any complexity of SQL queries

### 2. Add SQL Files to Your Project

Create a SQL file with metadata comments and optional conditional blocks:

```sql
-- name: GetProducts
-- result: Models.ProductView
-- Query users with optional filtering
SELECT * FROM products
WHERE 
  COALESCE(category = @Category:text, TRUE) AND
  COALESCE(price <= @Price:money, TRUE) AND
  COALESCE(color = @Color:text, TRUE)
ORDER BY name
```

Make sure to include the SQL files in your project file:

```xml
<ItemGroup>
  <AdditionalFiles Include="**/*.sql" />
</ItemGroup>
```

### Parameter Type Mapping

SQL parameters use the format `@ParameterName:type` where type is mapped to C# types:

| SQL Type | C# Type |
|----------|---------|
| text, varchar, nvarchar, char, nchar | string |
| int, integer | int |
| smallint | short |
| bigint | long |
| tinyint | byte |
| bit, boolean | bool |
| decimal, numeric, money | decimal |
| float | float |
| real | double |
| datetime, date, timestamp | DateTime |
| time | TimeSpan |
| uuid, uniqueidentifier | Guid |
| binary, varbinary, image | byte[] |
| json, jsonb | string |

