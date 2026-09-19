using SunflowerApi.Models;

namespace SunflowerApi.Services
{
    public interface IDataRequestService
    {
        Task<Guid> SubmitRequestAsync(DataRequest request, CancellationToken ct);
    }
}