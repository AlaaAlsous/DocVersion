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
        if (string.IsNullOrEmpty(username))
        {
            return Unauthorized();
        }
        var files = await _fileService.GetAllFilesAsync(username);
        return Ok(files);
    }
}