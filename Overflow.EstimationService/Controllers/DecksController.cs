using CommandFlow;
using Microsoft.AspNetCore.Mvc;
using Overflow.EstimationService.Features.Rooms.Queries;

namespace Overflow.EstimationService.Controllers;

[ApiController]
[Route("estimation")]
public class DecksController(ISender sender) : ControllerBase
{
    [HttpGet("decks")]
    public async Task<IActionResult> GetDecks()
    {
        var decks = await sender.Send(new GetDecksQuery());
        return Ok(decks);
    }
}