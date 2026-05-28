using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Shared.Messaging.Contracts;
using WebApp.Publishers;

namespace WebApp.Middleware;

/// <summary>
/// Middleware to automatically log all API requests for audit trail.
/// Publishes audit log entries to RabbitMQ for the Users microservice to store.
/// </summary>
public class AuditLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditLoggingMiddleware> _logger;

    // HTTP methods that represent write operations
    private static readonly HashSet<string> WriteMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "POST", "PUT", "PATCH", "DELETE"
    };

    // Paths to exclude from audit logging
    private static readonly HashSet<string> ExcludedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/health",
        "/swagger",
        "/api/auth/login",
        "/api/auth/register"
    };

    public AuditLoggingMiddleware(
        RequestDelegate next,
        ILogger<AuditLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        MonolithEventPublisher eventPublisher)
    {
        // Skip if path is excluded
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
        if (ShouldSkipAudit(path))
        {
            await _next(context);
            return;
        }

        // Store the start time for duration calculation
        var startTime = DateTime.UtcNow;
        
        // Capture request body for POST/PUT/PATCH
        var requestBody = string.Empty;
        if (WriteMethods.Contains(context.Request.Method) && context.Request.ContentLength > 0)
        {
            context.Request.EnableBuffering();
            using var reader = new StreamReader(
                context.Request.Body, 
                encoding: Encoding.UTF8, 
                detectEncodingFromByteOrderMarks: false,
                leaveOpen: true);
            requestBody = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
        }

        // Capture the original response body
        var originalBody = context.Response.Body;
        using var memStream = new MemoryStream();
        context.Response.Body = memStream;

        try
        {
            await _next(context);
        }
        finally
        {
            // Get response body
            memStream.Position = 0;
            var responseBody = await new StreamReader(memStream).ReadToEndAsync();
            memStream.Position = 0;
            await memStream.CopyToAsync(originalBody);

            // Log if it's a write operation and successful
            if (WriteMethods.Contains(context.Request.Method) && context.Response.StatusCode >= 200 && context.Response.StatusCode < 300)
            {
                await PublishAuditAsync(
                    context,
                    eventPublisher,
                    requestBody,
                    responseBody,
                    startTime);
            }
        }
    }

    private bool ShouldSkipAudit(string path)
    {
        foreach (var excluded in ExcludedPaths)
        {
            if (path.StartsWith(excluded, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private async Task PublishAuditAsync(
        HttpContext context,
        MonolithEventPublisher eventPublisher,
        string requestBody,
        string responseBody,
        DateTime startTime)
    {
        try
        {
            // Extract user ID if authenticated
            Guid? userId = null;
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var parsed))
                {
                    userId = parsed;
                }
            }

            // Extract tenant ID from header or context
            Guid? tenantId = null;
            var tenantIdHeader = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
            if (!string.IsNullOrEmpty(tenantIdHeader) && Guid.TryParse(tenantIdHeader, out var parsedTenant))
            {
                tenantId = parsedTenant;
            }

            // Determine entity name and action from path
            var (entityName, entityId, action) = ExtractEntityInfo(context.Request.Path.Value ?? "", requestBody, responseBody);

            // Determine old/new values based on action
            string? oldValues = null;
            string? newValues = null;
            
            if (action == "Updated")
            {
                oldValues = FormatJsonValues(requestBody);
                newValues = FormatJsonValues(responseBody);
            }
            else if (action == "Created")
            {
                newValues = FormatJsonValues(responseBody);
            }
            else if (action == "Deleted")
            {
                oldValues = FormatJsonValues(requestBody);
            }
            else
            {
                oldValues = GetOldValues(requestBody, action);
                newValues = GetNewValues(responseBody, action);
            }

            // Calculate duration
            var duration = DateTime.UtcNow - startTime;

            var auditMessage = new AuditLogRequestMessage(
                TenantId: tenantId ?? Guid.Empty,
                UserId: userId,
                EntityName: entityName,
                EntityId: entityId,
                Action: action,
                OldValues: oldValues,
                NewValues: newValues,
                IpAddress: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                Details: $"Method: {context.Request.Method}, Path: {context.Request.Path}, Duration: {duration.TotalMilliseconds}ms",
                Timestamp: DateTime.UtcNow);

            // Publish to RabbitMQ — Users microservice will consume and store
            await eventPublisher.PublishAuditLogRequestAsync(auditMessage);

            _logger.LogDebug(
                "Audit log published to RabbitMQ: {Action} on {EntityName} {EntityId} by User {UserId}",
                action, entityName, entityId, userId);
        }
        catch (Exception ex)
        {
            // Don't fail the request if audit logging fails
            _logger.LogError(ex, "Failed to publish audit log for {Path}", context.Request.Path);
        }
    }
    
    private static string? FormatJsonValues(string? jsonString)
    {
        if (string.IsNullOrEmpty(jsonString))
            return null;
        
        try
        {
            using var doc = JsonDocument.Parse(jsonString);
            // Remove 'id' from root level for cleaner output
            if (doc.RootElement.TryGetProperty("id", out _))
            {
                var dict = new Dictionary<string, JsonElement>();
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (prop.Name.Equals("id", StringComparison.OrdinalIgnoreCase))
                        continue;
                    dict[prop.Name] = prop.Value;
                }
                return JsonSerializer.Serialize(dict);
            }
            return JsonSerializer.Serialize(doc.RootElement);
        }
        catch
        {
            return jsonString.Length > 4000 ? jsonString[..4000] : jsonString;
        }
    }

    private static (string entityName, Guid entityId, string action) ExtractEntityInfo(
        string path, 
        string requestBody, 
        string responseBody)
    {
        var action = "Unknown"; // Default action
        
        // Try to determine action from response or request
        if (!string.IsNullOrEmpty(responseBody))
        {
            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                // If we got here with a response, it's likely a create
                action = "Created";
            }
            catch
            {
                action = "Updated";
            }
        }

        // Try to extract entity name from path (e.g., /api/aircraft/123 -> Aircraft)
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        
        if (segments.Length >= 3 && segments[1].Equals("api", StringComparison.OrdinalIgnoreCase))
        {
            var entityName = segments[2].TrimEnd('s'); // Remove plural (aircraft -> aircraft)
            
            // Try to get ID from path or response
            if (segments.Length >= 4 && Guid.TryParse(segments[3], out var id))
            {
                return (entityName, id, action);
            }

            // Try to get ID from response body
            if (!string.IsNullOrEmpty(responseBody))
            {
                try
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    if (doc.RootElement.TryGetProperty("id", out var idElement) && 
                        idElement.TryGetGuid(out var responseId))
                    {
                        return (entityName, responseId, action);
                    }
                }
                catch { }
            }

            return (entityName, Guid.Empty, action);
        }

        return ("Unknown", Guid.Empty, action);
    }

    private static string? GetOldValues(string requestBody, string action)
    {
        if (action != "Updated" || string.IsNullOrEmpty(requestBody))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(requestBody);
            return JsonSerializer.Serialize(doc.RootElement);
        }
        catch
        {
            return requestBody.Length > 1000 ? requestBody[..1000] : requestBody;
        }
    }

    private static string? GetNewValues(string responseBody, string action)
    {
        if (string.IsNullOrEmpty(responseBody))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            return JsonSerializer.Serialize(doc.RootElement);
        }
        catch
        {
            return responseBody.Length > 1000 ? responseBody[..1000] : responseBody;
        }
    }
}

/// <summary>
/// Extension methods for adding AuditLoggingMiddleware to the pipeline.
/// </summary>
public static class AuditLoggingMiddlewareExtensions
{
    /// <summary>
    /// Adds the AuditLoggingMiddleware to the application pipeline.
    /// This middleware automatically logs all write operations (POST, PUT, PATCH, DELETE)
    /// by publishing audit log messages to RabbitMQ.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseAuditLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<AuditLoggingMiddleware>();
    }
}
