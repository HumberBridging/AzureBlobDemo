namespace AzureBlobDemo.Api.Options;

public sealed class BlobStorageOptions
{
    /// <summary>
    /// The data-plane endpoint of the storage account, e.g. https://sdbp031demo.blob.core.windows.net
    /// Used together with Managed Identity / DefaultAzureCredential. This is the PRODUCTION path.
    /// </summary>
    public Uri? ServiceUri { get; set; }

    public const string SectionName = "BlobStorage";

    public string ContainerName { get; set; } = string.Empty;

    /// <summary>
    /// Local-development only. Set to "UseDevelopmentStorage=true" to talk to Azurite.
    /// Never populate this in appsettings.json for a real account - that is how account keys leak.
    /// If both are set, ServiceUri + Managed Identity wins.
    /// </summary>
    public string? ConnectionString { get; set; }

    public long MaxUploadBytes { get; set; } = 50L * 1024L * 1024L; // 50 MB

    public bool UsesEmulator =>
        ServiceUri is null && !string.IsNullOrWhiteSpace(ConnectionString);
}
