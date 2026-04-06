using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;

namespace DocVersion.Server.Hubs;

[Authorize]
public class EventsHub : Hub { }