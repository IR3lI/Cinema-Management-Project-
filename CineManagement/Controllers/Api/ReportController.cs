using CineManagement.DTOs;
using CineManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CineManagement.Controllers.Api;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = "Admin")]
public sealed class ReportController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("revenue")]
    [ProducesResponseType(typeof(IReadOnlyList<DailyRevenueDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetRevenue(
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _reportService.GetRevenueReportAsync(fromDate, toDate, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("top-movies")]
    [ProducesResponseType(typeof(IReadOnlyList<TopMovieDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTopMovies(
        [FromQuery] int top = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _reportService.GetTopMoviesAsync(top, cancellationToken));
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("showtimes/{showtimeId:int}/occupancy")]
    [ProducesResponseType(typeof(RoomOccupancyRateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRoomOccupancyRate(int showtimeId, CancellationToken cancellationToken)
    {
        var result = await _reportService.GetRoomOccupancyRateAsync(showtimeId, cancellationToken);
        return result is null
            ? NotFound(new { message = "Không tìm thấy suất chiếu." })
            : Ok(result);
    }
}
