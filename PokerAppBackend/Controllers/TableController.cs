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
    public IActionResult Create([FromQuery] int playerCount)
    {
        if (playerCount is < 2 or > 6)
            return BadRequest("seats must be 2..6");

        var code = tableService.CreateTable(playerCount);
        return Ok(new { tableCode = code });
    }
    
    [HttpGet("{code}/state")]
    public ActionResult<TableDto> State(string code, [FromQuery] int? playerSeat = null)
    {
        var table = tableService.Get(code);
        return Ok(table.ToTableDto(playerSeat));
    }
}