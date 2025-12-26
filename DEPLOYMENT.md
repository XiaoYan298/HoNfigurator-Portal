# HoNfigurator Management Portal - Deployment Guide

## Quick Start (Local Network)

### 1. แก้ไข appsettings.json

```json
{
  "Portal": {
    "BaseUrl": "http://YOUR_SERVER_IP:5200",
    "Port": 5200,
    "BindToAllInterfaces": true
  },
  "Discord": {
    "ClientId": "YOUR_DISCORD_CLIENT_ID",
    "ClientSecret": "YOUR_DISCORD_CLIENT_SECRET",
    "RedirectUri": "http://YOUR_SERVER_IP:5200/auth/discord/callback"
  }
}
```

### 2. ตั้งค่า Discord OAuth2

1. ไปที่ [Discord Developer Portal](https://discord.com/developers/applications)
2. เลือก Application ของคุณ
3. ไปที่ OAuth2 > General
4. เพิ่ม Redirect URI: `http://YOUR_SERVER_IP:5200/auth/discord/callback`
5. Save Changes

### 3. เปิด Firewall Port 5200

**Windows:**
```powershell
netsh advfirewall firewall add rule name="HoNfigurator Portal" dir=in action=allow protocol=tcp localport=5200
```

**Linux:**
```bash
sudo ufw allow 5200/tcp
```

### 4. Build และ Run

```bash
cd HoNfigurator.ManagementPortal
dotnet publish -c Release -o ./publish
cd publish
dotnet HoNfigurator.ManagementPortal.dll
```

---

## Production Deployment (with HTTPS)

### Option A: Docker Compose

1. **แก้ไข docker-compose.yml:**
   - เปลี่ยน `your-domain.com` เป็น domain ของคุณ
   - ใส่ Discord credentials

2. **สร้าง SSL certificates:**
   ```bash
   mkdir ssl
   # ใช้ Let's Encrypt หรือ certificate ของคุณ
   # วาง fullchain.pem และ privkey.pem ใน folder ssl/
   ```

3. **Run:**
   ```bash
   docker-compose up -d
   ```

### Option B: Manual with Reverse Proxy (Nginx)

1. **Build:**
   ```bash
   dotnet publish -c Release -o /var/www/honfigurator-portal
   ```

2. **สร้าง systemd service:**
   ```bash
   sudo nano /etc/systemd/system/honfigurator-portal.service
   ```
   
   ```ini
   [Unit]
   Description=HoNfigurator Management Portal
   After=network.target

   [Service]
   WorkingDirectory=/var/www/honfigurator-portal
   ExecStart=/usr/bin/dotnet /var/www/honfigurator-portal/HoNfigurator.ManagementPortal.dll
   Restart=always
   RestartSec=10
   User=www-data
   Environment=ASPNETCORE_ENVIRONMENT=Production
   Environment=Portal__BindToAllInterfaces=true
   Environment=Portal__BaseUrl=https://your-domain.com

   [Install]
   WantedBy=multi-user.target
   ```

3. **Enable และ Start service:**
   ```bash
   sudo systemctl enable honfigurator-portal
   sudo systemctl start honfigurator-portal
   ```

4. **ตั้งค่า Nginx:**
   ```nginx
   server {
       listen 80;
       server_name your-domain.com;
       return 301 https://$server_name$request_uri;
   }

   server {
       listen 443 ssl http2;
       server_name your-domain.com;

       ssl_certificate /etc/letsencrypt/live/your-domain.com/fullchain.pem;
       ssl_certificate_key /etc/letsencrypt/live/your-domain.com/privkey.pem;

       location / {
           proxy_pass http://localhost:5200;
           proxy_http_version 1.1;
           proxy_set_header Upgrade $http_upgrade;
           proxy_set_header Connection "upgrade";
           proxy_set_header Host $host;
           proxy_set_header X-Real-IP $remote_addr;
           proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
           proxy_set_header X-Forwarded-Proto $scheme;
           proxy_read_timeout 86400;
       }
   }
   ```

---

## Discord OAuth2 Setup

### สร้าง Discord Application

1. ไปที่ https://discord.com/developers/applications
2. คลิก "New Application"
3. ตั้งชื่อ (เช่น "HoNfigurator Portal")
4. ไปที่ OAuth2 > General
5. Copy **Client ID** และ **Client Secret**
6. เพิ่ม **Redirects**:
   - Development: `http://localhost:5200/auth/discord/callback`
   - Production: `https://your-domain.com/auth/discord/callback`

### ตั้งค่า Bot (Optional - สำหรับ Discord notifications)

1. ไปที่ Bot tab
2. คลิก "Add Bot"
3. Copy **Bot Token**
4. เปิด Privileged Gateway Intents ที่ต้องการ

---

## Environment Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `Portal__BaseUrl` | Public URL ของ Portal | `https://portal.example.com` |
| `Portal__Port` | Port ที่จะ listen | `5200` |
| `Portal__BindToAllInterfaces` | Bind 0.0.0.0 หรือ localhost | `true` |
| `Discord__ClientId` | Discord OAuth2 Client ID | `1234567890` |
| `Discord__ClientSecret` | Discord OAuth2 Client Secret | `xxxxx` |
| `Discord__RedirectUri` | OAuth2 Redirect URI | `https://portal.example.com/auth/discord/callback` |

---

## Troubleshooting

### Cannot connect from other machines
1. ตรวจสอบว่า `BindToAllInterfaces` เป็น `true`
2. ตรวจสอบ Firewall ว่าเปิด port 5200
3. ตรวจสอบ Windows Defender Firewall

### Discord login fails
1. ตรวจสอบ Redirect URI ตรงกันทั้งใน Discord Developer Portal และ config
2. ตรวจสอบ Client ID และ Client Secret

### SignalR connection fails
1. ถ้าใช้ reverse proxy ต้องตั้งค่า WebSocket support
2. ตรวจสอบ `proxy_read_timeout` ใน nginx

### Database errors
1. ตรวจสอบ permission ของ folder ที่เก็บ `portal.db`
2. ถ้าใช้ Docker ต้อง mount volume

---

## Security Recommendations

1. **ใช้ HTTPS** - สำคัญมากสำหรับ production
2. **เก็บ secrets ใน environment variables** - อย่าใส่ใน source code
3. **ใช้ reverse proxy** - nginx/caddy สำหรับ SSL termination
4. **Limit access** - ถ้าใช้ในองค์กร อาจจำกัด IP range
5. **Regular backups** - backup portal.db เป็นประจำ
