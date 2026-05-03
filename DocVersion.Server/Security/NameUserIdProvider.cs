using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace DocVersion.Server.Security;

public class NameUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        return connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? connection.User?.FindFirst(ClaimTypes.Name)?.Value;
    }
}
