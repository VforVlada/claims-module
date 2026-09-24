using ClaimsModule.IntegrationTests.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ClaimsModule.IntegrationTests.PersistenceTests;

/// <summary>I-DB-02 and I-DB-03: the physical schema, read back from INFORMATION_SCHEMA, is what the model specifies.</summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class SchemaTests(ApiWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private sealed record DbColumn(string Table, string Column, string DataType, int? MaxLength, byte? Precision, int? Scale, short? DateTimePrecision);

    private async Task<List<DbColumn>> ReadColumnsAsync()
    {
        await using var connection = new SqlConnection(Factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION, NUMERIC_SCALE, DATETIME_PRECISION
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME <> '__EFMigrationsHistory'
            """;
        var columns = new List<DbColumn>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(new DbColumn(
                reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetInt32(3),
                reader.IsDBNull(4) ? null : reader.GetByte(4),
                reader.IsDBNull(5) ? null : reader.GetInt32(5),
                reader.IsDBNull(6) ? null : reader.GetInt16(6)));
        }

        return columns;
    }

    [Fact]
    public async Task MoneyColumns_AreDecimal19_4()
    {
        var decimals = (await ReadColumnsAsync()).Where(c => c.DataType == "decimal").ToList();

        Assert.NotEmpty(decimals);
        Assert.All(decimals, c => Assert.True(c.Precision == 19 && c.Scale == 4, $"{c.Table}.{c.Column} is decimal({c.Precision},{c.Scale})"));
    }

    [Fact]
    public async Task Timestamps_AreDateTimeOffset7_WithNoLegacyDateTimeTypes()
    {
        var columns = await ReadColumnsAsync();

        Assert.DoesNotContain(columns, c => c.DataType is "datetime" or "datetime2" or "smalldatetime" or "date");
        var offsets = columns.Where(c => c.DataType == "datetimeoffset").ToList();
        Assert.NotEmpty(offsets);
        Assert.All(offsets, c => Assert.True(c.DateTimePrecision == 7, $"{c.Table}.{c.Column} is datetimeoffset({c.DateTimePrecision})"));
    }

    /// <summary>Every string column is NVARCHAR with exactly the length the EF configuration specifies.</summary>
    [Fact]
    public async Task StringColumns_AreNVarCharWithConfiguredLengths()
    {
        var actual = (await ReadColumnsAsync()).ToDictionary(c => (c.Table, c.Column));
        await using var context = Factory.CreateDbContext();
        var relationalModel = context.GetService<IDesignTimeModel>().Model.GetRelationalModel();

        Assert.DoesNotContain(actual.Values, c => c.DataType is "varchar" or "char" or "text" or "ntext");

        var checkedColumns = 0;
        foreach (var table in relationalModel.Tables.Where(t => t.Schema is null or "dbo"))
        {
            foreach (var column in table.Columns.Where(c => c.StoreType.StartsWith("nvarchar", StringComparison.OrdinalIgnoreCase)))
            {
                var expectedLength = column.StoreType.Contains("max", StringComparison.OrdinalIgnoreCase)
                    ? -1
                    : int.Parse(column.StoreType[(column.StoreType.IndexOf('(') + 1)..column.StoreType.IndexOf(')')]);
                var db = actual[(table.Name, column.Name)];
                Assert.True(db.DataType == "nvarchar" && db.MaxLength == expectedLength,
                    $"{table.Name}.{column.Name}: model says {column.StoreType}, database has {db.DataType}({db.MaxLength})");
                checkedColumns++;
            }
        }

        Assert.True(checkedColumns > 20, $"Only {checkedColumns} string columns were checked — the model lookup is probably broken.");
    }

    /// <summary>I-DB-03: tenant, audit and soft-delete columns exist on every table.</summary>
    [Fact]
    public async Task EveryTable_HasTenantAuditAndSoftDeleteColumns()
    {
        string[] required = ["OrganizationEntityId", "CreatedAt", "UserCreated", "UpdatedAt", "UserModified", "IsDeleted", "DeletedAt"];
        var byTable = (await ReadColumnsAsync()).GroupBy(c => c.Table).ToList();

        Assert.True(byTable.Count >= 10, $"Only {byTable.Count} tables found");
        Assert.All(byTable, table =>
        {
            var missing = required.Except(table.Select(c => c.Column)).ToList();
            Assert.True(missing.Count == 0, $"{table.Key} is missing {string.Join(", ", missing)}");
        });
    }
}
