namespace AzureBlobDemo.Api.Contracts;

/// <summary>What we tell the caller about a stored blob. Note: no SDK types leak across the API boundary.</summary>
public sealed record BlobSummary(
    string Name,
    long? SizeBytes,
    string? ContentType,
    string? AccessTier,
    DateTimeOffset? LastModified,
    string? ETag);

/// <summary>A page of results plus the opaque token needed to ask for the next page.</summary>
public sealed record BlobPage(
    IReadOnlyList<BlobSummary> Items,
    string? ContinuationToken);

/// <summary>Result of an upload. The ETag is the caller's concurrency handle for later updates.</summary>
public sealed record UploadResult(
    string Name,
    string ETag,
    DateTimeOffset LastModified,
    long SizeBytes);
