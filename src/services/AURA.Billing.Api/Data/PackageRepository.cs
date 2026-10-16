using AURA.Billing.Api.Models;
using Npgsql;

namespace AURA.Billing.Api.Data;

public sealed class PackageRepository(NpgsqlDataSource dataSource)
{
    private long _readCount;

    public long ReadCount => Interlocked.Read(ref _readCount);

    public async Task VerifyDatabaseAsync(
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, name, price, credits, updated_at
            FROM billing_poc.service_packages
            LIMIT 0;
            """;

        await using var command = dataSource.CreateCommand(sql);
        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PackageDto>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, name, price, credits
            FROM billing_poc.service_packages
            ORDER BY id;
            """;

        Interlocked.Increment(ref _readCount);

        await using var command = dataSource.CreateCommand(sql);
        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        var packages = new List<PackageDto>();

        while (await reader.ReadAsync(cancellationToken))
        {
            packages.Add(new PackageDto(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetDecimal(2),
                reader.GetInt32(3)));
        }

        return packages;
    }

    public async Task<PackageDto?> UpdateAsync(
        int id,
        UpdatePackageRequest request,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE billing_poc.service_packages
            SET name = @name,
                price = @price,
                credits = @credits,
                updated_at = now()
            WHERE id = @id
            RETURNING id, name, price, credits;
            """;

        await using var command = dataSource.CreateCommand(sql);

        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("name", request.Name.Trim());
        command.Parameters.AddWithValue("price", request.Price);
        command.Parameters.AddWithValue("credits", request.Credits);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new PackageDto(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetDecimal(2),
            reader.GetInt32(3));
    }
}