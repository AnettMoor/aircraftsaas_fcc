using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Security.Claims;
using Fleet.Api.DTOs;
using Fleet.Api.Mappers;
using Fleet.Application.DTOs;
using Fleet.Application.Interfaces;
using Shared.Contracts.Common;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Fleet.Api.Controllers;

/// <summary>Request model for uploading an aircraft photo (multipart/form-data).</summary>
public class AddAircraftPhotoRequest
{
    [Required]
    public IFormFile File { get; set; } = default!;

    [StringLength(200)]
    public string? Description { get; set; }

    public bool IsPrimary { get; set; }

    [Range(0, int.MaxValue)]
    public int DisplayOrder { get; set; }
}

/// <summary>
/// Manages photos for a specific aircraft. CompanyOwners can upload PNG/JPG photos,
/// set a primary photo, and delete photos. Photos are publicly readable.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/aircraft/{aircraftId:guid}/photos")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class AircraftPhotosController : ControllerBase
{
    private static readonly string[] AllowedContentTypes = { "image/png", "image/jpeg" };
    private static readonly string[] AllowedExtensions = { ".png", ".jpg", ".jpeg" };

    private readonly IAircraftService _aircraftService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<AircraftPhotosController> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;

    public AircraftPhotosController(
        IAircraftService aircraftService,
        ITenantContext tenantContext,
        ILogger<AircraftPhotosController> logger,
        IWebHostEnvironment env,
        IConfiguration configuration)
    {
        _aircraftService = aircraftService;
        _tenantContext = tenantContext;
        _logger = logger;
        _env = env;
        _configuration = configuration;
    }

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return value != null && Guid.TryParse(value, out var id) ? id : null;
    }

    /// <summary>
    /// Serve a photo file by its relative path (public).
    /// </summary>
    [HttpGet("file")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(FileStreamResult), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    public IActionResult GetPhotoFile(Guid aircraftId, [FromQuery] string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return BadRequest(new RestApiErrorResponse { Status = HttpStatusCode.BadRequest, Error = "Missing 'path' query parameter." });

        var expectedPrefix = $"/uploads/aircraft/{aircraftId}/";
        if (!path.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new RestApiErrorResponse { Status = HttpStatusCode.BadRequest, Error = "Invalid photo path." });

        var wwwRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var relativePath = path.TrimStart('/');
        var filePath = Path.Combine(wwwRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

        var fullPath = Path.GetFullPath(filePath);
        var fullWwwRoot = Path.GetFullPath(wwwRoot);
        if (!fullPath.StartsWith(fullWwwRoot, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new RestApiErrorResponse { Status = HttpStatusCode.BadRequest, Error = "Invalid path." });

        if (!System.IO.File.Exists(fullPath))
            return NotFound();

        var ext = Path.GetExtension(fullPath).ToLowerInvariant();
        var contentType = ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/octet-stream",
        };

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return File(stream, contentType);
    }

    /// <summary>
    /// Get all photos for an aircraft (public).
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<AircraftPhotoResponse>), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(RestApiErrorResponse), (int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<IEnumerable<AircraftPhotoResponse>>> GetPhotos(Guid aircraftId)
    {
        try
        {
            var photos = await _aircraftService.GetPhotosAsync(aircraftId);
            return Ok(photos.ToResponse());
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new RestApiErrorResponse
            {
                Status = HttpStatusCode.NotFound,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Upload a PNG or JPG photo for an aircraft (CompanyOwner only).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "CompanyOwner")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(AircraftPhotoResponse), (int)HttpStatusCode.Created)]
    [ProducesResponseType(typeof(RestApiErrorResponse), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<ActionResult<AircraftPhotoResponse>> UploadPhoto(
        Guid aircraftId,
        [FromForm] AddAircraftPhotoRequest request)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var tenantId = await _tenantContext.ResolveOrAutoSetTenantAsync(userId.Value);
        if (!tenantId.HasValue)
            return BadRequest(new RestApiErrorResponse
            {
                Status = HttpStatusCode.BadRequest,
                Error = "No company context found."
            });

        if (!await _tenantContext.IsUserCompanyOwnerAsync(tenantId.Value, userId.Value))
            return Forbid();

        if (request.File == null || request.File.Length == 0)
            return BadRequest(new RestApiErrorResponse
            {
                Status = HttpStatusCode.BadRequest,
                Error = "No file provided."
            });

        if (!AllowedContentTypes.Contains(request.File.ContentType, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new RestApiErrorResponse
            {
                Status = HttpStatusCode.BadRequest,
                Error = "Only PNG and JPG/JPEG images are allowed."
            });

        var ext = Path.GetExtension(request.File.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return BadRequest(new RestApiErrorResponse
            {
                Status = HttpStatusCode.BadRequest,
                Error = "Only .png, .jpg, and .jpeg file extensions are allowed."
            });

        var maxFileSize = _configuration.GetValue<long>("FileStorage:MaxFileSizeBytes", 5_242_880);
        if (request.File.Length > maxFileSize)
            return BadRequest(new RestApiErrorResponse
            {
                Status = HttpStatusCode.BadRequest,
                Error = $"File size exceeds the maximum allowed size of {maxFileSize / 1_048_576} MB."
            });

        var photosRelativePath = _configuration.GetValue<string>("FileStorage:PhotosRelativePath", "uploads/aircraft")!;
        var wwwRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var aircraftFolder = Path.Combine(wwwRoot, photosRelativePath, aircraftId.ToString());
        Directory.CreateDirectory(aircraftFolder);

        var fileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(aircraftFolder, fileName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await request.File.CopyToAsync(stream);
        }

        var imageUrl = $"/{photosRelativePath}/{aircraftId}/{fileName}";

        try
        {
            var addedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
            var dto = new AddAircraftPhotoDto
            {
                ImageUrl = imageUrl,
                Description = request.Description,
                IsPrimary = request.IsPrimary,
                DisplayOrder = request.DisplayOrder,
            };

            var photo = await _aircraftService.AddPhotoAsync(aircraftId, tenantId.Value, dto, addedBy);

            return CreatedAtAction(
                nameof(GetPhotos),
                new { aircraftId },
                photo.ToResponse());
        }
        catch (InvalidOperationException ex)
        {
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);

            return BadRequest(new RestApiErrorResponse
            {
                Status = HttpStatusCode.BadRequest,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Set a specific photo as the primary photo for the aircraft (CompanyOwner only).
    /// </summary>
    [HttpPut("{photoId:guid}/set-primary")]
    [Authorize(Roles = "CompanyOwner")]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [ProducesResponseType(typeof(RestApiErrorResponse), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<IActionResult> SetPrimaryPhoto(Guid aircraftId, Guid photoId)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var tenantId = await _tenantContext.ResolveOrAutoSetTenantAsync(userId.Value);
        if (!tenantId.HasValue)
            return BadRequest(new RestApiErrorResponse
            {
                Status = HttpStatusCode.BadRequest,
                Error = "No company context found."
            });

        if (!await _tenantContext.IsUserCompanyOwnerAsync(tenantId.Value, userId.Value))
            return Forbid();

        try
        {
            await _aircraftService.SetPrimaryPhotoAsync(photoId, aircraftId, tenantId.Value);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new RestApiErrorResponse
            {
                Status = HttpStatusCode.BadRequest,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Soft-delete a photo (CompanyOwner only). The physical file is not removed.
    /// </summary>
    [HttpDelete("{photoId:guid}")]
    [Authorize(Roles = "CompanyOwner")]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [ProducesResponseType(typeof(RestApiErrorResponse), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<IActionResult> DeletePhoto(Guid aircraftId, Guid photoId)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var tenantId = await _tenantContext.ResolveOrAutoSetTenantAsync(userId.Value);
        if (!tenantId.HasValue)
            return BadRequest(new RestApiErrorResponse
            {
                Status = HttpStatusCode.BadRequest,
                Error = "No company context found."
            });

        if (!await _tenantContext.IsUserCompanyOwnerAsync(tenantId.Value, userId.Value))
            return Forbid();

        try
        {
            var deletedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
            await _aircraftService.DeletePhotoAsync(photoId, aircraftId, tenantId.Value, deletedBy);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new RestApiErrorResponse
            {
                Status = HttpStatusCode.BadRequest,
                Error = ex.Message
            });
        }
    }
}
