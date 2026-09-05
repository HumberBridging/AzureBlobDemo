using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using AzureBlobDemo.Api.Contracts;
using AzureBlobDemo.Api.Options;
using Microsoft.Extensions.Options;

namespace AzureBlobDemo.Api.Services;

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _serviceClient;
    private readonly BlobContainerClient _container;
    private readonly BlobStorageOptions _options;
    private readonly ILogger<BlobStorageService> _logger;

    public BlobStorageService(BlobServiceClient serviceClient, IOptions<BlobStorageOptions> options, ILogger<BlobStorageService> logger)
    {
        _serviceClient = serviceClient;
        _options = options.Value;
        _logger = logger;
        _container = serviceClient.GetBlobContainerClient(_options.ContainerName);
    }

    public async Task<bool> DeleteAsync(string blobName, CancellationToken cancellationToken = default)
    {
        var blob = _container.GetBlobClient(blobName);

        Response<bool> response = await blob.DeleteIfExistsAsync(
            DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);

        if (response.Value)
            _logger.LogInformation("Deleted {BlobName} from {Container}", blobName, _options.ContainerName);

        return response.Value;
    }

    //Note that this method is designed to return a single page of results, even if there are more blobs available. The caller can use the continuation token to request the next page of results.
    public async Task<BlobPage> ListAsync(
    string? prefix, string? continuationToken, int pageSize, CancellationToken cancellationToken = default)
    {
        var items = new List<BlobSummary>(pageSize);

        // AsPages() gives us ONE round trip per page and an opaque continuation token to hand back
        // to the caller. This is the difference between an endpoint that works on 10 blobs and one
        // that works on 10 million.
        await foreach (Page<BlobItem> page in _container
                           .GetBlobsAsync(BlobTraits.Metadata, BlobStates.None, prefix, cancellationToken)
                           .AsPages(continuationToken, pageSize)
                           .WithCancellation(cancellationToken))
        {
            foreach (var blob in page.Values)
            {
                items.Add(new BlobSummary(
                    blob.Name,
                    blob.Properties.ContentLength,
                    blob.Properties.ContentType,
                    blob.Properties.AccessTier?.ToString(),
                    blob.Properties.LastModified,
                    blob.Properties.ETag?.ToString()));
            }

            // We only ever want one page per request.
            return new BlobPage(items, page.ContinuationToken);
        }

        return new BlobPage(items, null);
    }
}
