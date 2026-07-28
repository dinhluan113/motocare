using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MotoCare.Api.Infrastructure;
using MotoCare.Api.Services;

namespace MotoCare.Api.Controllers;

[ApiController]
[Route("api/v1/locations")]
[Authorize]
public sealed class LocationsController(LocationService locations) : ControllerBase
{
    [HttpGet("countries")]
    public async Task<IActionResult> GetCountries(CancellationToken cancellationToken) =>
        Ok(ApiEnvelope.Ok(await locations.GetCountriesAsync(cancellationToken)));

    [HttpGet("countries/{countryCode}/regions")]
    public async Task<IActionResult> GetRegions(
        string countryCode,
        CancellationToken cancellationToken) =>
        Ok(ApiEnvelope.Ok(await locations.GetRegionsAsync(countryCode, cancellationToken)));

    [HttpGet("countries/{countryCode}/regions/{regionCode}/areas")]
    public async Task<IActionResult> GetAreas(
        string countryCode,
        string regionCode,
        CancellationToken cancellationToken) =>
        Ok(ApiEnvelope.Ok(await locations.GetAreasAsync(
            countryCode,
            regionCode,
            cancellationToken)));
}
