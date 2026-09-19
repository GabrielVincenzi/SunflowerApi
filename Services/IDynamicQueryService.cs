using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SunflowerApi.Services
{
    public interface IDynamicQueryService
    {
        /// <summary>
        /// Query a dynamic table in the public schema for rows matching filters.
        /// TableName must be validated by the service (prevents injection).
        /// Returns list of rows as Dictionary{columnName, object}.
        /// </summary>
        Task<List<Dictionary<string, object?>>> QueryTableAsync(
            string tableName,
            string[]? geos = null,
            DateTime? startPeriod = null,
            DateTime? endPeriod = null,
            CancellationToken cancellationToken = default);
    }
}
