using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MotoCare.Api.Infrastructure;
using MotoCare.Api.Services;

namespace MotoCare.Api.Controllers;

[ApiController]
[Route("api/v1/reports")]
[Authorize(Roles = SecurityRoles.Management + "," + SecurityRoles.Cashier)]
public sealed class ReportsController(
    ReportsService reports,
    ExcelExportService excel) : ControllerBase
{
    [HttpGet("revenue")]
    public async Task<IActionResult> Revenue(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string groupBy = "month",
        CancellationToken cancellationToken = default)
    {
        var range = Range(from, to);
        return Ok(ApiEnvelope.Ok(await reports.RevenueAsync(
            range.From,
            range.To,
            groupBy,
            cancellationToken)));
    }

    [HttpGet("top-parts")]
    public async Task<IActionResult> TopParts(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var range = Range(from, to);
        return Ok(ApiEnvelope.Ok(await reports.TopPartsAsync(
            range.From,
            range.To,
            limit,
            cancellationToken)));
    }

    [HttpGet("top-vehicles")]
    public async Task<IActionResult> TopVehicles(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var range = Range(from, to);
        return Ok(ApiEnvelope.Ok(await reports.TopVehiclesAsync(
            range.From,
            range.To,
            limit,
            cancellationToken)));
    }

    [HttpGet("loyal-customers")]
    public async Task<IActionResult> LoyalCustomers(
        [FromQuery] string? tier,
        [FromQuery] decimal? minimumSpend,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        return Ok(ApiEnvelope.Ok(await reports.LoyalCustomersAsync(
            tier,
            minimumSpend,
            limit,
            cancellationToken)));
    }

    [HttpGet("loyalty-transactions")]
    public async Task<IActionResult> LoyaltyTransactions(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var range = Range(from, to);
        return Ok(ApiEnvelope.Ok(await reports.LoyaltyTransactionsAsync(
            range.From,
            range.To,
            cancellationToken)));
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] string report,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var range = Range(from, to);
        var filename = $"motocare-{report}-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx";
        byte[] content = report.ToLowerInvariant() switch
        {
            "revenue" => excel.Export(
                "Doanh thu",
                await reports.RevenueAsync(range.From, range.To, "month", cancellationToken)),
            "top-parts" => excel.Export(
                "Phụ tùng bán chạy",
                await reports.TopPartsAsync(range.From, range.To, 500, cancellationToken)),
            "top-vehicles" => excel.Export(
                "Xe sửa nhiều",
                await reports.TopVehiclesAsync(range.From, range.To, 500, cancellationToken)),
            "loyal-customers" => excel.Export(
                "Khách hàng thân thiết",
                await reports.LoyalCustomersAsync(null, null, 1_000, cancellationToken)),
            "loyalty-transactions" => excel.Export(
                "Giao dịch loyalty",
                await reports.LoyaltyTransactionsAsync(range.From, range.To, cancellationToken)),
            _ => throw new InvalidOperationException("Loại báo cáo không được hỗ trợ.")
        };
        return File(
            content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            filename);
    }

    private static (DateTime From, DateTime To) Range(DateTime? from, DateTime? to)
    {
        var utcTo = (to ?? DateTime.UtcNow).ToUniversalTime();
        var utcFrom = (from ?? utcTo.AddMonths(-1)).ToUniversalTime();
        if (utcFrom > utcTo)
        {
            throw new InvalidOperationException("Ngày bắt đầu phải trước ngày kết thúc.");
        }

        return (utcFrom, utcTo);
    }
}
