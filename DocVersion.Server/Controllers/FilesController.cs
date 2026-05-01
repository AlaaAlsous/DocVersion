using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using DocVersion.Server.Services;
using DocVersion.Server.Hubs;
using DocVersion.Core.Models;

namespace DocVersion.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly FileService _fileService;
    private readonly IHubContext<EventsHub> _eventsHub;
    public FilesController(FileService fileService, IHubContext<EventsHub> eventsHub)
    {
        _fileService = fileService;
        _eventsHub = eventsHub;
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
        catch (IOException)
        {
            return Conflict("File is in use by another process.");
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

    [HttpGet("history/{**filename}")]
    public async Task<IActionResult> GetFileHistoryAsync(string filename, [FromQuery] int? version = null)
    {
        var username = GetUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(filename)) return NotFound();

        try
        {
            if (version.HasValue)
            {
                if (version.Value <= 0) return BadRequest("Invalid version.");

                var (fileStream, contentType) = await _fileService.GetFileHistoryVersionContentAsync(username, filename, version.Value, HttpContext.RequestAborted);
                if (fileStream == null) return NotFound();

                return File(fileStream, contentType, Path.GetFileName(filename), enableRangeProcessing: true);
            }

            var history = await _fileService.GetFileHistoryAsync(username, filename, HttpContext.RequestAborted);
            if (history == null || history.Count == 0) return NotFound();
            var result = history
            .OrderByDescending(h => h.Version)
            .Select(h => new
            {
                h.Version,
                h.CreatedAt
            }).ToList();

            return Ok(result);
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
        if (string.IsNullOrWhiteSpace(filename)) return NotFound();

        try
        {
            var xTypePost = Request.Headers["X-Type"].FirstOrDefault();
            bool isFolderPost = xTypePost == "folder" || (xTypePost != "file" && Request.ContentLength.GetValueOrDefault() == 0);
            if (isFolderPost)
            {
                var folderCreated = await _fileService.CreateFolderAsync(username, filename);
                if (!folderCreated) return Conflict("Folder already exists.");
                await _eventsHub.Clients.All.SendAsync("Event", (int)EventsType.FolderCreated, filename);
                return CreatedAtAction(nameof(GetFileContent), new { filename }, null);
            }

            var created = await _fileService.CreateFileAsync(username, filename, Request.Body, HttpContext.RequestAborted);
            if (!created) return Conflict("File already exists.");
            await _eventsHub.Clients.All.SendAsync("Event", (int)EventsType.FileCreated, filename);
            return CreatedAtAction(nameof(GetFileContent), new { filename }, null);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
        catch (IOException)
        {
            return Conflict("File is in use by another process.");
        }
    }

    [HttpPost("restore/{**filename}")]
    public async Task<IActionResult> RestoreFileVersionAsync(string filename, [FromQuery] int version)
    {
        var username = GetUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(filename)) return NotFound();
        if (version <= 0) return BadRequest("Invalid version.");
        try
        {
            await _fileService.RestoreFileHistoryAsync(username, filename, version, HttpContext.RequestAborted);

            await _eventsHub.Clients.All.SendAsync("Event", (int)EventsType.FileUpdated, filename);
            return Ok(new { Message = "File restored to version ", version, filename });
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    [HttpPut("{**filename}")]
    public async Task<IActionResult> UpdateFileAsync(string filename)
    {
        var username = GetUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(filename)) return NotFound();

        try
        {
            var xTypePut = Request.Headers["X-Type"].FirstOrDefault();
            bool isFolderPut = xTypePut == "folder" || (xTypePut != "file" && Request.ContentLength.GetValueOrDefault() == 0);
            if (isFolderPut)
            {
                var created = await _fileService.CreateFolderAsync(username, filename);
                if (created)
                    await _eventsHub.Clients.All.SendAsync("Event", (int)EventsType.FolderCreated, filename);
                return NoContent();
            }
            bool fileExists = await _fileService.FileExistsAsync(username, filename);
            await _fileService.SaveFileAsync(username, filename, Request.Body, HttpContext.RequestAborted);
            await _eventsHub.Clients.All.SendAsync("Event", fileExists ? (int)EventsType.FileUpdated : (int)EventsType.FileCreated, filename);
            return NoContent();
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
        catch (IOException)
        {
            return Conflict("File is in use by another process.");
        }
    }

    [HttpPost("upload-folder")]
    [RequestSizeLimit(1_073_741_824)]
    public async Task<IActionResult> UploadFolder()
    {
        var username = GetUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();
        if (!Request.HasFormContentType || !Request.Form.Files.Any())
            return BadRequest("No files uploaded.");

        var uploadFiles = Request.Form.Files
            .Select(f => (FileName: f.FileName.Replace("\\", "/").TrimStart('/'), Content: f.OpenReadStream()))
            .ToList();

        var allFolders = uploadFiles
            .Select(f => Path.GetDirectoryName(f.FileName))
            .Where(d => !string.IsNullOrEmpty(d))
            .SelectMany(d =>
                d!.Split('/')
                .Select((_, idx) => string.Join('/', d.Split('/').Take(idx + 1))))
            .Distinct()
            .OrderBy(x => x.Length)
            .ToList();

        foreach (var folder in allFolders)
        {
            if (!string.IsNullOrWhiteSpace(folder))
            {
                await _fileService.CreateFolderAsync(username, folder);
                await _eventsHub.Clients.All.SendAsync("Event", (int)EventsType.FolderCreated, folder);
            }
        }

        var results = await _fileService.UploadFilesAsync(username, uploadFiles, HttpContext.RequestAborted);

        foreach (var result in results)
        {
            if (result.Success)
                await _eventsHub.Clients.All.SendAsync("Event", (int)EventsType.FileCreated, result.File);
        }

        var failed = results.Where(r => !r.Success).ToList();
        if (failed.Count > 0)
            return StatusCode(207, new { Message = "Some files failed", Results = results });
        return Ok(new { Message = "Folder uploaded", Results = results });
    }

    [HttpDelete("{**filename}")]
    public async Task<IActionResult> DeleteFileAsync(string filename)
    {
        var username = GetUsername();
        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(filename))
            return BadRequest("Filename is required");

        try
        {
            var isFile = await _fileService.FileExistsAsync(username, filename);
            var isFolder = await _fileService.FolderExistsAsync(username, filename);

            if (!isFile && !isFolder)
                return NoContent();

            if (isFile)
            {
                var deleted = await _fileService.DeleteFileAsync(username, filename);
                if (!deleted)
                {
                    return StatusCode(500, new { Message = "Failed to delete file on server." });
                }

                _ = _eventsHub.Clients.All.SendAsync("Event", (int)EventsType.FileDeleted, filename);
            }
            else if (isFolder)
            {
                var deleted = await _fileService.DeleteFolderAsync(username, filename);
                if (!deleted)
                {
                    return StatusCode(500, new { Message = "Failed to delete folder on server." });
                }

                _ = _eventsHub.Clients.All.SendAsync("Event", (int)EventsType.FolderDeleted, filename);
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Error deleting file/folder", Details = ex.Message });
        }
    }

    [HttpPost("rename")]
    public async Task<IActionResult> Rename([FromBody] RenameRequest request)
    {
        if (request == null)
            return BadRequest("Request body saknas.");

        var username = GetUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        var oldName = request.OldName?.Trim();
        var newName = request.NewName?.Trim();

        if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName))
            return BadRequest("OldName and NewName are required.");

        try
        {
            bool success;

            var payload = string.Join("|", oldName, newName);

            if (request.IsFolder)
            {
                success = await _fileService.RenameFolderAsync(username, oldName, newName);

                if (success)
                    await _eventsHub.Clients.All.SendAsync(
                        "Event",
                        (int)EventsType.FolderRenamed,
                        payload);
            }
            else
            {
                success = await _fileService.RenameFileAsync(username, oldName, newName);

                if (success)
                    await _eventsHub.Clients.All.SendAsync(
                        "Event",
                        (int)EventsType.FileRenamed,
                        payload);
            }

            if (!success)
                return Conflict("Rename failed.");

            return Ok(new { Message = "Rename successful", OldName = oldName, NewName = newName });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpGet("zip/{**foldername}")]
    public async Task<IActionResult> DownloadFolderAsZip(string foldername)
    {
        var username = GetUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(foldername)) return NotFound();

        var zipStream = await _fileService.GetFolderAsZipAsync(username, foldername);
        if (zipStream == null) return NotFound();

        var zipFileName = Path.GetFileName(foldername.TrimEnd('/', '\\'));
        if (string.IsNullOrWhiteSpace(zipFileName)) zipFileName = "folder";
        zipFileName += ".zip";

        return File(zipStream, "application/zip", zipFileName);
    }

    public record RenameRequest(string OldName, string NewName, bool IsFolder);
}