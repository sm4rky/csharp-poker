using Microsoft.AspNetCore.Mvc;
using PokerAppBackend.Contracts;
using PokerAppBackend.Mappers;
using PokerAppBackend.Services;

namespace PokerAppBackend.Controllers;

[ApiController]
[Route("api/deck")]
public class DeckController(IDeckService deckService) : ControllerBase
{
    [HttpPost("start")]
    public ActionResult<StartDeckResponse> Start()
    {
        var id = deckService.StartNewDeck();
        return Ok(new StartDeckResponse { SessionId = id });
    }

    [HttpPost("{id:guid}/shuffle")]
    public IActionResult Shuffle(Guid id)
    {
        deckService.Shuffle(id);
        return NoContent();
    }

    [HttpGet("{id:guid}/remaining")]
    public IActionResult Remaining(Guid id)
    {
        return Ok(new { remaining = deckService.Remaining(id) });
    }

    [HttpPost("{id:guid}/draw/{n:int}")]
    public ActionResult<DrawManyResponse> Draw(Guid id, int n)
    {
        if (n <= 0) return BadRequest("n must be > 0");

        var cards = deckService.DrawMany(id, n).Select(x => x.ToCardDto()).ToList();
        return Ok(new DrawManyResponse
        {
            Cards = cards,
            Remaining = deckService.Remaining(id)
        });
    }

    [HttpPost("{id:guid}/burn")]
    public ActionResult<BurnResponse> Burn(Guid id)
    {
        var card = deckService.Burn(id);

        return Ok(new BurnResponse
        {
            Card = card.ToCardDto(),
            Remaining = deckService.Remaining(id)
        });
    }
}