using Microsoft.AspNetCore.Mvc;
using DocVersion.Server.Services;

namespace DocVersion.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SharedController : ControllerBase
{
    private readonly FileService _fileService;

    public SharedController(FileService fileService)
    {
        _fileService = fileService;
    }

    [HttpGet("{token}")]
    public async Task<IActionResult> GetSharedFile(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return NotFound();

        var shareLink = await _fileService.GetShareLinkByTokenAsync(token);

        if (shareLink == null)
            return NotFound("Share link not found or expired.");

        try
        {
            var (fileStream, contentType) = await _fileService.GetFileContentAsync(shareLink.Username, shareLink.FilePath);
            if (fileStream == null)
                return NotFound("File not found.");

            return File(fileStream, contentType, Path.GetFileName(shareLink.FilePath), enableRangeProcessing: true);
        }
        catch
        {
            return NotFound();
        }
    }
}
