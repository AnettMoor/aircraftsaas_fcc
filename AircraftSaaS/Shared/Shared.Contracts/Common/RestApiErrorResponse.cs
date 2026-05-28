using System.Net;

namespace Shared.Contracts.Common;

public class RestApiErrorResponse
{
    public HttpStatusCode Status { get; set; }
    public string Error { get; set; } = default!;
}
