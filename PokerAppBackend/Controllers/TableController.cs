using Microsoft.AspNetCore.Mvc;
using PokerAppBackend.Contracts;
using PokerAppBackend.Domain;
using PokerAppBackend.Mappers;
using PokerAppBackend.Services;

namespace PokerAppBackend.Controllers;

[ApiController]
[Route("api/table")]
public class TableController(ITableService tableService) : ControllerBase
{
    [HttpPost("create")]
    public IActionResult Create([FromQuery] int playerCount, [FromQuery] string name)
    {
        if (playerCount is < 2 or > 6)
            return BadRequest("seats must be 2..6");

        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("Invalid name");
        
        var code = tableService.CreateTable(playerCount, name);
        return Ok(new { tableCode = code });
    }
    
    [HttpPost("{code}/join")]
    public IActionResult Join(string code, [FromQuery] int seatIndex, [FromQuery] string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("Invalid name");
        
        tableService.JoinAsPlayer(code, seatIndex, name);
        return NoContent();
    }
    
    [HttpPost("{code}/start")]
    public IActionResult Start(string code)
    {
        tableService.StartHand(code);
        return NoContent();
    }

    [HttpPost("{code}/flop")]
    public IActionResult Flop(string code)
    {
        tableService.DealFlop(code);
        return NoContent();
    }

    [HttpPost("{code}/turn")]
    public IActionResult Turn(string code)
    {
        tableService.DealTurn(code);
        return NoContent();
    }

    [HttpPost("{code}/river")]
    public IActionResult River(string code)
    {
        tableService.DealRiver(code);
        return NoContent();
    }
    
    [HttpGet("{code}/state")]
    public ActionResult<TableDto> State(string code, [FromQuery] int? playerSeat = null)
    {
        var table = tableService.Get(code);
        return Ok(table.ToTableDto(playerSeat));
    }
}