using System.Net.Http.Headers;
using System.Text.Json;
using HoNfigurator.ManagementPortal.Data;
using HoNfigurator.ManagementPortal.Models;
using Microsoft.EntityFrameworkCore;

namespace HoNfigurator.ManagementPortal.Endpoints;

public static class AuthEndpoints
{
    private static readonly HttpClient _httpClient = new();
    
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/auth")
            .WithTags("Authentication")
            .WithOpenApi();

        group.MapGet("/discord", RedirectToDiscord)
            .WithName("DiscordLogin")
            .WithDescription("Redirect to Discord OAuth2 login");

        group.MapGet("/discord/callback", DiscordCallback)
            .WithName("DiscordCallback")
            .WithDescription("Handle Discord OAuth2 callback");

        group.MapGet("/me", GetCurrentUser)
            .WithName("GetCurrentUser")
            .WithDescription("Get current logged in user");

        group.MapPost("/logout", Logout)
            .WithName("Logout")
            .WithDescription("Logout current user");
    }

    private static IResult RedirectToDiscord(IConfiguration config)
    {
        var clientId = config["Discord:ClientId"];
        var redirectUri = config["Discord:RedirectUri"] ?? "http://localhost:5200/auth/discord/callback";
        
        if (string.IsNullOrEmpty(clientId))
        {
            return Results.BadRequest(new { error = "Discord OAuth not configured" });
        }
        
        var scope = "identify email";
        var state = Guid.NewGuid().ToString("N");
        
        var url = $"https://discord.com/api/oauth2/authorize" +
                  $"?client_id={clientId}" +
                  $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                  $"&response_type=code" +
                  $"&scope={Uri.EscapeDataString(scope)}" +
                  $"&state={state}";
        
        return Results.Redirect(url);
    }

    private static async Task<IResult> DiscordCallback(
        string? code,
        string? error,
        IConfiguration config,
        PortalDbContext db,
        ILogger<Program> logger)
    {
        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code))
        {
            logger.LogWarning("Discord OAuth error: {Error}", error);
            return Results.Redirect("/?error=auth_failed");
        }
        
        var clientId = config["Discord:ClientId"];
        var clientSecret = config["Discord:ClientSecret"];
        var redirectUri = config["Discord:RedirectUri"] ?? "http://localhost:5200/auth/discord/callback";
        
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            return Results.Redirect("/?error=not_configured");
        }
        
        try
        {
            // Exchange code for token
            var tokenResponse = await _httpClient.PostAsync(
                "https://discord.com/api/oauth2/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["grant_type"] = "authorization_code",
                    ["code"] = code,
                    ["redirect_uri"] = redirectUri
                }));
            
            if (!tokenResponse.IsSuccessStatusCode)
            {
                var errorContent = await tokenResponse.Content.ReadAsStringAsync();
                logger.LogError("Discord token exchange failed: {Error}", errorContent);
                return Results.Redirect("/?error=token_failed");
            }
            
            var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
            var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenJson);
            var accessToken = tokenData.GetProperty("access_token").GetString();
            
            // Get user info
            var userRequest = new HttpRequestMessage(HttpMethod.Get, "https://discord.com/api/users/@me");
            userRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            
            var userResponse = await _httpClient.SendAsync(userRequest);
            if (!userResponse.IsSuccessStatusCode)
            {
                return Results.Redirect("/?error=user_fetch_failed");
            }
            
            var userJson = await userResponse.Content.ReadAsStringAsync();
            var userData = JsonSerializer.Deserialize<JsonElement>(userJson);
            
            var discordId = userData.GetProperty("id").GetString()!;
            var username = userData.GetProperty("username").GetString()!;
            var discriminator = userData.TryGetProperty("discriminator", out var disc) ? disc.GetString() : null;
            var avatar = userData.TryGetProperty("avatar", out var av) ? av.GetString() : null;
            var email = userData.TryGetProperty("email", out var em) ? em.GetString() : null;
            
            // Find or create user
            var user = await db.Users.FirstOrDefaultAsync(u => u.DiscordId == discordId);
            if (user == null)
            {
                user = new PortalUser
                {
                    DiscordId = discordId,
                    Username = username,
                    Discriminator = discriminator,
                    AvatarHash = avatar,
                    Email = email,
                    CreatedAt = DateTime.UtcNow
                };
                db.Users.Add(user);
            }
            else
            {
                user.Username = username;
                user.Discriminator = discriminator;
                user.AvatarHash = avatar;
                user.Email = email;
            }
            
            // Generate session token
            user.SessionToken = GenerateSessionToken();
            user.SessionExpiresAt = DateTime.UtcNow.AddDays(7);
            user.LastLoginAt = DateTime.UtcNow;
            
            await db.SaveChangesAsync();
            
            logger.LogInformation("User {Username} ({DiscordId}) logged in", username, discordId);
            
            // Redirect to dashboard with session token in cookie
            return Results.Redirect($"/?token={user.SessionToken}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Discord OAuth error");
            return Results.Redirect("/?error=internal_error");
        }
    }

    private static async Task<IResult> GetCurrentUser(
        HttpContext httpContext,
        PortalDbContext db)
    {
        var user = await GetAuthenticatedUser(httpContext, db);
        if (user == null)
        {
            return Results.Unauthorized();
        }
        
        return Results.Ok(new
        {
            id = user.Id,
            discordId = user.DiscordId,
            username = user.Username,
            avatar = user.GetAvatarUrl(),
            email = user.Email,
            isSuperAdmin = user.IsSuperAdmin
        });
    }

    private static async Task<IResult> Logout(
        HttpContext httpContext,
        PortalDbContext db)
    {
        var user = await GetAuthenticatedUser(httpContext, db);
        if (user != null)
        {
            user.SessionToken = null;
            user.SessionExpiresAt = null;
            await db.SaveChangesAsync();
        }
        
        return Results.Ok(new { success = true });
    }

    public static async Task<PortalUser?> GetAuthenticatedUser(HttpContext httpContext, PortalDbContext db)
    {
        var token = httpContext.Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "")
                    ?? httpContext.Request.Cookies["session"];
        
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }
        
        var user = await db.Users
            .Include(u => u.Servers)
            .FirstOrDefaultAsync(u => u.SessionToken == token && u.SessionExpiresAt > DateTime.UtcNow);
        
        return user;
    }

    private static string GenerateSessionToken()
    {
        var bytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}
