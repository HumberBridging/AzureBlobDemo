using AzureBlobDemo.Api.Contracts;

namespace AzureBlobDemo.Api.Services;

public interface IBlobStorageService
{
    /// <summary>Returns true if something was actually deleted.</summary>
    Task<bool> DeleteAsync(string blobName, CancellationToken cancellationToken = default);

    /// <summary>Server-side paging. Never materialise an unbounded container into a List.</summary>
    Task<BlobPage> ListAsync(
        string? prefix, string? continuationToken, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Streams a blob straight back to the caller. Returns null when the blob does not exist.</summary>
    Task<(Stream Content, string ContentType, string ETag)?> OpenReadAsync(
        string blobName, CancellationToken cancellationToken = default);

    /// <summary>Streams an uploaded file to storage without buffering the whole thing in memory.</summary>
    Task<UploadResult> UploadAsync(
        string blobName, Stream content, string contentType, bool overwrite,
        IDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default);
}
