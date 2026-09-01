using System.Data;
using Microsoft.EntityFrameworkCore;
using VeriScan.Application.Abstractions;

namespace VeriScan.Infrastructure.Persistence.Repositories;

public sealed partial class AdminReadStore
{
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

            var fromParameter = command.CreateParameter();
            fromParameter.ParameterName = "from";
            fromParameter.DbType = DbType.DateTimeOffset;
            fromParameter.Value = from;
            command.Parameters.Add(fromParameter);

            var throughParameter = command.CreateParameter();
            throughParameter.ParameterName = "through";
            throughParameter.DbType = DbType.DateTimeOffset;
            throughParameter.Value = through;
            command.Parameters.Add(throughParameter);

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
}
