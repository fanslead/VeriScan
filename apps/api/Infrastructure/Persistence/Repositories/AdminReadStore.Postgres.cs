using System.Data;
using Microsoft.EntityFrameworkCore;
using VeriScan.Application.Abstractions;

namespace VeriScan.Infrastructure.Persistence.Repositories;

public sealed partial class AdminReadStore
{
    private async Task<decimal?> GetPostgresP95LatencyMsAsync(
        DateTimeOffset from,
        DateTimeOffset through,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT percentile_cont(0.95) WITHIN GROUP (
                ORDER BY EXTRACT(EPOCH FROM ("MachineCompletedAt" - "CreatedAt")) * 1000
            )
            FROM "moderation_items"
            WHERE "CreatedAt" >= @from
              AND "CreatedAt" < @through
              AND "MachineCompletedAt" IS NOT NULL;
            """;

        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = dbContext.Database.GetDbConnection().CreateCommand();
            command.CommandType = CommandType.Text;
            command.CommandText = sql;
            AddDateTimeOffsetParameter(command, "from", from);
            AddDateTimeOffsetParameter(command, "through", through);
            var value = await command.ExecuteScalarAsync(cancellationToken);
            return value is null or DBNull
                ? null
                : decimal.Round(Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture), 2);
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    private async Task<IReadOnlyList<AdminOverviewTrendReadData>> GetPostgresTrendAsync(
        DateTimeOffset from,
        DateTimeOffset through,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                EXTRACT(HOUR FROM ("CreatedAt" AT TIME ZONE 'UTC'))::integer AS "Hour",
                COUNT(*)::bigint AS "Total",
                COUNT(*) FILTER (WHERE "Decision" = 'Reject')::bigint AS "Reject",
                COUNT(*) FILTER (WHERE "Decision" = 'Review')::bigint AS "Review"
            FROM "moderation_items"
            WHERE "CreatedAt" >= @from
              AND "CreatedAt" < @through
            GROUP BY EXTRACT(HOUR FROM ("CreatedAt" AT TIME ZONE 'UTC'))
            ORDER BY "Hour";
            """;

        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = dbContext.Database.GetDbConnection().CreateCommand();
            command.CommandType = CommandType.Text;
            command.CommandText = sql;

            AddDateTimeOffsetParameter(command, "from", from);
            AddDateTimeOffsetParameter(command, "through", through);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var trend = new List<AdminOverviewTrendReadData>(capacity: 24);
            while (await reader.ReadAsync(cancellationToken))
            {
                trend.Add(new AdminOverviewTrendReadData(
                    reader.GetInt32(0),
                    reader.GetInt64(1),
                    reader.GetInt64(2),
                    reader.GetInt64(3)));
            }

            return trend;
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    private static void AddDateTimeOffsetParameter(
        System.Data.Common.DbCommand command,
        string name,
        DateTimeOffset value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = DbType.DateTimeOffset;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
