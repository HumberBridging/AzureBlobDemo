using AzureBlobDemo.Api.Contracts;

namespace AzureBlobDemo.Api.Services;

public interface IBlobStorageService
{
    /// <summary>Returns true if something was actually deleted.</summary>
    Task<bool> DeleteAsync(string blobName, CancellationToken cancellationToken = default);

    /// <summary>Server-side paging. Never materialise an unbounded container into a List.</summary>
    Task<BlobPage> ListAsync(
        string? prefix, string? continuationToken, int pageSize, CancellationToken cancellationToken = default);
}
