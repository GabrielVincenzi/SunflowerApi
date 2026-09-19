namespace SunflowerApi.Repositories
{
    public interface IDataRequestRepository
    {
        Task<Guid> InsertDataRequestAsync(
            string userId,
            string message,
            CancellationToken ct = default);
    }
}