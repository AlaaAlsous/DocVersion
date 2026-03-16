using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DocVersion.Services;
using System.Security.Claims;

namespace DocVersion.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly FileService _fileService;
    public FilesController(FileService fileService)
    {
        _fileService = fileService;
    }

    [HttpGet]
    public async Task<IActionResult> GetFiles()
    {
        var username = User.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        var files = await _fileService.GetAllFilesAsync(username);
        return Ok(files);
    }

    [HttpGet("{**filename}")]
    public async Task<IActionResult> GetFileContent(string filename)
    {
        var username = User.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrEmpty(username)) return Unauthorized();
        var (fileStram, contentType) = await _fileService.GetFileContentAsync(username, filename);
        if (fileStram == null) return NotFound();
        return File(fileStram, contentType, Path.GetFileName(filename), enableRangeProcessing: true);
    }

    [HttpPost("{**filename}")]
    public async Task<IActionResult> CreateFileAsync(string filename)
    {
        var username = User.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrEmpty(username)) return Unauthorized();
        var created = await _fileService.CreateFileAsync(username, filename, Request.Body);
        if (!created) return Conflict("File already exists.");
        return CreatedAtAction(nameof(GetFiles), new { filename }, null);
    }

}