using System.Collections.Concurrent;
using System.Text.Json;
using HoNfigurator.ManagementPortal.Models;

namespace HoNfigurator.ManagementPortal.Services;

/// <summary>
/// In-memory cache for real-time server status
/// </summary>
public class ServerStatusService
{
    private readonly ConcurrentDictionary<string, ServerStatusReport> _statusCache = new();
    private readonly ILogger<ServerStatusService> _logger;

    public event Action<string, ServerStatusReport>? OnStatusUpdated;

    public ServerStatusService(ILogger<ServerStatusService> logger)
    {
        _logger = logger;
    }

    public void UpdateStatus(string serverId, ServerStatusReport status)
    {
        _statusCache[serverId] = status;
        OnStatusUpdated?.Invoke(serverId, status);
        _logger.LogDebug("Updated status for server {ServerId}", serverId);
    }

    public ServerStatusReport? GetStatus(string serverId)
    {
        return _statusCache.TryGetValue(serverId, out var status) ? status : null;
    }

    public IReadOnlyDictionary<string, ServerStatusReport> GetAllStatuses()
    {
        return _statusCache;
    }

    public DashboardSummary GetDashboardSummary(IEnumerable<RegisteredServer> registeredServers)
    {
        var summary = new DashboardSummary();
        
        foreach (var server in registeredServers)
        {
            summary.TotalServers++;
            
            var status = GetStatus(server.ServerId);
            var serverSummary = new RegisteredServerSummary
            {
                ServerId = server.ServerId,
                ServerName = server.ServerName,
                Region = server.Region,
                IsOnline = server.IsOnline,
                LastSeenAt = server.LastSeenAt
            };
            
            if (status != null && server.IsOnline)
            {
                summary.OnlineServers++;
                summary.TotalGameServers += status.TotalServers;
                summary.ActiveGameServers += status.OnlineServers;
                summary.TotalPlayers += status.TotalPlayers;
                
                if (status.Instances != null)
                {
                    summary.ActiveMatches += status.Instances.Count(i => 
                        i.Status == "Occupied" || i.Status == "OCCUPIED");
                }
                
                serverSummary.TotalServers = status.TotalServers;
                serverSummary.OnlineServers = status.OnlineServers;
                serverSummary.TotalPlayers = status.TotalPlayers;
                serverSummary.CpuPercent = status.SystemStats?.CpuPercent ?? 0;
                serverSummary.MemoryPercent = status.SystemStats?.MemoryPercent ?? 0;
            }
            
            summary.Servers.Add(serverSummary);
        }
        
        return summary;
    }

    public void RemoveServer(string serverId)
    {
        _statusCache.TryRemove(serverId, out _);
    }
}

/// <summary>
/// Background service to mark servers as offline if they haven't reported in
/// </summary>
public class ServerCleanupService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ServerCleanupService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);
    private readonly TimeSpan _offlineThreshold = TimeSpan.FromMinutes(2);

    public ServerCleanupService(IServiceProvider services, ILogger<ServerCleanupService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(_checkInterval, stoppingToken);
            
            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<Data.PortalDbContext>();
                
                var threshold = DateTime.UtcNow - _offlineThreshold;
                var staleServers = await db.Servers
                    .Where(s => s.IsOnline && s.LastSeenAt < threshold)
                    .ToListAsync(stoppingToken);
                
                foreach (var server in staleServers)
                {
                    server.IsOnline = false;
                    _logger.LogInformation("Server {ServerId} marked as offline (no status report)", server.ServerId);
                }
                
                if (staleServers.Count > 0)
                {
                    await db.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in server cleanup service");
            }
        }
    }
}
