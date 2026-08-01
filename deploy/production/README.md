# MotoCare production deployment (Docker Compose)

Production runs two containers only:

- `motocare-api`: ASP.NET Core API, published on `127.0.0.1:5112` and on the
  internal Compose network.
- `motocare-web`: generated Nuxt site served on `127.0.0.1:5113` by its own
  Nginx container.

The VPS's existing shared Nginx remains responsible for public ports 80/443
and TLS. Its MotoCare virtual host proxies the frontend to port 5113 and the API,
SignalR and uploaded files to port 5112. Other sites on the VPS are unaffected.

MongoDB is not installed on the VPS. The API uses the MongoDB Cloud connection
string in `appsettings.Production.json`.

All runtime files live in `/home/MotoCare`:

```text
/home/MotoCare/
├── docker-compose.yml
├── appsettings.Production.json
├── .env
├── uploads/
├── data-protection-keys/
└── windows-releases/
```

No systemd service is created. Both application containers use
`restart: unless-stopped`.

## Requirements

Local Windows computer:

- Docker Desktop running Linux containers
- OpenSSH (`ssh.exe` and `scp.exe`)
- `tar.exe`

VPS:

- Docker Engine
- Docker Compose plugin (`docker compose`)
- host Nginx, `curl` and `openssl`
- DNS A record for `moto.luandinh.com` pointing to the VPS when HTTPS is enabled

The first Compose deployment disables only the old `motocare.service`. It
updates and validates the MotoCare Nginx virtual host, then reloads shared Nginx.

## Configuration

Set the MongoDB Cloud connection string in
`src/MotoCare.Api/appsettings.Production.json`, or keep a separate private file
and pass it to the deployment script:

```powershell
.\deploy.ps1 -AppSettingsPath 'D:\secrets\motocare.appsettings.Production.json'
```

For an Atlas-style `mongodb+srv://` URI, do not append
`directConnection=true`; SRV discovery and direct connection are incompatible.

On the first deployment only, that file is copied to
`/home/MotoCare/appsettings.Production.json`. Later deployments never overwrite
the VPS copy, so production configuration is preserved.

If `/home/MotoCare/.env` is missing, the installer creates it with:

- a cryptographically random JWT signing key;
- a random initial admin password;
- the destructive demo-data feature disabled.

The generated admin password is printed once at the end of the first successful
deployment. Environment variables in `.env` override values in appsettings by
using ASP.NET Core's double-underscore convention.

## Deploy

Build both Linux images locally, export them, upload them over SSH and start
Compose on the VPS:

```powershell
.\deploy.ps1
```

For the first HTTPS deployment, after DNS is ready:

```powershell
.\deploy.ps1 -EnableHttps
```

The installer reuses the existing host Let's Encrypt certificate when one
exists. Otherwise `-EnableHttps` asks the host's Certbot installation to obtain
one after the HTTP virtual host is active.

Custom SSH key or VPS:

```powershell
.\deploy.ps1 -VpsHost '203.0.113.10' -VpsUser root -SshKey "$env:USERPROFILE\.ssh\id_ed25519" -EnableHttps
```

## Operations on the VPS

```bash
cd /home/MotoCare
docker compose ps
docker compose logs -f --tail 100 api web
docker compose restart api web
```

Certificate renewal remains managed by the host Certbot installation:

```bash
certbot renew
```

Back up at least these paths:

- `/home/MotoCare/appsettings.Production.json`
- `/home/MotoCare/.env`
- `/home/MotoCare/uploads/`
- the MongoDB Cloud database (using the provider's backup facilities)
