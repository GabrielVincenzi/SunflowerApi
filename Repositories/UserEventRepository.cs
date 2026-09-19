using Npgsql;
using NpgsqlTypes;
using System.Data;
using SunflowerApi.Models;

namespace SunflowerApi.Repositories
{
    public class UserEventRepository : IUserEventRepository
    {
        private readonly string _connString;

        public UserEventRepository(IConfiguration cfg)
        {
            _connString = cfg.GetConnectionString("DefaultConnection")!;
        }

        public async Task<List<(Guid ChartId, DateTime SavedAt)>> GetSavedAsync(
        string userId,
        CancellationToken ct)
        {
            var list = new List<(Guid, DateTime)>();

            await using var conn = new NpgsqlConnection(_connString);
            await conn.OpenAsync(ct);

            const string sql = """
                SELECT object_id, MAX(event_time) AS event_time
                FROM public.saved
                WHERE user_id = @u
                GROUP BY object_id
                ORDER BY event_time DESC;
            """;

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("u", userId);

            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                if (!Guid.TryParse(r[0]?.ToString(), out var id)) continue;
                var time = r.IsDBNull(1) ? DateTime.MinValue : r.GetDateTime(1);
                list.Add((id, time));
            }

            return list;
        }

        public async Task<List<Guid>> GetSavedEventIdsAsync(string userId, CancellationToken ct)
        {
            var saved = await GetSavedAsync(userId, ct);
            return saved.Select(x => x.ChartId).ToList();
        }

        public async Task<Dictionary<Guid, Dictionary<string, object?>>> GetChartsByIdsAsync(
            Guid[] ids,
            string lang,
            CancellationToken ct)
        {
            var dict = new Dictionary<Guid, Dictionary<string, object?>>();

            if (ids.Length == 0)
                return dict;

            await using var conn = new NpgsqlConnection(_connString);
            await conn.OpenAsync(ct);

            const string sql = """
                SELECT
                    c.chart_id, c.id, ct.title, ct.description,
                    ct.category, c.chart_type, c.vars, c.db_name
                FROM public.charts c
                LEFT JOIN public.charts_text ct
                ON ct.chart_id = c.chart_id AND ct.lang = @lang
                WHERE c.chart_id = ANY(@ids);
            """;

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid, ids);
            cmd.Parameters.AddWithValue("lang", lang);

            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < r.FieldCount; i++)
                    row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);

                dict[r.GetGuid(0)] = row;
            }

            return dict;
        }

        public async Task<int> InsertEventAsync(
            string userId, string action, Guid objectId, CancellationToken ct)
        {
            await using var conn = new NpgsqlConnection(_connString);
            await conn.OpenAsync(ct);

            const string sql = """
                INSERT INTO public.events (user_id, action, object_id)
                VALUES (@u, @a, @o);
            """;

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("u", userId);
            cmd.Parameters.AddWithValue("a", action);
            cmd.Parameters.AddWithValue("o", objectId);

            return await cmd.ExecuteNonQueryAsync(ct);
        }

        public async Task<int> InsertSavedAsync(
            string userId, Guid objectId, CancellationToken ct)
        {
            await using var conn = new NpgsqlConnection(_connString);
            await conn.OpenAsync(ct);

            const string sql = """
                INSERT INTO public.saved (user_id, object_id)
                VALUES (@u, @o)
                ON CONFLICT (user_id, object_id) DO NOTHING;
            """;

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("u", userId);
            cmd.Parameters.AddWithValue("o", objectId);

            return await cmd.ExecuteNonQueryAsync(ct);
        }

        public async Task<int> DeleteSavedAsync(string userId, Guid objectId, CancellationToken ct)
        {
            await using var conn = new NpgsqlConnection(_connString);
            await conn.OpenAsync(ct);

            const string sql = """
                DELETE FROM public.saved
                WHERE user_id=@u AND object_id=@o;
            """;

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("u", userId);
            cmd.Parameters.AddWithValue("o", objectId);

            return await cmd.ExecuteNonQueryAsync(ct);
        }

        public async Task<List<UserEvent>> GetEventsByFilterAsync(
            string userId,
            string action,
            Guid? objectId,
            DateTime? from,
            DateTime? to,
            CancellationToken ct)
        {
            var list = new List<UserEvent>();

            await using var conn = new NpgsqlConnection(_connString);
            await conn.OpenAsync(ct);

            var sql = """
                SELECT action, object_id, event_time
                FROM   public.events
                WHERE  user_id = @u
                AND  action  = @a
            """;

            if (objectId.HasValue) sql += " AND object_id  = @o";
            if (from.HasValue) sql += " AND event_time >= @from";
            if (to.HasValue) sql += " AND event_time <= @to";

            sql += " ORDER BY event_time DESC;";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("u", userId);
            cmd.Parameters.AddWithValue("a", action);

            if (objectId.HasValue) cmd.Parameters.AddWithValue("o", objectId.Value);
            if (from.HasValue) cmd.Parameters.AddWithValue("from", from.Value);
            if (to.HasValue) cmd.Parameters.AddWithValue("to", to.Value);

            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                list.Add(new UserEvent
                {
                    Action = r.GetString(0),
                    ObjectId = r.GetGuid(1),
                    Timestamp = r.GetDateTime(2)
                });
            }

            return list;

        }
    }
}