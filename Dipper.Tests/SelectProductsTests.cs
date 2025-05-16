using Dipper.Generated.SqlQueries;
using Npgsql;

namespace Dipper.Tests;

public class SelectProductsTests
{
    [Fact]
    public async Task TestSelectProduct()
    {
        await using var conn = new NpgsqlConnection("");
        var result = await conn.QueryAsync(new SelectProductsQuery.Input { Id  = Guid.NewGuid() });
    }
}