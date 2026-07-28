# MotoCare production deployment

Target:

- Web: `https://moto.luandinh.com`
- API: `https://moto.luandinh.com/api/v1`
- SignalR: `https://moto.luandinh.com/hubs/notifications`
- VPS: `root@103.12.77.73`
- API service: `motocare.service`, bound to `127.0.0.1:5112`

Deploy:

```powershell
.\deploy.ps1
```

or:

```bat
deploy-production.bat
```

After the DNS A record for `moto.luandinh.com` points to `103.12.77.73`, enable
HTTPS:

```powershell
.\deploy.ps1 -EnableHttps
```

The deployment builds a self-contained Linux API, generates the Nuxt static
site, uploads both archives, switches `/home/MotoCare/current` atomically and
rolls back the symlink if the API health check fails.
