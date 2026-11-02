using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Booking.Api.Features.Demo;

[ApiController]
[Route("api/demo")]
[Authorize(Roles = "Admin")]
public class DemoController : ControllerBase
{
    private readonly DemoManager _demo;

    public DemoController(DemoManager demo)
    {
        _demo = demo;
    }

    [HttpPost("reset")]
    public async Task<IActionResult> Reset()
    {
        await _demo.ResetAsync();
        return NoContent();
    }
}
