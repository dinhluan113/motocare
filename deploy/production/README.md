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

## Internal demo-data tool

The destructive demo-data tool is disabled by default in Production. During
the internal demo phase, add the following line to
`/home/MotoCare/shared/motocare.env`, then restart `motocare.service`:

```dotenv
DemoData__Enabled=true
```

Before customer handover, remove that line or set it to `false` and restart the
service. The **Settings** navigation item and the reset operation will then be
unavailable; the API also rejects direct reset requests.
