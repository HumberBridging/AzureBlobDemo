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

    public async Task<(Stream Content, string ContentType, string ETag)?> OpenReadAsync(string blobName, CancellationToken cancellationToken = default)
    {
        var blob = _container.GetBlobClient(blobName);

        try
        {
            Response<BlobDownloadStreamingResult> response = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);

            var details = response.Value.Details;

            return (response.Value.Content, details.ContentType, details.ETag.ToString());
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogInformation("Blob {BlobName} not found in {Container}", blobName, _options.ContainerName);
            return null;
        }
    }

    public async Task<UploadResult> UploadAsync(string blobName, Stream content, string contentType, bool overwrite, IDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default)
    {
        var blob = _container.GetBlobClient(blobName);

        var uploadOptions = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
            Metadata = metadata,

            // Parallel, chunked upload. The SDK splits anything over InitialTransferSize into blocks.
            TransferOptions = new Azure.Storage.StorageTransferOptions
            {
                InitialTransferSize = 4 * 1024 * 1024,
                MaximumTransferSize = 4 * 1024 * 1024,
                MaximumConcurrency = 4
            },

            // Optimistic concurrency: IfNoneMatch = ETag.All means "only if this blob does not exist yet".
            // Without it, two consumers uploading invoice.pdf silently overwrite each other.
            Conditions = overwrite ? null : new BlobRequestConditions { IfNoneMatch = ETag.All }
        };

        //Very important to use a try/catch here to handle the RequestFailedException that can be thrown if the blob already exists and overwrite is false.
        try
        {
            Response<BlobContentInfo> response = await blob.UploadAsync(content, uploadOptions, cancellationToken);

            _logger.LogInformation("Uploaded {BlobName} ({ContentType}) to {Container}", blobName, contentType, _options.ContainerName);

            return new UploadResult(
                blobName,
                response.Value.ETag.ToString(),
                response.Value.LastModified,
                content.CanSeek ? content.Length : 0);
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            // 409 BlobAlreadyExists - surfaced by the IfNoneMatch condition above.
            throw new InvalidOperationException($"A blob named '{blobName}' already exists. Pass overwrite=true to replace it.", ex);
        }
    }
}
