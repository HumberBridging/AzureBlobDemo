namespace AzureBlobDemo.Api.Contracts;

public sealed record BlobSummary(
    string Name,
    long? SizeBytes,
    string? ContentType,
    string? AccessTier,
    DateTimeOffset? LastModified,
    string? ETag);

public sealed record BlobPage(
    IReadOnlyList<BlobSummary> Items,
    string? ContinuationToken);
