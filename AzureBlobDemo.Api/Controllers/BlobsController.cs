using AzureBlobDemo.Api.Contracts;
using AzureBlobDemo.Api.Options;
using AzureBlobDemo.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AzureBlobDemo.Api.Controllers;

[ApiController]
[Route("api/v1/blobs")]
[Produces("application/json")]
public class BlobsController : ControllerBase
{
    private readonly IBlobStorageService _blobs;
    private readonly BlobStorageOptions _options;

    public BlobsController(IBlobStorageService blobs, IOptions<BlobStorageOptions> options)
    {
        _blobs = blobs;
        _options = options.Value;
    }

    // The largest page we are willing to serve, regardless of what the caller asks for.
    private const int MaxPageSize = 100;

    [HttpGet]
    [ProducesResponseType(typeof(BlobPage), StatusCodes.Status200OK)]
    public async Task<ActionResult<BlobPage>> List(
        [FromQuery] string? prefix,
        [FromQuery] string? continuationToken,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        // Clamp so a caller can't ask for a million-item page (or a zero/negative one).
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var page = await _blobs.ListAsync(prefix, continuationToken, pageSize, cancellationToken);

        return Ok(page);
    }

    [HttpDelete("{blobName}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string blobName, CancellationToken cancellationToken)
    {
        await _blobs.DeleteAsync(blobName, cancellationToken);
        return NoContent();
    }

    /// <summary>Streams a blob's bytes back to the caller.</summary>
    /// The * in [HttpGet("{*blobName}")] defines a catch-all route parameter (also known as a wildcard or greedy parameter) in ASP.NET Core endpoint routing
    /// It tells the router engine to capture everything remaining in the URI path, including forward slashes, into that single parameter:
    [HttpGet("{*blobName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(string blobName, CancellationToken cancellationToken)
    {
        var result = await _blobs.OpenReadAsync(blobName, cancellationToken);

        if (result is null)
            return Problem(
                title: "Blob not found",
                detail: $"No blob named '{blobName}' exists in container '{_options.ContainerName}'.",
                statusCode: StatusCodes.Status404NotFound);

        var (content, contentType, etag) = result.Value;

        // enableRangeProcessing lets a browser or video player ask for byte ranges instead of the whole file.
        Response.Headers.ETag = etag;
        return File(content, contentType, enableRangeProcessing: true);
    }
}
