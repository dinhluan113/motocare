using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MotoCare.Api.Infrastructure;
using MotoCare.Api.Services;

namespace MotoCare.Api.Controllers;

[ApiController]
[Route("api/v1/settings/demo-data")]
[Authorize(Roles = SecurityRoles.Administrators)]
public sealed class SettingsController(
    DemoDataService demoData,
    IOptions<DemoDataOptions> options) : ControllerBase
{
    private readonly DemoDataOptions _options = options.Value;

    [HttpGet]
    public IActionResult GetAvailability() => Ok(ApiEnvelope.Ok(new
    {
        enabled = _options.Enabled,
        confirmationPhrase = _options.Enabled ? _options.ConfirmationPhrase : null,
        preservesCurrentAdmin = true,
        scope = new[]
        {
            "Tài khoản và nhân viên", "Khách hàng và xe", "Danh mục, nhà cung cấp và kho",
            "Phiếu sửa chữa và mọi trạng thái", "Hóa đơn, thanh toán và thu chi",
            "Coupon, loyalty, thông báo, báo cáo và nhật ký"
        }
    }));

    [HttpPost("reset")]
    public async Task<IActionResult> Reset(
        ResetDemoDataRequest request,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return NotFound(ApiEnvelope.Fail("FEATURE_DISABLED", "Tính năng dữ liệu mẫu không được bật trên môi trường này."));

        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("Không xác định được tài khoản quản trị.");
        var result = await demoData.ResetAsync(adminId, request.Confirmation ?? string.Empty, cancellationToken);
        return Ok(ApiEnvelope.Ok(result, "Đã xóa dữ liệu cũ và tạo lại bộ dữ liệu mẫu hoàn chỉnh."));
    }
}

public sealed record ResetDemoDataRequest(string? Confirmation);
