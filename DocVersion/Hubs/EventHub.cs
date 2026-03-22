using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;

namespace DocVersion.Hubs;

[Authorize]
public class EventHub : Hub
{

}