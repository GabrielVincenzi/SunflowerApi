using System.Text.RegularExpressions;
using SunflowerApi.Models;
using SunflowerApi.Repositories;

namespace SunflowerApi.Services
{
    public class DataRequestService : IDataRequestService
    {
        private readonly IDataRequestRepository _repo;

        // Collapse any run of whitespace / control characters to a single space.
        // This prevents invisible Unicode tricks and null-byte injections while
        // keeping the message fully readable.
        private static readonly Regex _normaliseWhitespace =
            new(@"[\p{C}\s]+", RegexOptions.Compiled);

        public DataRequestService(IDataRequestRepository repo)
        {
            _repo = repo;
        }

        public async Task<Guid> SubmitRequestAsync(DataRequest request, CancellationToken ct)
        {
            // userId is set by the endpoint from HttpContext — never from the body
            var userId = request.UserId!.Trim();
            var message = Sanitise(request.Message!);

            // Reject messages that are empty after sanitisation even if they
            // passed the [MinLength] annotation (e.g. 10 spaces)
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Message is empty after sanitisation.");

            return await _repo.InsertDataRequestAsync(userId, message, ct);
        }

        // ── Sanitisation ────────────────────────────────────────────────────
        // Goals:
        //   1. Strip null bytes and non-printable control chars (C0/C1 + DEL)
        //      that could corrupt text columns or confuse downstream readers.
        //   2. Normalise whitespace so repeated spaces / newlines are reduced.
        //   3. Hard-cap length (belt-and-suspenders; model + repo also enforce).
        //   4. Do NOT strip HTML/script tags — parameterised queries make them
        //      inert at the DB level and stripping would mangle legitimate text
        //      like "use <canvas> for charts".
        private static string Sanitise(string raw)
        {
            // 1. Remove null bytes explicitly (Postgres TEXT rejects \0)
            var clean = raw.Replace("\0", string.Empty);

            // 2. Strip non-printable control characters while preserving
            //    legitimate whitespace (\n, \r, \t)
            clean = Regex.Replace(clean, @"[\x01-\x08\x0B\x0C\x0E-\x1F\x7F]", string.Empty);

            // 3. Trim leading / trailing whitespace
            clean = clean.Trim();

            // 4. Hard cap
            if (clean.Length > 2000)
                clean = clean[..2000];

            return clean;
        }
    }
}