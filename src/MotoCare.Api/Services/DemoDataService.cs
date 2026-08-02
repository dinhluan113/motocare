using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using MotoCare.Api.Domain;
using MotoCare.Api.Infrastructure;

namespace MotoCare.Api.Services;

public sealed record DemoAccountResult(string Username, string FullName, string Role, string Password);

public sealed record DemoDataResetResult(
    DateTime CompletedAt,
    IReadOnlyDictionary<string, int> Counts,
    IReadOnlyList<DemoAccountResult> DemoAccounts);

public sealed class DemoDataService(
    MongoDbContext context,
    IOptions<DemoDataOptions> options,
    ILogger<DemoDataService> logger)
{
    private static readonly SemaphoreSlim ResetLock = new(1, 1);
    private readonly DemoDataOptions _options = options.Value;

    public async Task<DemoDataResetResult> ResetAsync(
        string currentAdminId,
        string confirmation,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            throw new KeyNotFoundException("Tính năng dữ liệu mẫu không được bật trên môi trường này.");
        if (!string.Equals(confirmation.Trim(), _options.ConfirmationPhrase, StringComparison.Ordinal))
            throw new InvalidOperationException("Câu xác nhận không chính xác.");
        if (!await ResetLock.WaitAsync(0, cancellationToken))
            throw new InvalidOperationException("Một tiến trình tạo dữ liệu mẫu khác đang chạy.");

        try
        {
            var currentAdmin = await context.Collection<AppUser>()
                .Find(x => x.Id == currentAdminId && x.IsActive && !x.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException("Không tìm thấy tài khoản quản trị đang thao tác.");
            if (!currentAdmin.Roles.Any(x =>
                    x.Equals(SecurityRoles.Admin, StringComparison.OrdinalIgnoreCase)
                    || x.Equals(SecurityRoles.LegacyAdministrator, StringComparison.OrdinalIgnoreCase)))
                throw new UnauthorizedAccessException("Chỉ quản trị viên được tạo lại dữ liệu mẫu.");

            var data = Build(currentAdmin, _options.DemoPassword);
            Validate(data);

            using var session = await context.Client.StartSessionAsync(cancellationToken: cancellationToken);
            session.StartTransaction();
            try
            {
                foreach (var collectionName in CollectionNames.All)
                {
                    await context.Database.GetCollection<BsonDocument>(collectionName).DeleteManyAsync(
                        session,
                        FilterDefinition<BsonDocument>.Empty,
                        cancellationToken: cancellationToken);
                }

                await InsertAll(session, data, cancellationToken);
                await session.CommitTransactionAsync(cancellationToken);
            }
            catch
            {
                if (session.IsInTransaction) await session.AbortTransactionAsync(cancellationToken);
                throw;
            }

            var counts = data.Counts();
            logger.LogWarning(
                "Admin {AdminId} cleared the database and created the complete demo dataset with {DocumentCount} documents.",
                currentAdminId,
                counts.Values.Sum());
            return new DemoDataResetResult(
                DateTime.UtcNow,
                counts,
                [
                    new("manager.demo", "Nguyễn Minh Quân", SecurityRoles.Manager, _options.DemoPassword),
                    new("staff.demo", "Trần Thu Hà", SecurityRoles.Employee, _options.DemoPassword)
                ]);
        }
        finally
        {
            ResetLock.Release();
        }
    }

    private async Task InsertAll(
        IClientSessionHandle session,
        DemoDataSet d,
        CancellationToken cancellationToken)
    {
        await Insert(session, d.Users, cancellationToken);
        await Insert(session, d.Employees, cancellationToken);
        await Insert(session, d.VehicleBrands, cancellationToken);
        await Insert(session, d.VehicleModels, cancellationToken);
        await Insert(session, d.Customers, cancellationToken);
        await Insert(session, d.Vehicles, cancellationToken);
        await Insert(session, d.PartBrands, cancellationToken);
        await Insert(session, d.Suppliers, cancellationToken);
        await Insert(session, d.PartCategories, cancellationToken);
        await Insert(session, d.ServiceCategories, cancellationToken);
        await Insert(session, d.Parts, cancellationToken);
        await Insert(session, d.SupplierPartStocks, cancellationToken);
        await Insert(session, d.InventoryTransactions, cancellationToken);
        await Insert(session, d.RepairOrders, cancellationToken);
        await Insert(session, d.Coupons, cancellationToken);
        await Insert(session, d.Invoices, cancellationToken);
        await Insert(session, d.CashCategories, cancellationToken);
        await Insert(session, d.CashTransactions, cancellationToken);
        await Insert(session, d.LoyaltyTiers, cancellationToken);
        await Insert(session, d.LoyaltyRules, cancellationToken);
        await Insert(session, d.LoyaltyAccounts, cancellationToken);
        await Insert(session, d.LoyaltyTransactions, cancellationToken);
        await Insert(session, d.Notifications, cancellationToken);
        await Insert(session, d.AuditLogs, cancellationToken);
        await Insert(session, d.Sequences, cancellationToken);
    }

    private Task Insert<T>(
        IClientSessionHandle session,
        IReadOnlyCollection<T> documents,
        CancellationToken cancellationToken) where T : BaseDocument =>
        documents.Count == 0
            ? Task.CompletedTask
            : context.Collection<T>().InsertManyAsync(
                session,
                documents,
                cancellationToken: cancellationToken);

    private static DemoDataSet Build(AppUser currentAdmin, string demoPassword)
    {
        var d = new DemoDataSet();
        var now = DateTime.UtcNow;
        var today = now.Date;

        var employees = new[]
        {
            Stamp(new Employee { EmployeeCode = "NV-000001", FullName = "Nguyễn Minh Quân", Phone = "0901000001", Email = "quan.manager@example.test", DateOfBirth = new DateTime(1988, 5, 12, 0, 0, 0, DateTimeKind.Utc), HireDate = today.AddYears(-6), Position = "Quản lý xưởng", SkillLevel = "Chuyên gia", Specialties = ["Điều phối xưởng", "Chẩn đoán", "Kiểm soát chất lượng"], BaseSalary = 18_000_000, Address = "12 Nguyễn Văn Linh, Quận 7, TP. Hồ Chí Minh", AddressDetails = Address("12 Nguyễn Văn Linh", "79", "Thành phố Hồ Chí Minh", "778", "Quận 7"), Notes = "Dữ liệu mẫu: phụ trách vận hành toàn xưởng." }, today.AddYears(-6)),
            Stamp(new Employee { EmployeeCode = "NV-000002", FullName = "Trần Thu Hà", Phone = "0901000002", Email = "ha.advisor@example.test", DateOfBirth = new DateTime(1994, 9, 21, 0, 0, 0, DateTimeKind.Utc), HireDate = today.AddYears(-3), Position = "Cố vấn dịch vụ", SkillLevel = "Khá", Specialties = ["Tiếp nhận xe", "Tư vấn bảo dưỡng", "Chăm sóc khách hàng"], BaseSalary = 11_000_000, Address = "28 Phan Văn Trị, Gò Vấp, TP. Hồ Chí Minh", AddressDetails = Address("28 Phan Văn Trị", "79", "Thành phố Hồ Chí Minh", "764", "Quận Gò Vấp") }, today.AddYears(-3)),
            Stamp(new Employee { EmployeeCode = "NV-000003", FullName = "Lê Quốc Bảo", Phone = "0901000003", HireDate = today.AddYears(-4), Position = "Kỹ thuật viên máy", SkillLevel = "Giỏi", Specialties = ["Động cơ", "Phun xăng điện tử", "Xe côn tay"], BaseSalary = 14_000_000, Address = "45 Lê Văn Việt, TP. Thủ Đức" }, today.AddYears(-4)),
            Stamp(new Employee { EmployeeCode = "NV-000004", FullName = "Phạm Đức Long", Phone = "0901000004", HireDate = today.AddYears(-2), Position = "Kỹ thuật viên điện", SkillLevel = "Khá", Specialties = ["Điện xe", "Ắc quy", "Đèn và khóa thông minh"], BaseSalary = 12_500_000, Address = "73 Cách Mạng Tháng Tám, Quận 10" }, today.AddYears(-2)),
            Stamp(new Employee { EmployeeCode = "NV-000005", FullName = "Võ Thanh Tâm", Phone = "0901000005", HireDate = today.AddYears(-2), Position = "Thủ kho", SkillLevel = "Khá", Specialties = ["Kho phụ tùng", "Nhập hàng", "Kiểm kê"], BaseSalary = 10_500_000, Address = "18 Tân Kỳ Tân Quý, Tân Phú" }, today.AddYears(-2)),
            Stamp(new Employee { EmployeeCode = "NV-000006", FullName = "Đỗ Ngọc Mai", Phone = "0901000006", HireDate = today.AddYears(-1), Position = "Kế toán", SkillLevel = "Khá", Specialties = ["Thu chi", "Công nợ", "Hóa đơn"], BaseSalary = 12_000_000, Status = EmployeeStatus.OnLeave, Notes = "Dữ liệu mẫu: đang nghỉ phép đến cuối tuần." }, today.AddYears(-1)),
            Stamp(new Employee { EmployeeCode = "NV-000007", FullName = "Hoàng Văn Phúc", Phone = "0901000007", HireDate = today.AddYears(-5), Position = "Kỹ thuật viên", SkillLevel = "Trung bình", Specialties = ["Bảo dưỡng cơ bản"], BaseSalary = 9_000_000, Status = EmployeeStatus.Inactive, Notes = "Dữ liệu mẫu: nhân sự đã nghỉ việc." }, today.AddYears(-5))
        };
        d.Employees.AddRange(employees);

        currentAdmin.Roles = [SecurityRoles.Admin];
        currentAdmin.IsActive = true;
        currentAdmin.IsDeleted = false;
        currentAdmin.EmployeeId = null;
        currentAdmin.UpdatedAt = now;
        var managerUser = Stamp(new AppUser { Username = "manager.demo", NormalizedUsername = "MANAGER.DEMO", FullName = employees[0].FullName, EmployeeId = employees[0].Id, Roles = [SecurityRoles.Manager] }, today.AddDays(-120));
        var staffUser = Stamp(new AppUser { Username = "staff.demo", NormalizedUsername = "STAFF.DEMO", FullName = employees[1].FullName, EmployeeId = employees[1].Id, Roles = [SecurityRoles.Employee] }, today.AddDays(-90));
        var hasher = new PasswordHasher<AppUser>();
        managerUser.PasswordHash = hasher.HashPassword(managerUser, demoPassword);
        staffUser.PasswordHash = hasher.HashPassword(staffUser, demoPassword);
        employees[0].UserId = managerUser.Id;
        employees[1].UserId = staffUser.Id;
        d.Users.AddRange([currentAdmin, managerUser, staffUser]);

        d.VehicleBrands.AddRange([
            Stamp(new VehicleBrand { Code = "HONDA", Name = "Honda", Country = "Nhật Bản" }, today.AddYears(-5)),
            Stamp(new VehicleBrand { Code = "YAMAHA", Name = "Yamaha", Country = "Nhật Bản" }, today.AddYears(-5)),
            Stamp(new VehicleBrand { Code = "SUZUKI", Name = "Suzuki", Country = "Nhật Bản" }, today.AddYears(-4)),
            Stamp(new VehicleBrand { Code = "PIAGGIO", Name = "Piaggio", Country = "Ý" }, today.AddYears(-4)),
            Stamp(new VehicleBrand { Code = "SYM", Name = "SYM", Country = "Đài Loan" }, today.AddYears(-3))
        ]);
        var honda = d.VehicleBrands[0]; var yamaha = d.VehicleBrands[1]; var suzuki = d.VehicleBrands[2]; var piaggio = d.VehicleBrands[3]; var sym = d.VehicleBrands[4];
        d.VehicleModels.AddRange([
            new VehicleModel { BrandId = honda.Id, Code = "WAVE-ALPHA-110", Name = "Wave Alpha 110", VehicleType = "Xe số", EngineCapacityCc = 109 },
            new VehicleModel { BrandId = honda.Id, Code = "VISION-110", Name = "Vision 110", VehicleType = "Xe tay ga", EngineCapacityCc = 109 },
            new VehicleModel { BrandId = honda.Id, Code = "AIR-BLADE-125", Name = "Air Blade 125", VehicleType = "Xe tay ga", EngineCapacityCc = 125 },
            new VehicleModel { BrandId = honda.Id, Code = "SH-MODE-125", Name = "SH Mode 125", VehicleType = "Xe tay ga", EngineCapacityCc = 125 },
            new VehicleModel { BrandId = yamaha.Id, Code = "JANUS-125", Name = "Janus 125", VehicleType = "Xe tay ga", EngineCapacityCc = 125 },
            new VehicleModel { BrandId = yamaha.Id, Code = "EXCITER-155", Name = "Exciter 155 VVA", VehicleType = "Xe côn tay", EngineCapacityCc = 155 },
            new VehicleModel { BrandId = yamaha.Id, Code = "GRANDE-125", Name = "Grande 125", VehicleType = "Xe tay ga", EngineCapacityCc = 125 },
            new VehicleModel { BrandId = suzuki.Id, Code = "RAIDER-R150", Name = "Raider R150", VehicleType = "Xe côn tay", EngineCapacityCc = 147 },
            new VehicleModel { BrandId = suzuki.Id, Code = "BURGMAN-125", Name = "Burgman Street 125", VehicleType = "Xe tay ga", EngineCapacityCc = 125 },
            new VehicleModel { BrandId = piaggio.Id, Code = "LIBERTY-125", Name = "Liberty 125", VehicleType = "Xe tay ga", EngineCapacityCc = 125 },
            new VehicleModel { BrandId = piaggio.Id, Code = "PRIMAVERA-125", Name = "Vespa Primavera 125", VehicleType = "Xe tay ga", EngineCapacityCc = 125 },
            new VehicleModel { BrandId = sym.Id, Code = "ELEGANT-110", Name = "Elegant 110", VehicleType = "Xe số", EngineCapacityCc = 110 }
        ]);

        var customerNames = new[] { "Nguyễn Văn An", "Trần Thị Bình", "Lê Hoàng Nam", "Phạm Minh Châu", "Võ Quốc Huy", "Đặng Thu Trang", "Bùi Gia Khang", "Đỗ Mỹ Linh", "Phan Anh Tuấn", "Hồ Ngọc Yến" };
        var districts = new[] { "Quận 1", "Quận 3", "Quận 7", "Quận Bình Thạnh", "Quận Gò Vấp", "Quận Tân Bình", "Thành phố Thủ Đức", "Quận 10", "Quận 5", "Huyện Bình Chánh" };
        for (var i = 0; i < customerNames.Length; i++)
        {
            d.Customers.Add(Stamp(new Customer
            {
                Code = $"CUS-{today:yyyyMM}-{i + 1:0000}", FullName = customerNames[i], Phone = $"09020000{i + 1:00}", NormalizedPhone = $"09020000{i + 1:00}",
                Email = $"khachhang{i + 1}@example.test", Address = $"{15 + i * 7} Đường mẫu, {districts[i]}, TP. Hồ Chí Minh", DateOfBirth = new DateTime(1985 + i, i % 12 + 1, i % 25 + 1, 0, 0, 0, DateTimeKind.Utc),
                Gender = i % 2 == 0 ? "Nam" : "Nữ", Notes = i == 0 ? "Khách quen, ưu tiên nhắc lịch thay nhớt." : "Hồ sơ khách hàng minh họa; thông tin liên hệ không dùng thực tế.", IsActive = i != 9
            }, today.AddMonths(-(18 - i))));
        }

        var vehicleSpecs = new (int Customer, int Model, string Plate, int Year, string Color, int Odo)[]
        {
            (0, 0, "59A1-123.45", 2021, "Đỏ đen", 18450), (0, 2, "59X3-246.80", 2023, "Xám", 7960),
            (1, 2, "59B2-135.79", 2022, "Trắng", 12680), (2, 5, "59C3-975.31", 2024, "Xanh GP", 5420),
            (3, 4, "59D2-468.02", 2020, "Đỏ", 23890), (4, 2, "59E1-864.20", 2023, "Xanh", 9100),
            (5, 7, "59F1-112.23", 2019, "Đen đỏ", 35600), (6, 9, "59G2-334.45", 2022, "Trắng", 14750),
            (7, 3, "59H1-556.67", 2024, "Đỏ", 3180), (8, 8, "59K2-778.89", 2021, "Đen", 19670),
            (8, 11, "59L1-990.01", 2018, "Xanh", 42200), (9, 10, "59M2-101.12", 2020, "Vàng", 11600)
        };
        foreach (var (customer, model, plate, year, color, odo) in vehicleSpecs)
            d.Vehicles.Add(Stamp(new Vehicle { CustomerId = d.Customers[customer].Id, VehicleModelId = d.VehicleModels[model].Id, LicensePlate = plate, NormalizedLicensePlate = Normalize.LicensePlate(plate), ManufactureYear = year, Color = color, Odometer = odo, PurchaseDate = new DateTime(year, 6, 15, 0, 0, 0, DateTimeKind.Utc), FrameNumber = $"DEMOFRAME{customer + 1:000000}", EngineNumber = $"DEMOENGINE{model + 1:000000}", Notes = "Số khung/số máy minh họa, không phải dữ liệu thật." }, today.AddMonths(-(12 - customer))));

        d.PartBrands.AddRange([
            new PartBrand { Code = "MOTUL", Name = "Motul", Country = "Pháp" }, new PartBrand { Code = "NGK", Name = "NGK", Country = "Nhật Bản" },
            new PartBrand { Code = "HONDA-GENUINE", Name = "Honda Genuine Parts", Country = "Nhật Bản" }, new PartBrand { Code = "YAMAHA-GENUINE", Name = "Yamaha Genuine Parts", Country = "Nhật Bản" },
            new PartBrand { Code = "IRC", Name = "IRC Tire", Country = "Nhật Bản" }, new PartBrand { Code = "GS", Name = "GS Battery", Country = "Nhật Bản" },
            new PartBrand { Code = "DID", Name = "D.I.D", Country = "Nhật Bản" }
        ]);
        d.Suppliers.AddRange([
            new Supplier { Code = "NCC-000001", Name = "Công ty Phụ tùng Minh Phát (Mẫu)", Phone = "02873000001", Address = "Quận 5, TP. Hồ Chí Minh", Notes = "Nhà cung cấp hư cấu dùng cho trình diễn.", AddressDetails = Address("Kho mẫu A", "79", "Thành phố Hồ Chí Minh", "774", "Quận 5") },
            new Supplier { Code = "NCC-000002", Name = "Nhà phân phối Dầu nhớt Thành Công (Mẫu)", Phone = "02873000002", Address = "Quận Tân Bình, TP. Hồ Chí Minh", Notes = "Nhà cung cấp hư cấu dùng cho trình diễn." },
            new Supplier { Code = "NCC-000003", Name = "Kho Vỏ xe An Tín (Mẫu)", Phone = "02873000003", Address = "TP. Thủ Đức, TP. Hồ Chí Minh", Notes = "Nhà cung cấp hư cấu dùng cho trình diễn." },
            new Supplier { Code = "NCC-000004", Name = "Điện xe & Ắc quy Nam Việt (Mẫu)", Phone = "02873000004", Address = "Quận 10, TP. Hồ Chí Minh", Notes = "Tạm ngừng đặt hàng để minh họa trạng thái.", IsActive = false }
        ]);
        d.PartCategories.AddRange([
            Category("DMPT-000001", "Dầu nhớt & dung dịch", "Nhớt máy, nhớt hộp số và dung dịch bảo dưỡng", [("VISCOSITY", "Độ nhớt", null, false, PartSpecificationDataType.Selection, new[] { "10W-30", "10W-40", "20W-40" }), ("VOLUME", "Dung tích", "L", true, PartSpecificationDataType.Number, Array.Empty<string>())]),
            Category("DMPT-000002", "Hệ thống đánh lửa", "Bugi và linh kiện đánh lửa", [("PLUG_CODE", "Mã bugi", null, true, PartSpecificationDataType.Text, Array.Empty<string>())]),
            Category("DMPT-000003", "Hệ thống phanh", "Má phanh, guốc phanh và dầu phanh", [("POSITION", "Vị trí", null, true, PartSpecificationDataType.Selection, new[] { "Trước", "Sau" })]),
            Category("DMPT-000004", "Truyền động", "Dây curoa, nhông sên dĩa", [("COMPATIBILITY", "Dòng xe", null, true, PartSpecificationDataType.Text, Array.Empty<string>())]),
            Category("DMPT-000005", "Lốp xe", "Lốp không săm và có săm", [("SIZE", "Kích thước", null, true, PartSpecificationDataType.Text, Array.Empty<string>()), ("TUBELESS", "Không săm", null, false, PartSpecificationDataType.Boolean, Array.Empty<string>())]),
            Category("DMPT-000006", "Điện & ắc quy", "Ắc quy, bóng đèn, cầu chì", [("VOLTAGE", "Điện áp", "V", true, PartSpecificationDataType.Number, Array.Empty<string>())]),
            Category("DMPT-000007", "Lọc gió", "Lọc gió động cơ", [("COMPATIBILITY", "Dòng xe", null, true, PartSpecificationDataType.Text, Array.Empty<string>())])
        ]);
        d.ServiceCategories.AddRange([
            Service("DV-000001", "Thay nhớt động cơ", 50_000, "Kiểm tra mức nhớt và thay nhớt; chưa gồm vật tư."),
            Service("DV-000002", "Bảo dưỡng định kỳ", 180_000, "Kiểm tra phanh, lốp, truyền động, điện và siết ốc."),
            Service("DV-000003", "Vệ sinh kim phun", 180_000, "Vệ sinh kim phun bằng thiết bị chuyên dụng."),
            Service("DV-000004", "Thay má phanh", 120_000, "Công tháo lắp, vệ sinh và căn chỉnh phanh."),
            Service("DV-000005", "Vệ sinh bộ truyền động CVT", 220_000, "Vệ sinh nồi xe tay ga, kiểm tra dây curoa."),
            Service("DV-000006", "Kiểm tra và sửa điện", 150_000, "Chẩn đoán hệ thống sạc, đề, đèn và dây điện."),
            Service("DV-000007", "Vá lốp không săm", 40_000, "Vá trong lốp không săm."),
            Service("DV-000008", "Thay lốp", 60_000, "Công tháo lắp và kiểm tra áp suất."),
            Service("DV-000009", "Cứu hộ nội thành", 250_000, "Chi phí cơ bản trong bán kính 10 km."),
            Service("DV-000010", "Kiểm tra tổng quát", 80_000, "Chẩn đoán ban đầu trước khi báo giá.")
        ]);

        var prices = new (string Code, string Name, int Brand, int Cat, string Unit, decimal Import, decimal Stock, decimal Sale, decimal Qty, decimal Min, int Supplier, (string, string, string?, string)[] Specs)[]
        {
            ("PT-000001", "Nhớt Motul Scooter MA 4T 10W-40 0,8L", 0, 0, "Chai", 125_000, 135_000, 165_000, 18, 8, 1, [("VISCOSITY", "Độ nhớt", null, "10W-40"), ("VOLUME", "Dung tích", "L", "0.8")]),
            ("PT-000002", "Nhớt động cơ Honda 4T SL 10W-30 0,8L", 2, 0, "Chai", 88_000, 95_000, 120_000, 24, 10, 0, [("VISCOSITY", "Độ nhớt", null, "10W-30"), ("VOLUME", "Dung tích", "L", "0.8")]),
            ("PT-000003", "Nhớt hộp số xe ga 120ml", 2, 0, "Tuýp", 32_000, 38_000, 50_000, 7, 8, 0, [("VOLUME", "Dung tích", "L", "0.12")]),
            ("PT-000004", "Bugi NGK C6HSA cho Wave/Dream", 1, 1, "Cái", 42_000, 50_000, 65_000, 16, 6, 0, [("PLUG_CODE", "Mã bugi", null, "C6HSA")]),
            ("PT-000005", "Bugi NGK CPR6EA-9 cho xe tay ga Honda", 1, 1, "Cái", 68_000, 78_000, 95_000, 4, 5, 0, [("PLUG_CODE", "Mã bugi", null, "CPR6EA-9")]),
            ("PT-000006", "Má phanh trước Honda Air Blade/Vision", 2, 2, "Bộ", 105_000, 125_000, 165_000, 6, 4, 0, [("POSITION", "Vị trí", null, "Trước")]),
            ("PT-000007", "Guốc phanh sau Wave Alpha", 2, 2, "Bộ", 82_000, 95_000, 135_000, 3, 4, 0, [("POSITION", "Vị trí", null, "Sau")]),
            ("PT-000008", "Dây curoa Honda Air Blade 125", 2, 3, "Sợi", 310_000, 350_000, 430_000, 5, 3, 0, [("COMPATIBILITY", "Dòng xe", null, "Air Blade 125")]),
            ("PT-000009", "Bộ nhông sên dĩa D.I.D Wave Alpha", 6, 3, "Bộ", 345_000, 390_000, 490_000, 2, 3, 0, [("COMPATIBILITY", "Dòng xe", null, "Wave Alpha 110")]),
            ("PT-000010", "Lốp IRC 70/90-17 NF63B", 4, 4, "Cái", 285_000, 320_000, 390_000, 6, 4, 2, [("SIZE", "Kích thước", null, "70/90-17"), ("TUBELESS", "Không săm", null, "false")]),
            ("PT-000011", "Lốp IRC 90/90-14 NR77U", 4, 4, "Cái", 405_000, 455_000, 540_000, 3, 4, 2, [("SIZE", "Kích thước", null, "90/90-14"), ("TUBELESS", "Không săm", null, "true")]),
            ("PT-000012", "Ắc quy GS GTZ6V 12V-5Ah", 5, 5, "Bình", 410_000, 465_000, 560_000, 5, 3, 3, [("VOLTAGE", "Điện áp", "V", "12")]),
            ("PT-000013", "Bóng đèn pha HS1 12V 35/35W", 3, 5, "Cái", 48_000, 58_000, 75_000, 10, 5, 0, [("VOLTAGE", "Điện áp", "V", "12")]),
            ("PT-000014", "Lọc gió Honda Vision 110", 2, 6, "Cái", 105_000, 120_000, 155_000, 4, 5, 0, [("COMPATIBILITY", "Dòng xe", null, "Vision 110")]),
            ("PT-000015", "Dung dịch vệ sinh kim phun 100ml", 0, 0, "Chai", 72_000, 85_000, 120_000, 9, 4, 1, [("VOLUME", "Dung tích", "L", "0.1")])
        };
        var replacementCycles = new (int? Km, int? Months)[]
        {
            (2_000, 6), (2_000, 6), (6_000, 12), (8_000, 18), (8_000, 18),
            (12_000, 24), (12_000, 24), (24_000, 36), (15_000, 24), (20_000, 36),
            (20_000, 36), (null, 24), (null, null), (12_000, 18), (null, null)
        };
        for (var index = 0; index < prices.Length; index++)
        {
            var p = prices[index];
            var cycle = replacementCycles[index];
            d.Parts.Add(new Part { Code = p.Code, Barcode = $"893000{p.Code[^6..]}", Name = p.Name, PartBrandId = d.PartBrands[p.Brand].Id, PartCategoryId = d.PartCategories[p.Cat].Id, SupplierIds = [d.Suppliers[p.Supplier].Id], Unit = p.Unit, ImportPrice = p.Import, StockPrice = p.Stock, SalePrice = p.Sale, QuantityOnHand = p.Qty, MinQuantity = p.Min, ReplacementIntervalKm = cycle.Km, ReplacementIntervalMonths = cycle.Months, Notes = "Chu kỳ thay là dữ liệu tham khảo; cần điều chỉnh theo khuyến cáo của nhà sản xuất và điều kiện sử dụng.", Specifications = p.Specs.Select(x => new PartSpecificationValue { Code = x.Item1, Name = x.Item2, Unit = x.Item3, Value = x.Item4 }).ToList() });
        }

        BuildOperations(d, currentAdmin, managerUser, staffUser, now, today);
        return d;
    }

    private static void BuildOperations(
        DemoDataSet d,
        AppUser admin,
        AppUser manager,
        AppUser staff,
        DateTime now,
        DateTime today)
    {
        d.CashCategories.AddRange([
            new CashCategory { Code = "THU_HOA_DON", Name = "Thu tiền hóa đơn", Scope = CashCategoryScope.Income, Description = "Khoản thu từ khách thanh toán hóa đơn." },
            new CashCategory { Code = "THU_KHAC", Name = "Thu khác", Scope = CashCategoryScope.Income },
            new CashCategory { Code = "NHAP_PHU_TUNG", Name = "Nhập phụ tùng", Scope = CashCategoryScope.Expense },
            new CashCategory { Code = "DIEN_NUOC", Name = "Điện nước", Scope = CashCategoryScope.Expense },
            new CashCategory { Code = "LUONG", Name = "Lương nhân viên", Scope = CashCategoryScope.Expense },
            new CashCategory { Code = "MAT_BANG", Name = "Thuê mặt bằng", Scope = CashCategoryScope.Expense },
            new CashCategory { Code = "MARKETING", Name = "Marketing", Scope = CashCategoryScope.Expense },
            new CashCategory { Code = "DIEU_CHINH", Name = "Điều chỉnh nội bộ", Scope = CashCategoryScope.Both, Description = "Danh mục minh họa có thể dùng cho cả khoản thu và khoản chi." },
            new CashCategory { Code = "CHI_KHAC", Name = "Chi khác", Scope = CashCategoryScope.Expense, IsActive = false, Description = "Danh mục tạm khóa để minh họa trạng thái." }
        ]);

        d.Coupons.AddRange([
            new Coupon { Code = "CHAOHE20", Name = "Chào hè - giảm 20.000đ", Audience = CouponAudience.All, DiscountType = DiscountType.Amount, DiscountValue = 20_000, UsageLimit = 200, UsedCount = 1, StartAt = today.AddDays(-30), EndAt = today.AddDays(30), Description = "Áp dụng cho mọi khách hàng." },
            new Coupon { Code = "BAODUONG10", Name = "Giảm 10% đơn bảo dưỡng", Audience = CouponAudience.MinimumOrder, MinimumOrderAmount = 500_000, DiscountType = DiscountType.Percentage, DiscountValue = 10, UsageLimit = 100, UsedCount = 4, StartAt = today.AddDays(-15), EndAt = today.AddDays(45), Description = "Đơn từ 500.000đ, giảm 10%." },
            new Coupon { Code = "VIP100", Name = "Tri ân khách thân thiết", Audience = CouponAudience.SpecificCustomers, CustomerIds = [d.Customers[2].Id, d.Customers[4].Id], DiscountType = DiscountType.Amount, DiscountValue = 100_000, UsageLimit = 20, UsedCount = 1, StartAt = today.AddDays(-10), EndAt = today.AddDays(20) },
            new Coupon { Code = "HETHAN50", Name = "Chương trình tháng trước", Audience = CouponAudience.All, DiscountType = DiscountType.Amount, DiscountValue = 50_000, UsageLimit = 50, UsedCount = 18, StartAt = today.AddDays(-60), EndAt = today.AddDays(-30), IsActive = false, Description = "Coupon đã hết hạn." },
            new Coupon { Code = "HETLUOT15", Name = "Ưu đãi giới hạn", Audience = CouponAudience.All, DiscountType = DiscountType.Percentage, DiscountValue = 15, UsageLimit = 10, UsedCount = 10, StartAt = today.AddDays(-7), EndAt = today.AddDays(7), Description = "Coupon còn thời hạn nhưng đã hết lượt." }
        ]);

        d.LoyaltyTiers.AddRange([
            new LoyaltyTier { Code = "MEMBER", Name = "Thành viên", Rank = 1, MinEligibleSpend = 0, MinEarnedPoints = 0, EarnRate = 1, RedemptionValue = 1_000, Benefits = ["Tích điểm cơ bản", "Nhắc lịch bảo dưỡng"], Description = "Hạng mặc định." },
            new LoyaltyTier { Code = "SILVER", Name = "Bạc", Rank = 2, MinEligibleSpend = 10_000_000, MinEarnedPoints = 1_000, EarnRate = 1.1m, RedemptionValue = 1_000, Benefits = ["Tích điểm x1,1", "Ưu tiên đặt lịch"] },
            new LoyaltyTier { Code = "GOLD", Name = "Vàng", Rank = 3, MinEligibleSpend = 30_000_000, MinEarnedPoints = 3_000, EarnRate = 1.25m, RedemptionValue = 1_000, Benefits = ["Tích điểm x1,25", "Kiểm tra tổng quát miễn phí", "Ưu tiên cứu hộ"] },
            new LoyaltyTier { Code = "PLATINUM", Name = "Bạch kim", Rank = 4, MinEligibleSpend = 60_000_000, MinEarnedPoints = 6_000, EarnRate = 1.5m, RedemptionValue = 1_000, Benefits = ["Tích điểm x1,5", "Giảm công bảo dưỡng", "Đường dây hỗ trợ riêng"], IsActive = false, Description = "Hạng tạm ngừng tuyển mới để minh họa trạng thái." }
        ]);
        d.LoyaltyRules.AddRange([
            Stamp(new LoyaltyRule { Name = "Chính sách tích điểm hiện hành", SpendPerPoint = 10_000, RedemptionValue = 1_000, MinimumRedemptionPoints = 10, MaximumRedemptionRate = 0.5m, PointExpiryDays = 365, EffectiveFrom = today.AddMonths(-6), IsActive = true }, today.AddMonths(-6)),
            Stamp(new LoyaltyRule { Name = "Chính sách cũ", SpendPerPoint = 20_000, RedemptionValue = 1_000, MinimumRedemptionPoints = 20, MaximumRedemptionRate = 0.3m, PointExpiryDays = 180, EffectiveFrom = today.AddYears(-1), EffectiveTo = today.AddMonths(-6).AddSeconds(-1), IsActive = false }, today.AddYears(-1))
        ]);

        var techMachine = d.Employees[2]; var techElectric = d.Employees[3];
        var orders = new List<RepairOrder>
        {
            Order("RO", 1, d.Customers[0], d.Vehicles[0], today.AddDays(-20).AddHours(2), 16_500, "Thay nhớt định kỳ, kiểm tra phanh sau.", "Xe hoạt động bình thường, vỏ trầy nhẹ bên phải.", RepairPriority.Normal, RepairOrderStatus.Delivered, staff.Id, d.Employees[1].Id,
                [Item(d.ServiceCategories[0], "Thay nhớt động cơ", 1, 50_000, techMachine.Id, WorkStatus.Completed), Item(d.Parts[1], "Nhớt động cơ Honda 4T SL 10W-30 0,8L", 1, 120_000, techMachine.Id, WorkStatus.Completed, true)],
                "Nhớt cũ sẫm màu; phanh sau còn sử dụng tốt.", History(admin.Id, today.AddDays(-20).AddHours(2), RepairOrderStatus.Received, RepairOrderStatus.Inspecting, RepairOrderStatus.Repairing, RepairOrderStatus.Completed, RepairOrderStatus.Delivered)),
            Order("RO", 2, d.Customers[1], d.Vehicles[2], today.AddHours(2), 12_680, "Xe rung khi tăng ga, đề nghị kiểm tra bộ nồi.", "Dàn áo nguyên vẹn, lốp trước còn 60%.", RepairPriority.High, RepairOrderStatus.Completed, staff.Id, d.Employees[1].Id,
                [Item(d.ServiceCategories[4], "Vệ sinh bộ truyền động CVT", 1, 220_000, techMachine.Id, WorkStatus.Completed, false, DiscountType.Percentage, 10), Item(d.Parts[7], "Dây curoa Honda Air Blade 125", 1, 430_000, techMachine.Id, WorkStatus.Completed, true), Item(d.Parts[2], "Nhớt hộp số xe ga 120ml", 1, 50_000, techMachine.Id, WorkStatus.Completed, true)],
                "Dây curoa nứt chân răng, đã thay mới và vệ sinh bộ nồi.", History(admin.Id, today.AddHours(2), RepairOrderStatus.Received, RepairOrderStatus.Inspecting, RepairOrderStatus.AwaitingApproval, RepairOrderStatus.Repairing, RepairOrderStatus.Completed)),
            Order("RO", 3, d.Customers[2], d.Vehicles[3], today.AddHours(3), 5_420, "Xe khó đề buổi sáng, đèn báo yếu điện.", "Không trầy xước đáng kể.", RepairPriority.Normal, RepairOrderStatus.Completed, staff.Id, d.Employees[1].Id,
                [Item(d.ServiceCategories[5], "Kiểm tra và sửa điện", 1, 150_000, techElectric.Id, WorkStatus.Completed), Item(d.Parts[11], "Ắc quy GS GTZ6V 12V-5Ah", 1, 560_000, techElectric.Id, WorkStatus.Completed, true)],
                "Ắc quy không còn khả năng giữ điện, hệ thống sạc bình thường.", History(admin.Id, today.AddHours(3), RepairOrderStatus.Received, RepairOrderStatus.Inspecting, RepairOrderStatus.Repairing, RepairOrderStatus.Completed)),
            Order("RO", 4, d.Customers[3], d.Vehicles[4], now.AddHours(-3), 23_890, "Máy hụt ga, hao xăng hơn bình thường.", "Trầy yếm trái, khách đã xác nhận từ trước.", RepairPriority.High, RepairOrderStatus.Repairing, staff.Id, d.Employees[1].Id,
                [Item(d.ServiceCategories[2], "Vệ sinh kim phun", 1, 180_000, techMachine.Id, WorkStatus.InProgress), Item(d.Parts[14], "Dung dịch vệ sinh kim phun 100ml", 1, 120_000, techMachine.Id, WorkStatus.Completed, true)],
                "Áp suất bơm xăng đạt; kim phun bám cặn.", History(admin.Id, now.AddHours(-3), RepairOrderStatus.Received, RepairOrderStatus.Inspecting, RepairOrderStatus.Repairing)),
            Order("RO", 5, d.Customers[4], d.Vehicles[5], today.AddDays(-1).AddHours(3), 9_100, "Có tiếng rít ở lốc nồi khi chạy chậm.", "Xe sạch, có móp nhỏ chắn bùn trước.", RepairPriority.Urgent, RepairOrderStatus.AwaitingParts, staff.Id, d.Employees[1].Id,
                [Item(d.ServiceCategories[4], "Vệ sinh bộ truyền động CVT", 1, 220_000, techMachine.Id, WorkStatus.InProgress), Item(d.Parts[7], "Dây curoa Honda Air Blade 125 (chờ xác nhận đúng mã)", 1, 430_000, techMachine.Id, WorkStatus.Pending)],
                "Chờ đối chiếu mã dây curoa theo số khung.", History(admin.Id, today.AddDays(-1).AddHours(3), RepairOrderStatus.Received, RepairOrderStatus.Inspecting, RepairOrderStatus.Repairing, RepairOrderStatus.AwaitingParts)),
            Order("RO", 6, d.Customers[5], d.Vehicles[6], now.AddHours(-2), 35_600, "Sên kêu và giật khi sang số.", "Dàn áo cũ theo tuổi xe, đủ gương.", RepairPriority.Normal, RepairOrderStatus.AwaitingApproval, staff.Id, d.Employees[1].Id,
                [Item(d.ServiceCategories[1], "Bảo dưỡng định kỳ", 1, 180_000, techMachine.Id, WorkStatus.Pending), Item(d.Parts[8], "Bộ nhông sên dĩa D.I.D Wave Alpha", 1, 490_000, techMachine.Id, WorkStatus.Pending)],
                "Nhông sên dĩa mòn; đã gửi báo giá chờ khách duyệt.", History(admin.Id, now.AddHours(-2), RepairOrderStatus.Received, RepairOrderStatus.Inspecting, RepairOrderStatus.AwaitingApproval)),
            Order("RO", 7, d.Customers[6], d.Vehicles[7], now.AddHours(-1), 14_750, "Kiểm tra xe trước chuyến đi xa.", "Ngoại thất tốt, lốp sau mòn gần vạch chỉ thị.", RepairPriority.Normal, RepairOrderStatus.Inspecting, staff.Id, d.Employees[1].Id,
                [Item(d.ServiceCategories[9], "Kiểm tra tổng quát", 1, 80_000, techMachine.Id, WorkStatus.InProgress)],
                "Đang kiểm tra hệ thống phanh và lốp.", History(admin.Id, now.AddHours(-1), RepairOrderStatus.Received, RepairOrderStatus.Inspecting)),
            Order("RO", 8, d.Customers[7], d.Vehicles[8], now.AddMinutes(-30), 3_180, "Lốp sau xuống hơi nhanh.", "Xe mới, không ghi nhận trầy xước.", RepairPriority.Low, RepairOrderStatus.Received, staff.Id, d.Employees[1].Id,
                [Item(d.ServiceCategories[6], "Vá lốp không săm", 1, 40_000, techMachine.Id, WorkStatus.Pending)],
                null, History(admin.Id, now.AddMinutes(-30), RepairOrderStatus.Received)),
            Order("RO", 9, d.Customers[8], d.Vehicles[9], today.AddDays(-2).AddHours(4), 19_670, "Xe không khởi động sau khi để qua đêm.", "Dàn áo bình thường.", RepairPriority.Normal, RepairOrderStatus.Cancelled, staff.Id, d.Employees[1].Id,
                [Item(d.ServiceCategories[5], "Kiểm tra và sửa điện", 1, 150_000, techElectric.Id, WorkStatus.Cancelled)],
                "Khách xin hủy vì đã tự thay bình tại nhà.", History(admin.Id, today.AddDays(-2).AddHours(4), RepairOrderStatus.Received, RepairOrderStatus.Inspecting, RepairOrderStatus.Cancelled)),
            Order("RO", 10, d.Customers[8], d.Vehicles[10], today.AddDays(-10).AddHours(2), 42_200, "Thay lốp sau đã mòn.", "Xe cũ, nhiều vết xước nhỏ; đã chụp và xác nhận.", RepairPriority.Normal, RepairOrderStatus.Delivered, staff.Id, d.Employees[1].Id,
                [Item(d.ServiceCategories[7], "Thay lốp", 1, 60_000, techMachine.Id, WorkStatus.Completed), Item(d.Parts[10], "Lốp IRC 90/90-14 NR77U", 1, 540_000, techMachine.Id, WorkStatus.Completed, true)],
                "Đã cân chỉnh và bơm lốp 2,2 bar.", History(admin.Id, today.AddDays(-10).AddHours(2), RepairOrderStatus.Received, RepairOrderStatus.Inspecting, RepairOrderStatus.Repairing, RepairOrderStatus.Completed, RepairOrderStatus.Delivered)),
            Order("RO", 11, d.Customers[9], d.Vehicles[11], today.AddDays(-1).AddHours(5), 11_600, "Đèn pha chập chờn, kiểm tra thêm ắc quy.", "Xe có trầy chắn bùn trước.", RepairPriority.High, RepairOrderStatus.Repairing, staff.Id, d.Employees[1].Id,
                [Item(d.ServiceCategories[5], "Kiểm tra và sửa điện", 1, 150_000, techElectric.Id, WorkStatus.InProgress), Item(d.Parts[12], "Bóng đèn pha HS1 12V 35/35W", 1, 75_000, techElectric.Id, WorkStatus.Completed, true), Item(d.Parts[11], "Ắc quy GS GTZ6V 12V-5Ah (khách không duyệt)", 1, 560_000, techElectric.Id, WorkStatus.Cancelled)],
                "Chân giắc lỏng và bóng đèn sắp hỏng; ắc quy còn dùng được.", History(admin.Id, today.AddDays(-1).AddHours(5), RepairOrderStatus.Received, RepairOrderStatus.Inspecting, RepairOrderStatus.Repairing)),
            Order("RO", 12, d.Customers[0], d.Vehicles[1], today.AddDays(-45).AddHours(2), 6_500, "Thay nhớt và kiểm tra xe trước kỳ nghỉ.", "Ngoại thất tốt.", RepairPriority.Normal, RepairOrderStatus.Delivered, staff.Id, d.Employees[1].Id,
                [Item(d.ServiceCategories[0], "Thay nhớt động cơ", 1, 50_000, techMachine.Id, WorkStatus.Completed), Item(d.Parts[0], "Nhớt Motul Scooter MA 4T 10W-40 0,8L", 1, 165_000, techMachine.Id, WorkStatus.Completed, true)],
                "Không phát hiện bất thường.", History(admin.Id, today.AddDays(-45).AddHours(2), RepairOrderStatus.Received, RepairOrderStatus.Inspecting, RepairOrderStatus.Repairing, RepairOrderStatus.Completed, RepairOrderStatus.Delivered))
        };
        orders[0].DeliveredAt = orders[0].ReceivedAt.AddHours(3);
        orders[9].DeliveredAt = orders[9].ReceivedAt.AddHours(4);
        orders[11].DeliveredAt = orders[11].ReceivedAt.AddHours(2);
        d.RepairOrders.AddRange(orders);

        var invPaid = InvoiceFromOrder(1, orders[0], d.Coupons[0], InvoicePaymentStatus.Paid, 150_000, 0, 0, 0, admin.Id, today.AddDays(-20).AddHours(5));
        var invPartial = InvoiceFromOrder(2, orders[1], d.Coupons[1], InvoicePaymentStatus.PartiallyPaid, 300_000, 0, 0, 0, admin.Id, today.AddHours(7));
        var invUnpaid = InvoiceFromOrder(3, orders[2], d.Coupons[2], InvoicePaymentStatus.Unpaid, 0, 0, 8, 0, admin.Id, today.AddHours(7));
        var invRefunded = InvoiceFromOrder(4, orders[9], null, InvoicePaymentStatus.Refunded, 600_000, 0, 0, 0, admin.Id, today.AddDays(-10).AddHours(6));
        var invCancelled = InvoiceFromOrder(5, orders[10], null, InvoicePaymentStatus.Cancelled, 0, 0, 0, 0, admin.Id, today.AddDays(-1).AddHours(8));
        var invLoyalty = InvoiceFromOrder(6, orders[11], null, InvoicePaymentStatus.Paid, 205_000, 10_000, 0, 10, admin.Id, today.AddDays(-45).AddHours(4));
        d.Invoices.AddRange([invPaid, invPartial, invUnpaid, invRefunded, invCancelled, invLoyalty]);
        foreach (var invoice in d.Invoices)
        {
            var order = orders.First(x => x.Id == invoice.RepairOrderId);
            var customer = d.Customers.First(x => x.Id == invoice.CustomerId);
            invoice.CustomerName = customer.FullName;
            invoice.CustomerPhone = customer.Phone;
            invoice.CustomerAddress = customer.Address;
            invoice.CustomerTaxCode = customer.TaxCode;
            if (invoice.PaymentStatus != InvoicePaymentStatus.Cancelled) order.FinalTotal = invoice.TotalAmount;
        }

        var confirmedPurchase = Cash("PC", 1, CashTransactionType.Expense, d.CashCategories[2], today.AddDays(-25), 0, "Nhập lô dầu nhớt và vật tư tiêu hao", "BankTransfer", admin.Id, "PartsPurchase", "Confirmed");
        confirmedPurchase.SupplierId = d.Suppliers[1].Id; confirmedPurchase.ReferenceType = nameof(Supplier); confirmedPurchase.ReferenceId = d.Suppliers[1].Id; confirmedPurchase.ConfirmedAt = confirmedPurchase.TransactionDate.AddHours(2); confirmedPurchase.ConfirmedBy = manager.Id;
        confirmedPurchase.PurchaseItems = [Purchase(d.Parts[0], 24, 125_000), Purchase(d.Parts[14], 12, 72_000)]; confirmedPurchase.Amount = confirmedPurchase.PurchaseItems.Sum(x => x.LineTotal);
        var newPurchase = Cash("PC", 2, CashTransactionType.Expense, d.CashCategories[2], today, 0, "Đề nghị nhập bổ sung bugi và lọc gió đang dưới định mức", "BankTransfer", manager.Id, "PartsPurchase", "New");
        newPurchase.SupplierId = d.Suppliers[0].Id; newPurchase.ReferenceType = nameof(Supplier); newPurchase.ReferenceId = d.Suppliers[0].Id;
        newPurchase.PurchaseItems = [Purchase(d.Parts[4], 10, 68_000), Purchase(d.Parts[13], 10, 105_000)]; newPurchase.Amount = newPurchase.PurchaseItems.Sum(x => x.LineTotal);
        var cancelledPurchase = Cash("PC", 3, CashTransactionType.Expense, d.CashCategories[2], today.AddDays(-5), 1_200_000, "Phiếu nhập sai nhà cung cấp - đã hủy", "BankTransfer", admin.Id, "PartsPurchase", "Cancelled");
        cancelledPurchase.SupplierId = d.Suppliers[2].Id;
        d.CashTransactions.AddRange([
            confirmedPurchase, newPurchase, cancelledPurchase,
            CashForInvoice("RCT", 1, invPaid, 150_000, "Cash", admin.Id), CashForInvoice("RCT", 2, invPartial, 300_000, "BankTransfer", admin.Id), CashForInvoice("RCT", 3, invLoyalty, 205_000, "Card", admin.Id),
            CashForInvoice("PAY", 4, invRefunded, 600_000, "Refund", admin.Id, true),
            Cash("PC", 5, CashTransactionType.Expense, d.CashCategories[3], today.AddDays(-7), 3_850_000, "Thanh toán điện nước tháng này", "BankTransfer", admin.Id, "Other", "Confirmed"),
            Cash("PC", 6, CashTransactionType.Expense, d.CashCategories[4], today.AddDays(-3), 67_000_000, "Chi lương nhân viên", "BankTransfer", admin.Id, "Other", "Approved"),
            Cash("PT", 7, CashTransactionType.Income, d.CashCategories[1], today.AddDays(-2), 500_000, "Thu thanh lý kệ cũ", "Cash", admin.Id, "Other", "Confirmed")
        ]);

        BuildInventory(d, confirmedPurchase, admin, today);
        BuildLoyalty(d, invPaid, invLoyalty, admin, today);

        d.Notifications.AddRange([
            Stamp(new Notification { Role = SecurityRoles.Manager, Type = "LowStock", Title = "Phụ tùng dưới định mức", Message = $"{d.Parts[4].Name} chỉ còn {d.Parts[4].QuantityOnHand} cái.", ReferenceType = nameof(Part), ReferenceId = d.Parts[4].Id }, now.AddMinutes(-15)),
            Stamp(new Notification { UserId = manager.Id, Type = "UrgentRepair", Title = "Phiếu sửa chữa khẩn", Message = $"{orders[4].Code} đang chờ phụ tùng.", ReferenceType = nameof(RepairOrder), ReferenceId = orders[4].Id }, now.AddMinutes(-40)),
            Stamp(new Notification { Role = SecurityRoles.Manager, Type = "OutstandingInvoice", Title = "Hóa đơn còn công nợ", Message = $"{invPartial.Code} còn {invPartial.RemainingAmount:N0}đ.", ReferenceType = nameof(Invoice), ReferenceId = invPartial.Id, IsRead = true, ReadAt = now.AddHours(-1), ReadByUserIds = [manager.Id] }, now.AddHours(-5)),
            Stamp(new Notification { UserId = staff.Id, Type = "RepairAssigned", Title = "Khách vừa đến xưởng", Message = $"Phiếu {orders[7].Code} đang chờ tiếp nhận.", ReferenceType = nameof(RepairOrder), ReferenceId = orders[7].Id }, now.AddMinutes(-25))
        ]);

        d.AuditLogs.AddRange([
            Stamp(new AuditLog { UserId = staff.Id, Username = staff.Username, UserDisplayName = staff.FullName, Action = "CREATE", EntityType = "repair-orders", EntityId = orders[7].Id, RequestPath = "/api/v1/repair-orders", StatusCode = 201, IpAddress = "127.0.0.1", AfterData = "{\"demo\":true,\"description\":\"Tạo phiếu tiếp nhận mẫu\"}" }, now.AddMinutes(-30)),
            Stamp(new AuditLog { UserId = manager.Id, Username = manager.Username, UserDisplayName = manager.FullName, Action = "UPDATE", EntityType = "repair-orders", EntityId = orders[3].Id, RequestPath = $"/api/v1/repair-orders/{orders[3].Id}/status", StatusCode = 200, IpAddress = "127.0.0.1", BeforeData = "{\"status\":\"Inspecting\"}", AfterData = "{\"status\":\"Repairing\"}" }, now.AddHours(-2)),
            Stamp(new AuditLog { UserId = admin.Id, Username = admin.Username, UserDisplayName = admin.FullName, Action = "CONFIRM", EntityType = "cash-transactions", EntityId = confirmedPurchase.Id, RequestPath = $"/api/v1/cash-transactions/{confirmedPurchase.Id}/confirm", StatusCode = 200, IpAddress = "127.0.0.1", AfterData = "{\"status\":\"Confirmed\",\"demo\":true}" }, confirmedPurchase.ConfirmedAt!.Value),
            Stamp(new AuditLog { UserId = admin.Id, Username = admin.Username, UserDisplayName = admin.FullName, Action = "DELETE", EntityType = "coupons", EntityId = d.Coupons[3].Id, RequestPath = $"/api/v1/coupons/{d.Coupons[3].Id}", StatusCode = 200, IpAddress = "127.0.0.1", BeforeData = "{\"code\":\"HETHAN50\",\"isActive\":true}", AfterData = "{\"expired\":true}" }, today.AddDays(-30))
        ]);

        var dateKey = now.ToString("yyyyMM");
        var sequenceValues = new Dictionary<string, long>
        {
            ["employee"] = 7, ["vehicle-brand"] = 5, ["vehicle-model"] = 12, ["part-brand"] = 7,
            ["part-category"] = 7, ["service-category"] = 10, ["part"] = 15, ["supplier"] = 4,
            ["cash-category"] = 9, ["loyalty-tier"] = 4, ["coupon"] = 5,
            [$"customer:{dateKey}"] = 10, [$"repair-order:{dateKey}"] = 12, [$"invoice:{dateKey}"] = 6,
            [$"inventory:{dateKey}"] = d.InventoryTransactions.Count, [$"cash:{dateKey}"] = 10,
            [$"cash-expense:{dateKey}"] = 6, [$"cash-income:{dateKey}"] = 7
        };
        d.Sequences.AddRange(sequenceValues.Select(x => new Sequence { Name = x.Key, Value = x.Value }));
    }

    private static void BuildInventory(DemoDataSet d, CashTransaction receiptVoucher, AppUser admin, DateTime today)
    {
        var sequence = 0;
        string NextCode() => $"STK-{today:yyyyMM}-{++sequence:0000}";
        foreach (var part in d.Parts)
        {
            var issuedItems = d.RepairOrders
                .SelectMany(order => order.Items
                    .Where(item => item.PartId == part.Id && item.InventoryIssued && item.WorkStatus != WorkStatus.Cancelled)
                    .Select(item => new { Order = order, Item = item }))
                .ToList();
            var issuedQuantity = issuedItems.Sum(x => x.Item.Quantity);
            var supplierId = part.SupplierIds[0];
            d.InventoryTransactions.Add(Stamp(new InventoryTransaction
            {
                Code = NextCode(), PartId = part.Id, Type = InventoryTransactionType.Receipt,
                Quantity = part.QuantityOnHand + issuedQuantity, UnitCost = part.ImportPrice,
                ReferenceType = part.Id == d.Parts[0].Id || part.Id == d.Parts[14].Id ? nameof(CashTransaction) : "OpeningBalance",
                ReferenceId = part.Id == d.Parts[0].Id || part.Id == d.Parts[14].Id ? receiptVoucher.Id : null,
                SupplierId = supplierId, TransactionDate = today.AddDays(-30), PerformedBy = admin.Id,
                Notes = "Nhập tồn đầu kỳ cho bộ dữ liệu mẫu."
            }, today.AddDays(-30)));
            foreach (var issued in issuedItems)
            {
                d.InventoryTransactions.Add(Stamp(new InventoryTransaction
                {
                    Code = NextCode(), PartId = part.Id, Type = InventoryTransactionType.RepairIssue,
                    Quantity = issued.Item.Quantity, UnitCost = part.StockPrice,
                    ReferenceType = nameof(RepairOrder), ReferenceId = issued.Order.Id,
                    TransactionDate = issued.Order.ReceivedAt.AddHours(1), PerformedBy = admin.Id,
                    Notes = $"Xuất cho phiếu {issued.Order.Code}."
                }, issued.Order.ReceivedAt.AddHours(1)));
            }
            d.SupplierPartStocks.Add(new SupplierPartStock
            {
                SupplierId = supplierId, PartId = part.Id, QuantityOnHand = part.QuantityOnHand,
                LastUnitCost = part.ImportPrice, LastReceiptAt = today.AddDays(-30)
            });
        }

        d.InventoryTransactions.AddRange([
            Stamp(new InventoryTransaction { Code = NextCode(), PartId = d.Parts[0].Id, Type = InventoryTransactionType.RepairReturn, Quantity = 1, UnitCost = d.Parts[0].StockPrice, ReferenceType = nameof(RepairOrder), ReferenceId = d.RepairOrders[3].Id, TransactionDate = today.AddDays(-3), PerformedBy = admin.Id, Notes = "Hoàn lại một chai chưa mở do đổi phương án sửa chữa." }, today.AddDays(-3)),
            Stamp(new InventoryTransaction { Code = NextCode(), PartId = d.Parts[0].Id, Type = InventoryTransactionType.AdjustmentDecrease, Quantity = 1, UnitCost = d.Parts[0].StockPrice, ReferenceType = "Stocktake", TransactionDate = today.AddDays(-2), PerformedBy = admin.Id, Notes = "Điều chỉnh một chai hư tem khi kiểm kê." }, today.AddDays(-2)),
            Stamp(new InventoryTransaction { Code = NextCode(), PartId = d.Parts[4].Id, Type = InventoryTransactionType.AdjustmentIncrease, Quantity = 2, UnitCost = d.Parts[4].StockPrice, ReferenceType = "Stocktake", TransactionDate = today.AddDays(-2), PerformedBy = admin.Id, Notes = "Phát hiện hai bugi để nhầm vị trí kệ." }, today.AddDays(-2)),
            Stamp(new InventoryTransaction { Code = NextCode(), PartId = d.Parts[4].Id, Type = InventoryTransactionType.AdjustmentDecrease, Quantity = 2, UnitCost = d.Parts[4].StockPrice, ReferenceType = "QualityControl", TransactionDate = today.AddDays(-1), PerformedBy = admin.Id, Notes = "Loại khỏi tồn hai bugi lỗi bao bì." }, today.AddDays(-1))
        ]);
    }

    private static void BuildLoyalty(
        DemoDataSet d,
        Invoice paidInvoice,
        Invoice loyaltyInvoice,
        AppUser admin,
        DateTime today)
    {
        var accountSpecs = new (int Customer, string Tier, long Available, long Earned, long Redeemed, decimal Spend, DateTime Joined)[]
        {
            (0, "MEMBER", 130, 180, 10, 1_805_000, today.AddMonths(-18)),
            (1, "MEMBER", 60, 70, 0, 700_000, today.AddMonths(-12)),
            (2, "GOLD", 2_500, 3_000, 500, 32_000_000, today.AddYears(-3)),
            (3, "SILVER", 900, 1_200, 0, 12_000_000, today.AddYears(-2)),
            (4, "GOLD", 2_700, 3_600, 900, 36_000_000, today.AddYears(-4)),
            (5, "MEMBER", 0, 0, 0, 0, today.AddMonths(-2))
        };
        foreach (var spec in accountSpecs)
        {
            var customer = d.Customers[spec.Customer];
            var account = Stamp(new LoyaltyAccount
            {
                CustomerId = customer.Id, MemberCode = $"MEM-{spec.Joined:yyyyMM}-{spec.Customer + 1:0000}", CurrentTierCode = spec.Tier,
                AvailablePoints = spec.Available, LifetimeEarnedPoints = spec.Earned, LifetimeRedeemedPoints = spec.Redeemed,
                EligibleSpend = spec.Spend, JoinedAt = spec.Joined, TierUpdatedAt = spec.Tier == "MEMBER" ? null : today.AddMonths(-1), Status = spec.Customer == 5 ? "Suspended" : "Active"
            }, spec.Joined);
            d.LoyaltyAccounts.Add(account);
            customer.LoyaltyAccountId = account.Id;
            customer.LoyaltyTierCode = account.CurrentTierCode;
            customer.LoyaltyPointBalance = account.AvailablePoints;
        }

        var number = 0;
        LoyaltyTransaction Tx(LoyaltyAccount account, LoyaltyTransactionType type, long points, long before, long after, decimal money, string reason, DateTime at, string? invoiceId = null) => Stamp(new LoyaltyTransaction
        {
            TransactionCode = $"LTX-DEMO-{++number:0000}", IdempotencyKey = $"demo-loyalty-{number:0000}", LoyaltyAccountId = account.Id,
            CustomerId = account.CustomerId, InvoiceId = invoiceId, Type = type, Points = points, MonetaryValue = money,
            BalanceBefore = before, BalanceAfter = after, EarnedAt = at, EffectiveAt = at, ExpiresAt = type == LoyaltyTransactionType.Earn ? at.AddDays(365) : null,
            CreatedBy = admin.Id, Reason = reason
        }, at);

        var a0 = d.LoyaltyAccounts[0]; var earnHistory0 = Tx(a0, LoyaltyTransactionType.Earn, 145, 0, 145, 1_450_000, "Tổng hợp tích điểm lịch sử", today.AddDays(-90));
        var redeem0 = Tx(a0, LoyaltyTransactionType.Redeem, -10, 145, 135, 10_000, "Đổi 10 điểm khi thanh toán hóa đơn", today.AddDays(-45), loyaltyInvoice.Id);
        var earnLoyaltyInvoice0 = Tx(a0, LoyaltyTransactionType.Earn, 20, 135, 155, 205_000, "Tích điểm từ hóa đơn đã thanh toán", today.AddDays(-45).AddMinutes(15), loyaltyInvoice.Id);
        var earnPaidInvoice0 = Tx(a0, LoyaltyTransactionType.Earn, 15, 155, 170, 150_000, "Tích điểm từ hóa đơn đã thanh toán", today.AddDays(-20), paidInvoice.Id);
        var expire0 = Tx(a0, LoyaltyTransactionType.Expire, -40, 170, 130, 40_000, "Điểm của kỳ cũ hết hạn", today.AddDays(-5));
        var a1 = d.LoyaltyAccounts[1]; var earn1 = Tx(a1, LoyaltyTransactionType.Earn, 70, 0, 70, 700_000, "Tích điểm lịch sử", today.AddDays(-120));
        var expire1 = Tx(a1, LoyaltyTransactionType.Expire, -10, 70, 60, 10_000, "Điểm hết hạn", today.AddDays(-30));
        var adjust1 = Tx(a1, LoyaltyTransactionType.Adjust, 20, 60, 80, 20_000, "Bù điểm chăm sóc khách hàng", today.AddDays(-10));
        var reverse1 = Tx(a1, LoyaltyTransactionType.Reverse, -20, 80, 60, -20_000, "Đảo giao dịch điều chỉnh nhập nhầm", today.AddDays(-9)); reverse1.ReferenceTransactionId = adjust1.Id;
        var a2 = d.LoyaltyAccounts[2]; var earn2 = Tx(a2, LoyaltyTransactionType.Earn, 3_000, 0, 3_000, 30_000_000, "Tổng hợp tích điểm lịch sử", today.AddYears(-1));
        var redeem2 = Tx(a2, LoyaltyTransactionType.Redeem, -500, 3_000, 2_500, 500_000, "Đổi điểm cho lần bảo dưỡng trước", today.AddMonths(-2));
        var a3 = d.LoyaltyAccounts[3]; var earn3 = Tx(a3, LoyaltyTransactionType.Earn, 1_200, 0, 1_200, 12_000_000, "Tổng hợp tích điểm lịch sử", today.AddMonths(-8));
        var expire3 = Tx(a3, LoyaltyTransactionType.Expire, -300, 1_200, 900, 300_000, "Điểm của kỳ cũ hết hạn", today.AddDays(-15));
        var a4 = d.LoyaltyAccounts[4]; var earn4 = Tx(a4, LoyaltyTransactionType.Earn, 3_600, 0, 3_600, 36_000_000, "Tổng hợp tích điểm lịch sử", today.AddYears(-1));
        var redeem4 = Tx(a4, LoyaltyTransactionType.Redeem, -900, 3_600, 2_700, 900_000, "Đổi điểm cho lần bảo dưỡng trước", today.AddMonths(-1));
        d.LoyaltyTransactions.AddRange([earnHistory0, redeem0, earnLoyaltyInvoice0, earnPaidInvoice0, expire0, earn1, expire1, adjust1, reverse1, earn2, redeem2, earn3, expire3, earn4, redeem4]);
    }

    private static RepairOrder Order(
        string prefix, int number, Customer customer, Vehicle vehicle, DateTime receivedAt, int odometer,
        string request, string condition, RepairPriority priority, RepairOrderStatus status,
        string createdBy, string advisorId, List<RepairOrderItem> items, string? diagnosis,
        List<RepairStatusHistory> history)
    {
        var billable = items.Where(x => x.WorkStatus != WorkStatus.Cancelled).ToList();
        foreach (var item in items.Where(x => x.WorkStatus == WorkStatus.Completed))
        {
            item.StartedAt = receivedAt.AddHours(1);
            item.CompletedAt = receivedAt.AddHours(2);
        }
        var estimated = billable.Sum(x => x.Quantity * x.UnitPrice);
        var final = billable.Sum(x => x.LineTotal);
        return Stamp(new RepairOrder
        {
            Code = $"{prefix}-{DateTime.UtcNow:yyyyMM}-{number:0000}", CustomerId = customer.Id, VehicleId = vehicle.Id,
            ReceivedAt = receivedAt, ExpectedDeliveryAt = receivedAt.AddDays(status == RepairOrderStatus.AwaitingParts ? -1 : 1),
            OdometerIn = odometer, FuelLevel = number % 3 == 0 ? "1/4" : number % 3 == 1 ? "1/2" : "3/4",
            VehicleCondition = condition, CustomerRequest = request, Diagnosis = diagnosis, InternalNotes = "Dữ liệu mẫu để minh họa quy trình.",
            Priority = priority, Status = status, EstimatedTotal = estimated, DiscountAmount = estimated - final, FinalTotal = final,
            CreatedBy = createdBy, ServiceAdvisorId = advisorId, Items = items, StatusHistory = history
        }, receivedAt);
    }

    private static RepairOrderItem Item(
        ServiceCategory service, string description, decimal quantity, decimal price, string employeeId,
        WorkStatus workStatus, bool inventoryIssued = false,
        DiscountType discountType = DiscountType.Amount, decimal discountValue = 0) =>
        ItemCore(RepairItemType.Service, service.Id, null, description, quantity, price, employeeId, workStatus, inventoryIssued, discountType, discountValue);

    private static RepairOrderItem Item(
        Part part, string description, decimal quantity, decimal price, string employeeId,
        WorkStatus workStatus, bool inventoryIssued = false,
        DiscountType discountType = DiscountType.Amount, decimal discountValue = 0) =>
        ItemCore(RepairItemType.Part, null, part.Id, description, quantity, price, employeeId, workStatus, inventoryIssued, discountType, discountValue);

    private static RepairOrderItem ItemCore(
        RepairItemType type, string? serviceId, string? partId, string description, decimal quantity, decimal price,
        string employeeId, WorkStatus status, bool issued, DiscountType discountType, decimal discountValue)
    {
        var gross = quantity * price;
        var discount = discountType == DiscountType.Percentage
            ? decimal.Round(gross * discountValue / 100m, 0, MidpointRounding.AwayFromZero)
            : Math.Min(gross, discountValue);
        var now = DateTime.UtcNow;
        return new RepairOrderItem
        {
            ItemType = type, ServiceId = serviceId, PartId = partId, Description = description, Quantity = quantity, UnitPrice = price,
            DiscountType = discountType, DiscountValue = discountValue, DiscountAmount = discount, LineTotal = gross - discount,
            AssignedEmployeeId = employeeId, WorkStatus = status, InventoryIssued = issued,
            StartedAt = status is WorkStatus.InProgress or WorkStatus.Completed ? now.AddHours(-2) : null,
            CompletedAt = status == WorkStatus.Completed ? now.AddHours(-1) : null,
            TechnicianNotes = status == WorkStatus.Cancelled ? "Hạng mục không thực hiện theo yêu cầu khách." : null
        };
    }

    private static List<RepairStatusHistory> History(string userId, DateTime start, params RepairOrderStatus[] statuses)
    {
        var result = new List<RepairStatusHistory>();
        RepairOrderStatus? previous = null;
        for (var i = 0; i < statuses.Length; i++)
        {
            result.Add(new RepairStatusHistory { FromStatus = previous, ToStatus = statuses[i], ChangedBy = userId, ChangedAt = start.AddMinutes(i * 35), Note = i == 0 ? "Tiếp nhận xe tại quầy." : "Cập nhật trạng thái nghiệp vụ mẫu." });
            previous = statuses[i];
        }
        return result;
    }

    private static Invoice InvoiceFromOrder(
        int number, RepairOrder order, Coupon? coupon, InvoicePaymentStatus status, decimal paidAmount,
        decimal loyaltyDiscount, decimal taxRate, long loyaltyPoints, string userId, DateTime issueDate)
    {
        var orderItems = order.Items.Where(x => x.WorkStatus != WorkStatus.Cancelled).ToList();
        var items = orderItems.Select(x => new InvoiceItem
        {
            ItemType = x.ItemType, ReferenceId = x.ServiceId ?? x.PartId, Description = x.Description, Quantity = x.Quantity,
            UnitPrice = x.UnitPrice, DiscountType = x.DiscountType, DiscountValue = x.DiscountValue,
            DiscountAmount = x.DiscountAmount, LineTotal = x.LineTotal, TaxRate = taxRate
        }).ToList();
        var subtotal = items.Sum(x => x.Quantity * x.UnitPrice);
        var itemDiscount = items.Sum(x => x.DiscountAmount);
        var afterItems = subtotal - itemDiscount;
        var couponDiscount = coupon is null ? 0 : coupon.DiscountType == DiscountType.Percentage
            ? decimal.Round(afterItems * coupon.DiscountValue / 100m, 0, MidpointRounding.AwayFromZero)
            : Math.Min(afterItems, coupon.DiscountValue);
        var beforeTax = Math.Max(0, afterItems - couponDiscount);
        var taxAmount = decimal.Round(beforeTax * taxRate / 100m, 0, MidpointRounding.AwayFromZero);
        var total = beforeTax + taxAmount;
        var remaining = status is InvoicePaymentStatus.Cancelled or InvoicePaymentStatus.Refunded
            ? 0
            : Math.Max(0, total - paidAmount - loyaltyDiscount);
        var invoice = Stamp(new Invoice
        {
            Code = $"INV-{DateTime.UtcNow:yyyyMM}-{number:0000}", RepairOrderId = order.Id, CustomerId = order.CustomerId, IssueDate = issueDate,
            Subtotal = subtotal, ItemDiscountAmount = itemDiscount, DiscountType = DiscountType.Amount, DiscountValue = 0,
            CouponId = coupon?.Id, CouponCode = coupon?.Code, CouponDiscountAmount = couponDiscount,
            CouponUsageReturned = status == InvoicePaymentStatus.Cancelled && coupon is not null,
            DiscountAmount = itemDiscount + couponDiscount, TaxRate = taxRate, TaxAmount = taxAmount, TotalAmount = total,
            PaidAmount = paidAmount, RemainingAmount = remaining, LoyaltyPointsRedeemed = loyaltyPoints, LoyaltyDiscountAmount = loyaltyDiscount,
            PaymentStatus = status, CustomerName = string.Empty, CustomerPhone = string.Empty, CreatedBy = userId,
            Notes = status == InvoicePaymentStatus.Refunded ? "Đã hoàn tiền: khách đổi phương án sử dụng xe." : status == InvoicePaymentStatus.Cancelled ? "Đã hủy: cần điều chỉnh lại hạng mục sửa chữa." : "Hóa đơn dữ liệu mẫu.",
            Items = items, LoyaltyEarned = status is InvoicePaymentStatus.Paid or InvoicePaymentStatus.Refunded
        }, issueDate);
        if (paidAmount > 0)
            invoice.Payments.Add(new Payment { IdempotencyKey = $"demo-payment-{number:0000}", Amount = paidAmount, Method = number % 3 == 0 ? "BankTransfer" : number % 3 == 1 ? "Cash" : "Card", PaidAt = issueDate.AddMinutes(10), ReferenceCode = $"DEMO-{number:0000}", ReceivedBy = userId, Notes = "Thanh toán minh họa." });
        return invoice;
    }

    private static CashTransaction Cash(
        string prefix, int number, CashTransactionType type, CashCategory category, DateTime date,
        decimal amount, string description, string method, string userId, string purpose, string status) => Stamp(new CashTransaction
        {
            Code = $"{prefix}-{DateTime.UtcNow:yyyyMM}-{number:0000}", Type = type, CashCategoryId = category.Id, Category = category.Name,
            TransactionDate = date, Amount = amount, PaymentMethod = method, Description = description, CreatedBy = userId,
            Purpose = purpose, Status = status, ConfirmedAt = status is "Confirmed" or "Approved" ? date.AddHours(1) : null,
            ConfirmedBy = status is "Confirmed" or "Approved" ? userId : null
        }, date);

    private static CashTransaction CashForInvoice(
        string prefix, int number, Invoice invoice, decimal amount, string method, string userId, bool refund = false)
    {
        var category = new CashCategory { Id = ObjectId.GenerateNewId().ToString(), Name = refund ? "Hoàn tiền hóa đơn" : "Thu tiền hóa đơn" };
        var cash = Cash(prefix, number, refund ? CashTransactionType.Expense : CashTransactionType.Income, category, invoice.IssueDate.AddMinutes(10), amount, refund ? $"Hoàn tiền hóa đơn {invoice.Code}" : $"Thu tiền hóa đơn {invoice.Code}", method, userId, "Other", "Confirmed");
        cash.CashCategoryId = null;
        cash.ReferenceType = nameof(Invoice);
        cash.ReferenceId = invoice.Id;
        return cash;
    }

    private static PurchaseExpenseItem Purchase(Part part, decimal quantity, decimal unitCost)
    {
        var profit = decimal.Round((part.SalePrice - unitCost) / unitCost * 100m, 2);
        return new PurchaseExpenseItem { PartId = part.Id, PartCode = part.Code, PartName = part.Name, Quantity = quantity, UnitCost = unitCost, LineTotal = quantity * unitCost, SalePriceSnapshot = part.SalePrice, ProfitRate = profit, IsLowProfit = profit < 20 };
    }

    private static PartCategory Category(
        string code, string name, string description,
        (string Code, string Name, string? Unit, bool Required, PartSpecificationDataType DataType, string[] Options)[] definitions) => new()
        {
            Code = code, Name = name, Description = description,
            SpecificationDefinitions = definitions.Select(x => new PartSpecificationDefinition { Code = x.Code, Name = x.Name, Unit = x.Unit, IsRequired = x.Required, DataType = x.DataType, Options = x.Options.ToList() }).ToList()
        };

    private static ServiceCategory Service(string code, string name, decimal price, string description) =>
        new() { Code = code, Name = name, DefaultPrice = price, Description = description };

    private static AddressDetails Address(string line, string regionCode, string regionName, string areaCode, string areaName) =>
        new() { AddressLine = line, CountryCode = "VN", CountryName = "Việt Nam", RegionCode = regionCode, RegionName = regionName, AreaCode = areaCode, AreaName = areaName };

    private static T Stamp<T>(T document, DateTime at) where T : BaseDocument
    {
        document.CreatedAt = DateTime.SpecifyKind(at, DateTimeKind.Utc);
        document.UpdatedAt = document.CreatedAt;
        return document;
    }

    private static void Validate(DemoDataSet d)
    {
        EnsureUnique(d.Users, x => x.NormalizedUsername, "tên đăng nhập");
        EnsureUnique(d.Customers, x => x.Code, "mã khách hàng");
        EnsureUnique(d.Vehicles, x => x.NormalizedLicensePlate, "biển số xe");
        EnsureUnique(d.Parts, x => x.Code, "mã phụ tùng");
        EnsureUnique(d.RepairOrders, x => x.Code, "mã phiếu sửa chữa");
        EnsureUnique(d.Invoices, x => x.Code, "mã hóa đơn");
        EnsureUnique(d.Coupons, x => x.Code, "mã coupon");
        EnsureUnique(d.LoyaltyTransactions, x => x.IdempotencyKey, "idempotency loyalty");

        var employeeIds = d.Employees.Select(x => x.Id).ToHashSet();
        var brandIds = d.VehicleBrands.Select(x => x.Id).ToHashSet();
        var modelIds = d.VehicleModels.Select(x => x.Id).ToHashSet();
        var customerIds = d.Customers.Select(x => x.Id).ToHashSet();
        var vehicleIds = d.Vehicles.Select(x => x.Id).ToHashSet();
        var partBrandIds = d.PartBrands.Select(x => x.Id).ToHashSet();
        var categoryIds = d.PartCategories.Select(x => x.Id).ToHashSet();
        var serviceIds = d.ServiceCategories.Select(x => x.Id).ToHashSet();
        var supplierIds = d.Suppliers.Select(x => x.Id).ToHashSet();
        var partIds = d.Parts.Select(x => x.Id).ToHashSet();
        var orderIds = d.RepairOrders.Select(x => x.Id).ToHashSet();
        var invoiceIds = d.Invoices.Select(x => x.Id).ToHashSet();
        var accountIds = d.LoyaltyAccounts.Select(x => x.Id).ToHashSet();
        var cashCategoryIds = d.CashCategories.Select(x => x.Id).ToHashSet();

        Require(d.Users.All(x => x.EmployeeId is null || employeeIds.Contains(x.EmployeeId)), "Tài khoản tham chiếu nhân viên không tồn tại.");
        Require(d.VehicleModels.All(x => brandIds.Contains(x.BrandId)), "Dòng xe tham chiếu hãng xe không tồn tại.");
        Require(d.Vehicles.All(x => customerIds.Contains(x.CustomerId) && modelIds.Contains(x.VehicleModelId)), "Xe tham chiếu khách hàng/dòng xe không tồn tại.");
        Require(d.Parts.All(x => partBrandIds.Contains(x.PartBrandId) && categoryIds.Contains(x.PartCategoryId) && x.SupplierIds.All(supplierIds.Contains)), "Phụ tùng có tham chiếu danh mục không tồn tại.");
        Require(d.SupplierPartStocks.All(x => supplierIds.Contains(x.SupplierId) && partIds.Contains(x.PartId)), "Tồn theo nhà cung cấp có tham chiếu không tồn tại.");
        Require(d.InventoryTransactions.All(x => partIds.Contains(x.PartId) && (x.SupplierId is null || supplierIds.Contains(x.SupplierId))), "Giao dịch kho có tham chiếu không tồn tại.");
        Require(d.RepairOrders.All(x => customerIds.Contains(x.CustomerId) && vehicleIds.Contains(x.VehicleId)
            && x.Items.All(item => item.AssignedEmployeeId is null || employeeIds.Contains(item.AssignedEmployeeId))
            && x.Items.All(item => item.ServiceId is null || serviceIds.Contains(item.ServiceId))
            && x.Items.All(item => item.PartId is null || partIds.Contains(item.PartId))), "Phiếu sửa chữa có tham chiếu không tồn tại.");
        Require(d.Invoices.All(x => orderIds.Contains(x.RepairOrderId) && customerIds.Contains(x.CustomerId)), "Hóa đơn có tham chiếu không tồn tại.");
        Require(d.CashTransactions.All(x => x.CashCategoryId is null || cashCategoryIds.Contains(x.CashCategoryId))
            && d.CashTransactions.SelectMany(x => x.PurchaseItems).All(x => partIds.Contains(x.PartId)), "Thu chi có tham chiếu không tồn tại.");
        Require(d.LoyaltyAccounts.All(x => customerIds.Contains(x.CustomerId)), "Tài khoản loyalty có khách hàng không tồn tại.");
        Require(d.LoyaltyTransactions.All(x => accountIds.Contains(x.LoyaltyAccountId) && customerIds.Contains(x.CustomerId) && (x.InvoiceId is null || invoiceIds.Contains(x.InvoiceId))), "Giao dịch loyalty có tham chiếu không tồn tại.");

        Require(Enum.GetValues<RepairOrderStatus>().All(status => d.RepairOrders.Any(x => x.Status == status)), "Bộ mẫu chưa bao phủ đủ trạng thái phiếu sửa chữa.");
        Require(Enum.GetValues<RepairPriority>().All(priority => d.RepairOrders.Any(x => x.Priority == priority)), "Bộ mẫu chưa bao phủ đủ mức ưu tiên sửa chữa.");
        Require(Enum.GetValues<WorkStatus>().All(status => d.RepairOrders.SelectMany(x => x.Items).Any(item => item.WorkStatus == status)), "Bộ mẫu chưa bao phủ đủ trạng thái hạng mục.");
        Require(Enum.GetValues<InvoicePaymentStatus>().All(status => d.Invoices.Any(x => x.PaymentStatus == status)), "Bộ mẫu chưa bao phủ đủ trạng thái hóa đơn.");
        Require(Enum.GetValues<InventoryTransactionType>().All(type => d.InventoryTransactions.Any(x => x.Type == type)), "Bộ mẫu chưa bao phủ đủ loại giao dịch kho.");
        Require(Enum.GetValues<LoyaltyTransactionType>().All(type => d.LoyaltyTransactions.Any(x => x.Type == type)), "Bộ mẫu chưa bao phủ đủ loại giao dịch loyalty.");
        Require(Enum.GetValues<CouponAudience>().All(type => d.Coupons.Any(x => x.Audience == type)), "Bộ mẫu chưa bao phủ đủ đối tượng coupon.");
        Require(Enum.GetValues<EmployeeStatus>().All(status => d.Employees.Any(x => x.Status == status)), "Bộ mẫu chưa bao phủ đủ trạng thái nhân viên.");
        Require(Enum.GetValues<CashCategoryScope>().All(scope => d.CashCategories.Any(x => x.Scope == scope)), "Bộ mẫu chưa bao phủ đủ phạm vi danh mục thu chi.");
        Require(new[] { "New", "Confirmed", "Approved", "Cancelled" }.All(status => d.CashTransactions.Any(x => x.Status == status)), "Bộ mẫu chưa bao phủ đủ trạng thái thu chi.");

        foreach (var part in d.Parts)
        {
            var balance = d.InventoryTransactions.Where(x => x.PartId == part.Id).Sum(x => x.Type switch
            {
                InventoryTransactionType.Receipt or InventoryTransactionType.RepairReturn or InventoryTransactionType.AdjustmentIncrease => x.Quantity,
                InventoryTransactionType.RepairIssue or InventoryTransactionType.AdjustmentDecrease => -x.Quantity,
                _ => 0
            });
            Require(balance == part.QuantityOnHand, $"Tồn kho không khớp cho phụ tùng {part.Code}: sổ {balance}, khai báo {part.QuantityOnHand}.");
        }

        foreach (var invoice in d.Invoices)
        {
            var calculated = invoice.Subtotal - invoice.DiscountAmount + invoice.TaxAmount;
            Require(calculated == invoice.TotalAmount, $"Tổng tiền hóa đơn {invoice.Code} không khớp.");
            if (invoice.PaymentStatus is not InvoicePaymentStatus.Cancelled and not InvoicePaymentStatus.Refunded)
                Require(invoice.TotalAmount == invoice.PaidAmount + invoice.LoyaltyDiscountAmount + invoice.RemainingAmount, $"Công nợ hóa đơn {invoice.Code} không khớp.");
        }

        foreach (var account in d.LoyaltyAccounts)
        {
            var customer = d.Customers.Single(x => x.Id == account.CustomerId);
            Require(customer.LoyaltyAccountId == account.Id && customer.LoyaltyPointBalance == account.AvailablePoints && customer.LoyaltyTierCode == account.CurrentTierCode,
                $"Số dư loyalty không đồng bộ cho khách {customer.Code}.");
        }
    }

    private static void EnsureUnique<T>(IEnumerable<T> values, Func<T, string> key, string label)
    {
        var duplicates = values.GroupBy(key, StringComparer.OrdinalIgnoreCase).Where(x => x.Count() > 1).Select(x => x.Key).ToList();
        Require(duplicates.Count == 0, $"Trùng {label}: {string.Join(", ", duplicates)}.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"Bộ dữ liệu mẫu không hợp lệ: {message}");
    }

    private sealed class DemoDataSet
    {
        public List<AppUser> Users { get; } = [];
        public List<Customer> Customers { get; } = [];
        public List<Employee> Employees { get; } = [];
        public List<VehicleBrand> VehicleBrands { get; } = [];
        public List<VehicleModel> VehicleModels { get; } = [];
        public List<Vehicle> Vehicles { get; } = [];
        public List<PartBrand> PartBrands { get; } = [];
        public List<Supplier> Suppliers { get; } = [];
        public List<PartCategory> PartCategories { get; } = [];
        public List<ServiceCategory> ServiceCategories { get; } = [];
        public List<SupplierPartStock> SupplierPartStocks { get; } = [];
        public List<Part> Parts { get; } = [];
        public List<InventoryTransaction> InventoryTransactions { get; } = [];
        public List<RepairOrder> RepairOrders { get; } = [];
        public List<Invoice> Invoices { get; } = [];
        public List<Coupon> Coupons { get; } = [];
        public List<CashTransaction> CashTransactions { get; } = [];
        public List<CashCategory> CashCategories { get; } = [];
        public List<LoyaltyTier> LoyaltyTiers { get; } = [];
        public List<LoyaltyRule> LoyaltyRules { get; } = [];
        public List<LoyaltyAccount> LoyaltyAccounts { get; } = [];
        public List<LoyaltyTransaction> LoyaltyTransactions { get; } = [];
        public List<Notification> Notifications { get; } = [];
        public List<AuditLog> AuditLogs { get; } = [];
        public List<Sequence> Sequences { get; } = [];

        public IReadOnlyDictionary<string, int> Counts() => new Dictionary<string, int>
        {
            ["Tài khoản"] = Users.Count, ["Nhân viên"] = Employees.Count, ["Khách hàng"] = Customers.Count,
            ["Xe máy"] = Vehicles.Count, ["Hãng và dòng xe"] = VehicleBrands.Count + VehicleModels.Count,
            ["Nhà cung cấp"] = Suppliers.Count, ["Phụ tùng"] = Parts.Count,
            ["Danh mục"] = PartCategories.Count + ServiceCategories.Count + CashCategories.Count + PartBrands.Count,
            ["Giao dịch kho"] = InventoryTransactions.Count, ["Phiếu sửa chữa"] = RepairOrders.Count,
            ["Hóa đơn"] = Invoices.Count, ["Thu chi"] = CashTransactions.Count, ["Coupon"] = Coupons.Count,
            ["Loyalty"] = LoyaltyTiers.Count + LoyaltyRules.Count + LoyaltyAccounts.Count + LoyaltyTransactions.Count,
            ["Thông báo và nhật ký"] = Notifications.Count + AuditLogs.Count
        };
    }
}
