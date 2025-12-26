# HoNfigurator Management Portal

A modern web-based management portal for HoNfigurator game servers. Built with .NET 10, featuring real-time updates via SignalR and a clean minimal UI.

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet)
![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)

## ✨ Features

- **🔐 Discord OAuth2 Authentication** - Secure login via Discord
- **👥 Role-Based Access Control** - SuperAdmin, Owner, and User roles
- **📊 Real-time Dashboard** - Live server status updates via SignalR
- **🖥️ Multi-Server Management** - Manage multiple HoNfigurator instances
- **🔑 API Key Management** - Secure API key generation and rotation
- **📡 Server Actions** - Start, stop, restart servers remotely
- **🎮 Instance Management** - View and control game server instances
- **📢 Broadcast Messages** - Send announcements to all connected clients
- **🌐 Access Control** - Grant/revoke user access per server

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Management Portal                         │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐  │
│  │   Web UI    │  │  REST API   │  │   SignalR Hub       │  │
│  │  (Alpine.js)│  │  (Minimal)  │  │  (Real-time)        │  │
│  └──────┬──────┘  └──────┬──────┘  └──────────┬──────────┘  │
│         │                │                     │             │
│         └────────────────┼─────────────────────┘             │
│                          │                                   │
│  ┌───────────────────────┴───────────────────────────────┐  │
│  │              ASP.NET Core 10 Backend                   │  │
│  │  ┌─────────────┐  ┌─────────────┐  ┌───────────────┐  │  │
│  │  │ Auth Service│  │Status Service│  │Portal DB (SQLite)│  │
│  │  └─────────────┘  └─────────────┘  └───────────────┘  │  │
│  └───────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                  HoNfigurator API Servers                    │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐         │
│  │  Server 1   │  │  Server 2   │  │  Server N   │   ...   │
│  │ (API + Game)│  │ (API + Game)│  │ (API + Game)│         │
│  └─────────────┘  └─────────────┘  └─────────────┘         │
└─────────────────────────────────────────────────────────────┘
```

## 🔄 How It Works

### 1. Authentication Flow

```
User → Discord OAuth2 → Portal validates → JWT Token issued → Access granted
```

1. User clicks "Login with Discord"
2. Redirected to Discord OAuth2 authorization
3. Discord returns authorization code
4. Portal exchanges code for access token
5. Portal fetches user info from Discord API
6. Portal creates/updates user in SQLite database
7. JWT token issued to browser (stored in localStorage)

### 2. Server Connection Flow

```
Portal → HTTP Request → HoNfigurator API → Response → Update UI
```

1. Portal sends request to registered HoNfigurator server
2. Request includes API key in `X-API-Key` header
3. HoNfigurator API validates key and processes request
4. Response returned to Portal
5. UI updated via SignalR broadcast

### 3. Real-time Updates

```
Background Service → Poll Servers → SignalR Hub → All Connected Clients
```

1. `ServerStatusService` runs every 30 seconds
2. Fetches status from all registered servers
3. Broadcasts updates via `PortalHub` SignalR hub
4. All connected browsers receive instant updates

### 4. Role Hierarchy

| Role | Permissions |
|------|-------------|
| **SuperAdmin** | All servers, all actions, manage users, manage SuperAdmins |
| **Owner** | Own servers only, all actions, manage server access |
| **User** | Granted servers only, view status, limited actions |

## 🚀 Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (Preview)
- Discord Application (for OAuth2)

### Configuration

1. Create a Discord Application at [Discord Developer Portal](https://discord.com/developers/applications)

2. Configure OAuth2 redirect URL:
   ```
   https://your-domain.com/auth/discord/callback
   ```

3. Update `appsettings.json`:
   ```json
   {
     "Discord": {
       "ClientId": "YOUR_DISCORD_CLIENT_ID",
       "ClientSecret": "YOUR_DISCORD_CLIENT_SECRET",
       "RedirectUri": "https://your-domain.com/auth/discord/callback"
     },
     "Jwt": {
       "Secret": "your-super-secret-jwt-key-at-least-32-chars",
       "Issuer": "HoNfigurator-Portal",
       "Audience": "HoNfigurator-Users"
     },
     "SuperAdmins": ["YOUR_DISCORD_USER_ID"]
   }
   ```

### Run Locally

```bash
# Clone repository
git clone https://github.com/XiaoYan298/HoNfigurator-Portal.git
cd HoNfigurator-Portal

# Restore dependencies
dotnet restore

# Run in development mode
dotnet run

# Or run in production mode
dotnet run --environment Production
```

Portal will be available at:
- HTTP: `http://localhost:5050`
- HTTPS: `https://localhost:5051`

### Build for Production

```bash
# Build release
dotnet publish -c Release -o ./publish

# Run published app
cd publish
./HoNfigurator.ManagementPortal.exe
```

## 🐳 Docker Deployment

### Using Docker Compose

```bash
# Build and run
docker-compose up -d

# View logs
docker-compose logs -f
```

### Manual Docker Build

```bash
# Build image
docker build -t honfigurator-portal .

# Run container
docker run -d \
  -p 5000:5000 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e Discord__ClientId=YOUR_CLIENT_ID \
  -e Discord__ClientSecret=YOUR_CLIENT_SECRET \
  -v portal-data:/app/data \
  honfigurator-portal
```

## 📡 API Endpoints

### Authentication

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/auth/discord` | Initiate Discord OAuth2 login |
| GET | `/auth/discord/callback` | OAuth2 callback handler |
| GET | `/auth/me` | Get current user info |
| POST | `/auth/logout` | Logout user |

### Servers

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/servers` | List all accessible servers |
| POST | `/api/servers` | Add new server (Owner+) |
| PUT | `/api/servers/{id}` | Update server (Owner+) |
| DELETE | `/api/servers/{id}` | Delete server (Owner+) |
| GET | `/api/servers/{id}/status` | Get server status |
| POST | `/api/servers/{id}/start` | Start server |
| POST | `/api/servers/{id}/stop` | Stop server |
| POST | `/api/servers/{id}/restart` | Restart server |

### Server Access

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/servers/{id}/access` | List users with access |
| POST | `/api/servers/{id}/access` | Grant user access |
| DELETE | `/api/servers/{id}/access/{odId}` | Revoke user access |

### Admin

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/admin/users` | List all users (SuperAdmin) |
| POST | `/api/admin/superadmin` | Add SuperAdmin (SuperAdmin) |
| DELETE | `/api/admin/superadmin/{userId}` | Remove SuperAdmin (SuperAdmin) |

## 🔌 SignalR Events

### Client → Server

```javascript
// Join server room for updates
connection.invoke("JoinServerRoom", serverId);

// Leave server room
connection.invoke("LeaveServerRoom", serverId);
```

### Server → Client

```javascript
// Receive server status update
connection.on("ServerStatusUpdate", (serverId, status) => {
    // Update UI with new status
});

// Receive broadcast message
connection.on("BroadcastMessage", (message) => {
    // Show notification
});
```

## 🗄️ Database Schema

SQLite database (`portal.db`) with the following tables:

```sql
-- Users table
CREATE TABLE Users (
    Id INTEGER PRIMARY KEY,
    DiscordId TEXT UNIQUE NOT NULL,
    Username TEXT NOT NULL,
    Discriminator TEXT,
    Avatar TEXT,
    IsSuperAdmin INTEGER DEFAULT 0,
    CreatedAt TEXT NOT NULL,
    LastLoginAt TEXT
);

-- Servers table
CREATE TABLE Servers (
    Id INTEGER PRIMARY KEY,
    Name TEXT NOT NULL,
    ApiUrl TEXT NOT NULL,
    ApiKey TEXT NOT NULL,
    OwnerDiscordId TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    IsOnline INTEGER DEFAULT 0,
    LastCheckedAt TEXT
);

-- ServerAccess table
CREATE TABLE ServerAccess (
    Id INTEGER PRIMARY KEY,
    ServerId INTEGER NOT NULL,
    DiscordId TEXT NOT NULL,
    GrantedAt TEXT NOT NULL,
    GrantedBy TEXT NOT NULL,
    FOREIGN KEY (ServerId) REFERENCES Servers(Id)
);
```

## 🎨 UI Design

The portal uses a **Clean Minimal Design** with:

- **Color Palette**: Zinc-based dark theme
  - Background: `zinc-950`
  - Cards/Modals: `zinc-900`
  - Borders: `zinc-800`
  - Text: `white` (primary), `zinc-400` (secondary)
  - Accent: `orange-500`

- **Technologies**:
  - [Tailwind CSS](https://tailwindcss.com/) - Utility-first CSS
  - [Alpine.js](https://alpinejs.dev/) - Lightweight reactivity
  - [Heroicons](https://heroicons.com/) - SVG icons

## 📁 Project Structure

```
HoNfigurator.ManagementPortal/
├── Data/
│   └── PortalDbContext.cs      # EF Core DbContext
├── Endpoints/
│   ├── AuthEndpoints.cs        # Authentication routes
│   └── PortalEndpoints.cs      # API routes
├── Hubs/
│   └── PortalHub.cs            # SignalR hub
├── Models/
│   └── ServerModels.cs         # Data models
├── Services/
│   └── ServerStatusService.cs  # Background status checker
├── wwwroot/
│   ├── index.html              # Main SPA page
│   └── favicon.png             # Site icon
├── Program.cs                  # Application entry point
├── appsettings.json            # Configuration
├── Dockerfile                  # Docker build
└── docker-compose.yml          # Docker Compose
```

## 🔧 Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `ASPNETCORE_ENVIRONMENT` | Runtime environment | `Development` |
| `ASPNETCORE_URLS` | Listen URLs | `http://localhost:5200` |
| `Discord__ClientId` | Discord OAuth2 Client ID | - |
| `Discord__ClientSecret` | Discord OAuth2 Client Secret | - |
| `Discord__RedirectUri` | OAuth2 callback URL | - |
| `Jwt__Secret` | JWT signing key (32+ chars) | - |
| `SuperAdmins__0` | First SuperAdmin Discord ID | - |

## 🤝 Integration with HoNfigurator

This portal is designed to work with [HoNfigurator](https://github.com/XiaoYan298/HoNfigurator-.NET) game servers.

### Required HoNfigurator API Endpoints

The portal expects the following endpoints on HoNfigurator servers:

```
GET  /api/status          # Server status
GET  /api/instances       # List game instances
POST /api/server/start    # Start server
POST /api/server/stop     # Stop server
POST /api/server/restart  # Restart server
POST /api/broadcast       # Send broadcast message
```

### API Key Authentication

All requests to HoNfigurator include the `X-API-Key` header:

```http
GET /api/status HTTP/1.1
Host: your-honfigurator-server.com
X-API-Key: your-api-key-here
```

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- [Project KONGOR](https://github.com/Project-KONGOR-Open-Source) - Community revival project
- [ASP.NET Core](https://docs.microsoft.com/aspnet/core) - Web framework
- [Tailwind CSS](https://tailwindcss.com/) - CSS framework
- [Alpine.js](https://alpinejs.dev/) - JavaScript framework
