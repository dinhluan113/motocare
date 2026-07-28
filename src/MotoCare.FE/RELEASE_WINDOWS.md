# Build và phát hành MotoCare cho Windows

## Chuẩn bị một lần

Yêu cầu: Node.js, pnpm, Rust stable và Microsoft C++ Build Tools.

1. Updater đã được cấu hình dùng `https://moto.luandinh.com/releases`.
2. Chạy:

   ```bat
   build-release.bat init-key
   ```

3. Sao lưu private key và mật khẩu. Không commit hoặc upload private key.

## Build và upload

Build theo version/ghi chú truyền vào:

```bat
build-release.bat 1.2.3 "Sửa lỗi và cải thiện hiệu năng"
```

Script sẽ:

1. cập nhật version, release notes và updater endpoint trong `release.config.json`
   và `src-tauri/tauri.conf.json`;
2. build Nuxt + Tauri NSIS cho Windows;
3. ký updater artifact;
4. tạo `latest.json`;
5. publish installer, `.sig` và `latest.json`.

Release được upload lên VPS qua SSH bằng cấu hình:

```json
{
  "upload": {
    "method": "scp",
    "localDirectory": "release\\published",
    "scpDestination": "root@103.12.77.73:/home/MotoCare/windows-releases"
  }
}
```

Máy chủ phải phục vụ đúng URL:

```text
https://moto.luandinh.com/releases/latest.json
https://moto.luandinh.com/releases/MotoCare_1.2.3_x64-setup.exe
```

Có thể đặt trước mật khẩu khóa trong session CI:

```powershell
$env:TAURI_SIGNING_PRIVATE_KEY_PASSWORD = '...'
```

Đây là chữ ký updater của Tauri. Khi phát hành công khai trên Windows, nên cấu hình
thêm chứng thư code-signing Windows để tránh cảnh báo SmartScreen.
