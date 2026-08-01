#!/usr/bin/env bash
set -euo pipefail

release_id="${1:?release id is required}"
enable_https="${2:-false}"
app_root="/home/MotoCare"
stage_dir="/tmp/motocare-${release_id}"
bundle_dir="${stage_dir}/bundle"
site_name="moto.luandinh.com"
nginx_site="/etc/nginx/sites-available/${site_name}"
nginx_backup="${stage_dir}/nginx-site.backup"
container_uid="1654"
container_gid="1654"

if [[ ! "${release_id}" =~ ^[0-9]{14}$ ]]; then
  echo "Invalid release id: ${release_id}" >&2
  exit 1
fi
for required_command in docker curl openssl tar grep nginx systemctl; do
  if ! command -v "${required_command}" >/dev/null 2>&1; then
    echo "Required command not found on the VPS: ${required_command}" >&2
    exit 1
  fi
done
if ! docker compose version >/dev/null 2>&1; then
  echo "The Docker Compose plugin is required on the VPS." >&2
  exit 1
fi

mkdir -p "${bundle_dir}"
tar -xzf "${stage_dir}/motocare-compose-${release_id}.tar.gz" -C "${bundle_dir}"

install -d -m 0755 "${app_root}"
install -d -m 0755 \
  "${app_root}/uploads" \
  "${app_root}/data-protection-keys" \
  "${app_root}/windows-releases"

chown -R "${container_uid}:${container_gid}" \
  "${app_root}/uploads" \
  "${app_root}/data-protection-keys"
chmod 0750 "${app_root}/uploads" "${app_root}/data-protection-keys"

install -m 0644 "${bundle_dir}/docker-compose.yml" "${app_root}/docker-compose.yml"
if [[ ! -f "${app_root}/appsettings.Production.json" ]]; then
  install -m 0640 "${bundle_dir}/appsettings.Production.json" \
    "${app_root}/appsettings.Production.json"
  echo "Created ${app_root}/appsettings.Production.json from the deployment bundle."
fi
chown root:"${container_gid}" "${app_root}/appsettings.Production.json"
chmod 0640 "${app_root}/appsettings.Production.json"

if [[ ! -f "${app_root}/.env" ]]; then
  umask 077
  jwt_key="$(openssl rand -hex 48)"
  admin_password="Admin-$(openssl rand -hex 12)"
  {
    printf 'Jwt__SigningKey=%s\n' "${jwt_key}"
    printf 'SeedAdmin__Password=%s\n' "${admin_password}"
    printf 'DemoData__Enabled=false\n'
  } > "${app_root}/.env"
  echo "Initial admin password: ${admin_password}"
  echo "Save it securely and change it after the first login."
fi
chown root:"${container_gid}" "${app_root}/.env"
chmod 0640 "${app_root}/.env"

if ! grep -Eq '"ConnectionString"[[:space:]]*:[[:space:]]*"mongodb(\+srv)?://' \
    "${app_root}/appsettings.Production.json"; then
  echo "MongoDB Cloud connection string is missing." >&2
  echo "Edit ${app_root}/appsettings.Production.json, then deploy again." >&2
  exit 1
fi
if grep -Eq 'mongodb://(localhost|127\.0\.0\.1)' "${app_root}/appsettings.Production.json"; then
  echo "Production MongoDB must not point to localhost; MongoDB is not part of this stack." >&2
  exit 1
fi
if grep -Eq 'mongodb\+srv://[^\"]*directConnection=true' \
    "${app_root}/appsettings.Production.json"; then
  echo "A mongodb+srv URI cannot use directConnection=true. Remove that query option." >&2
  exit 1
fi

docker load --input "${bundle_dir}/motocare-images.tar"

# Containers replace the old API service. Shared host Nginx keeps ports 80/443.
systemctl disable --now motocare.service >/dev/null 2>&1 || true

cd "${app_root}"
docker compose up -d --remove-orphans --force-recreate api web

healthy=false
for _ in {1..30}; do
  if curl --fail --silent --show-error http://127.0.0.1:5112/health >/dev/null; then
    healthy=true
    break
  fi
  sleep 2
done
if [[ "${healthy}" != "true" ]]; then
  docker compose ps >&2 || true
  docker compose logs --tail 100 api >&2 || true
  echo "MotoCare API health check failed." >&2
  exit 1
fi

had_nginx_site=false
if [[ -f "${nginx_site}" ]]; then
  cp -a "${nginx_site}" "${nginx_backup}"
  had_nginx_site=true
fi
if [[ -s "/etc/letsencrypt/live/${site_name}/fullchain.pem" ]]; then
  install -m 0644 "${bundle_dir}/host-https.conf" "${nginx_site}"
else
  install -m 0644 "${bundle_dir}/host-http.conf" "${nginx_site}"
fi
ln -sfn "${nginx_site}" "/etc/nginx/sites-enabled/${site_name}"
if ! nginx -t; then
  if [[ "${had_nginx_site}" == "true" ]]; then
    cp -a "${nginx_backup}" "${nginx_site}"
  else
    rm -f -- "${nginx_site}" "/etc/nginx/sites-enabled/${site_name}"
  fi
  nginx -t || true
  echo "New MotoCare host Nginx configuration is invalid; previous configuration restored." >&2
  exit 1
fi
systemctl reload nginx

if [[ "${enable_https}" == "true" \
      && ! -s "/etc/letsencrypt/live/${site_name}/fullchain.pem" ]]; then
  if ! command -v certbot >/dev/null 2>&1; then
    echo "Certbot is required to create the first HTTPS certificate." >&2
    exit 1
  fi
  certbot --nginx --domain "${site_name}" \
    --non-interactive --agree-tos --register-unsafely-without-email --redirect
fi

if ! curl --fail --silent --show-error http://127.0.0.1:5113/ >/dev/null; then
  docker compose logs --tail 100 web >&2 || true
  echo "MotoCare web container health check failed." >&2
  exit 1
fi

rm -rf -- "${stage_dir}"
echo "MotoCare ${release_id} is running with Docker Compose in ${app_root}."
