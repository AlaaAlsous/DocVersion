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

    private string? GetUsername()
    {
        return User.FindFirstValue(ClaimTypes.Name);
    }

    private async Task<IActionResult> ReturnMetadataHeadersAsync(string username, string filename)
    {
        var metadata = await _fileService.GetFileMetadataAsync(username, filename);
        if (metadata == null) return NotFound();

        Response.Headers["X-Created-At"] = metadata.Created;
        Response.Headers["X-Changed-At"] = metadata.Changed;
        Response.Headers["X-Type"] = metadata.IsFile ? "file" : "folder";
        Response.Headers["X-Bytes"] = metadata.Bytes.ToString();
        Response.Headers["X-Extension"] = metadata.Extension ?? "";

        return Ok();
    }

    [HttpGet]
    public async Task<IActionResult> GetFiles()
    {
        var username = GetUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        var files = await _fileService.GetAllFilesAsync(username);
        return Ok(files);
    }

    [HttpGet("{**filename}")]
    public async Task<IActionResult> GetFileContent(string filename)
    {
        var username = GetUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(filename)) return NotFound();

        try
        {
            var folderContent = await _fileService.GetFolderContentAsync(username, filename);
            if (folderContent != null)
                return Ok(folderContent);

            var (fileStream, contentType) = await _fileService.GetFileContentAsync(username, filename);
            if (fileStream == null) return NotFound();

            await ReturnMetadataHeadersAsync(username, filename);

            return File(fileStream, contentType, Path.GetFileName(filename), enableRangeProcessing: true);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    [HttpHead("{**filename}")]
    public async Task<IActionResult> HeadFileContent(string filename)
    {
        var username = GetUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(filename)) return NotFound();

        try
        {
            return await ReturnMetadataHeadersAsync(username, filename);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    [HttpPost("{**filename}")]
    public async Task<IActionResult> CreateFileAsync(string filename)
    {
        var username = GetUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();
        var created = await _fileService.CreateFileAsync(username, filename, Request.Body);
        if (!created) return Conflict("File already exists.");
        return CreatedAtAction(nameof(GetFileContent), new { filename }, null);
    }

    [HttpPut("{**filename}")]
    public async Task<IActionResult> UpdateFileAsync(string filename)
    {
        var username = GetUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();
        await _fileService.SaveFileAsync(username, filename, Request.Body);
        return NoContent();
    }

    [HttpDelete("{**filename}")]
    public async Task<IActionResult> DeleteFileAsync(string filename)
    {
        var username = GetUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();
        await _fileService.DeleteFileAsync(username, filename);
        return NoContent();
    }
}