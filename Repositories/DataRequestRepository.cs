using Npgsql;

namespace SunflowerApi.Repositories
{
    public class DataRequestRepository : IDataRequestRepository
    {
        private readonly string _connString;

        // Max message length enforced again at DB layer as a second line of defence
        private const int MaxMessageLength = 2000;

        public DataRequestRepository(IConfiguration cfg)
        {
            _connString = cfg.GetConnectionString("DefaultConnection")!;
        }

        public async Task<Guid> InsertDataRequestAsync(
            string userId,
            string message,
            CancellationToken ct)
        {
            // Hard-truncate as a final safety net — validation should have caught this first
            if (message.Length > MaxMessageLength)
                message = message[..MaxMessageLength];

            await using var conn = new NpgsqlConnection(_connString);
            await conn.OpenAsync(ct);

            // Fully parameterised — no string interpolation anywhere near SQL.
            // CockroachDB / Postgres gen_random_uuid() returns the new id so we
            // can hand it back to the caller without a second round-trip.
            const string sql = """
                INSERT INTO public.data_requests (user_id, message)
                VALUES (@userId, @message)
                RETURNING request_id;
            """;

            await using var cmd = new NpgsqlCommand(sql, conn);

            // NpgsqlDbType forces the wire type, preventing any type-confusion attack
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter("userId", NpgsqlTypes.NpgsqlDbType.Text) { Value = userId });
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter("message", NpgsqlTypes.NpgsqlDbType.Text) { Value = message });

            var result = await cmd.ExecuteScalarAsync(ct);
            return result is Guid id ? id : Guid.Empty;
        }
    }
}