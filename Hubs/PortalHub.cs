using Microsoft.AspNetCore.SignalR;
using HoNfigurator.ManagementPortal.Models;
using HoNfigurator.ManagementPortal.Services;

namespace HoNfigurator.ManagementPortal.Hubs;

/// <summary>
/// SignalR hub for real-time portal dashboard updates
/// </summary>
public class PortalHub : Hub
{
    private readonly ServerStatusService _statusService;
    private readonly ILogger<PortalHub> _logger;

    public PortalHub(ServerStatusService statusService, ILogger<PortalHub> logger)
    {
        _statusService = statusService;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogDebug("Dashboard client connected: {ConnectionId}", Context.ConnectionId);
        
        // Send current status to newly connected client
        var allStatuses = _statusService.GetAllStatuses();
        foreach (var kvp in allStatuses)
        {
            await Clients.Caller.SendAsync("ServerStatusUpdated", kvp.Key, kvp.Value);
        }
        
        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogDebug("Dashboard client disconnected: {ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Client can request a specific server's status
    /// </summary>
    public async Task RequestServerStatus(string serverId)
    {
        var status = _statusService.GetStatus(serverId);
        if (status != null)
        {
            await Clients.Caller.SendAsync("ServerStatusUpdated", serverId, status);
        }
    }

    /// <summary>
    /// Client can request all server statuses
    /// </summary>
    public async Task RequestAllStatuses()
    {
        var allStatuses = _statusService.GetAllStatuses();
        foreach (var kvp in allStatuses)
        {
            await Clients.Caller.SendAsync("ServerStatusUpdated", kvp.Key, kvp.Value);
        }
    }
}
