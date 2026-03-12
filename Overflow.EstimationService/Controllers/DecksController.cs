using Microsoft.AspNetCore.Mvc;
using Overflow.EstimationService.DTOs;
using Overflow.EstimationService.Models;

namespace Overflow.EstimationService.Controllers;

[ApiController]
[Route("estimation")]
public class DecksController : ControllerBase
{
    [HttpGet("decks")]
    public IActionResult GetDecks()
    {
        var decks = Decks.All.Values.Select(d => new DeckDefinitionResponse(d.Id, d.Name, d.Values));
        return Ok(decks);
    }
}