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
}
