# MotoCare

MotoCare là hệ thống quản lý tiệm sửa chữa và nâng cấp xe máy, gồm REST API,
giao diện quản trị trên web và ứng dụng desktop Windows.

## Chức năng chính

- Quản lý khách hàng, xe và danh mục hãng/dòng xe.
- Tiếp nhận, phân công và theo dõi phiếu sửa chữa.
- Quản lý phụ tùng, nhập/xuất kho và tồn kho.
- Lập hóa đơn, ghi nhận thanh toán, hoàn tiền và thu chi.
- Tích điểm, xếp hạng thành viên và quản lý loyalty.
- Dashboard, báo cáo và xuất dữ liệu Excel.
- Thông báo thời gian thực qua SignalR.
- Phân quyền theo vai trò: `Administrator`, `Manager`, `Receptionist`,
  `Technician` và `Cashier`.

## Công nghệ

| Thành phần | Công nghệ |
| --- | --- |
| Backend | ASP.NET Core 8 Web API, MongoDB Driver, JWT, SignalR, Swagger |
| Frontend | Nuxt 4, Vue 3, TypeScript, pnpm |
| Desktop | Tauri 2, Rust, NSIS |
| Dữ liệu | MongoDB 7 replica set |
| Triển khai | Docker images, Docker Compose, Nginx, PowerShell |

## Cấu trúc dự án

```text
MotoCare/
├── src/
│   ├── MotoCare.Api/       # Backend API
│   └── MotoCare.FE/        # Nuxt frontend và Tauri desktop
├── deploy/production/      # Cấu hình Nginx, systemd và script cài release
├── docs/                   # Tài liệu kỹ thuật/nghiệp vụ
├── docker-compose.yml      # MongoDB dùng cho môi trường local
└── MotoCare.sln
```

## Yêu cầu

- [.NET SDK 8](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js](https://nodejs.org/) và [pnpm](https://pnpm.io/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- Rust stable và Microsoft C++ Build Tools nếu cần build ứng dụng Tauri

## Chạy môi trường phát triển

### 1. Khởi động MongoDB

Từ thư mục gốc:

```powershell
docker compose up -d
```

MongoDB chạy tại:

```text
mongodb://localhost:27017/?replicaSet=rs0&directConnection=true
```

### 2. Chạy backend

```powershell
dotnet restore MotoCare.sln
dotnet run --project src/MotoCare.Api
```

- Swagger: `http://localhost:5014/swagger`
- Health check: `http://localhost:5014/health`
- API base URL: `http://localhost:5014/api/v1`

Tài khoản khởi tạo chỉ dành cho development:

```text
Username: admin
Password: Admin@123456
```

Hãy đổi mật khẩu ngay sau lần đăng nhập đầu tiên.

### 3. Chạy frontend

Mở terminal khác:

```powershell
Set-Location src/MotoCare.FE
pnpm install
$env:NUXT_PUBLIC_API_BASE = "http://localhost:5014/api/v1"
pnpm dev
```

Truy cập `http://localhost:3000`.

### 4. Chạy ứng dụng desktop (tùy chọn)

Sau khi backend đã chạy:

```powershell
Set-Location src/MotoCare.FE
$env:NUXT_PUBLIC_API_BASE = "http://localhost:5014/api/v1"
pnpm tauri:dev
```

## Cấu hình và bảo mật

Không lưu khóa hoặc mật khẩu production trong repository. ASP.NET Core đọc các
biến môi trường theo quy ước dấu gạch dưới kép:

```text
Mongo__ConnectionString
Mongo__DatabaseName
Jwt__SigningKey
SeedAdmin__Password
RestCountries__ApiKey
```

Ví dụ lưu API key cho môi trường development bằng .NET User Secrets:

```powershell
dotnet user-secrets set "RestCountries:ApiKey" "<API_KEY>" --project src/MotoCare.Api
```

`Jwt__SigningKey` phải có ít nhất 32 byte. Các tệp `.env`, private key, chứng thư
và data-protection key đã được loại khỏi Git.

## Kiểm tra build

Backend:

```powershell
dotnet build MotoCare.sln
```

Frontend:

```powershell
Set-Location src/MotoCare.FE
pnpm install --frozen-lockfile
pnpm build
```

## Triển khai và phát hành

- Triển khai web/API production: xem
  [`deploy/production/README.md`](deploy/production/README.md).
- Build và phát hành desktop Windows: xem
  [`src/MotoCare.FE/RELEASE_WINDOWS.md`](src/MotoCare.FE/RELEASE_WINDOWS.md).
- Mô tả kỹ thuật và nghiệp vụ chi tiết:
  [`docs/MO_TA_KY_THUAT_MOTOCARE.md`](docs/MO_TA_KY_THUAT_MOTOCARE.md).
