#!/usr/bin/env bash
set -euo pipefail

release_id="${1:?release id is required}"
enable_https="${2:-false}"
app_root="/home/MotoCare"
stage_dir="/tmp/motocare-${release_id}"
release_dir="${app_root}/releases/${release_id}"
current_link="${app_root}/current"
service_name="motocare"
site_name="moto.luandinh.com"

if [[ ! "${release_id}" =~ ^[0-9]{14}$ ]]; then
  echo "Invalid release id: ${release_id}" >&2
  exit 1
fi

previous_release=""
if [[ -L "${current_link}" ]]; then
  previous_release="$(readlink -f "${current_link}")"
fi

install -d -m 0755 "${app_root}/releases" "${release_dir}/api" "${release_dir}/web"
install -d -o www-data -g www-data -m 0750 \
  "${app_root}/shared" \
  "${app_root}/shared/data-protection-keys"

tar -xzf "${stage_dir}/api.tar.gz" -C "${release_dir}/api"
tar -xzf "${stage_dir}/web.tar.gz" -C "${release_dir}/web"
chmod 0755 "${release_dir}/api/MotoCare.Api"
chown -R root:www-data "${release_dir}"
find "${release_dir}" -type d -exec chmod 0750 {} +
find "${release_dir}" -type f -exec chmod 0640 {} +
chmod 0750 "${release_dir}/api/MotoCare.Api"

if [[ ! -f "${app_root}/shared/motocare.env" ]]; then
  umask 077
  jwt_key="$(openssl rand -base64 48 | tr -d '\n')"
  printf 'Jwt__SigningKey=%s\n' "${jwt_key}" > "${app_root}/shared/motocare.env"
  chown www-data:www-data "${app_root}/shared/motocare.env"
fi

ln -sfn "${release_dir}" "${current_link}"

install -m 0644 "${stage_dir}/motocare.service" "/etc/systemd/system/${service_name}.service"
if [[ ! -f "/etc/nginx/sites-available/${site_name}" ]]; then
  install -m 0644 "${stage_dir}/${site_name}.conf" "/etc/nginx/sites-available/${site_name}"
fi
ln -sfn "/etc/nginx/sites-available/${site_name}" "/etc/nginx/sites-enabled/${site_name}"

nginx -t
systemctl daemon-reload
systemctl enable "${service_name}.service" >/dev/null
systemctl restart "${service_name}.service"

healthy=false
for _ in {1..30}; do
  if curl --fail --silent --show-error http://127.0.0.1:5112/health >/dev/null; then
    healthy=true
    break
  fi
  sleep 2
done

if [[ "${healthy}" != "true" ]]; then
  journalctl -u "${service_name}.service" -n 80 --no-pager >&2 || true
  if [[ -n "${previous_release}" && -d "${previous_release}" ]]; then
    ln -sfn "${previous_release}" "${current_link}"
    systemctl restart "${service_name}.service" || true
  fi
  echo "MotoCare health check failed; previous release restored." >&2
  exit 1
fi

systemctl reload nginx

if [[ "${enable_https}" == "true" ]]; then
  if getent ahostsv4 "${site_name}" >/dev/null; then
    certbot --nginx \
      --domain "${site_name}" \
      --non-interactive \
      --agree-tos \
      --register-unsafely-without-email \
      --redirect
  else
    echo "DNS for ${site_name} is not available; HTTPS setup skipped."
  fi
fi

rm -rf -- "${stage_dir}"
echo "MotoCare ${release_id} installed successfully."
