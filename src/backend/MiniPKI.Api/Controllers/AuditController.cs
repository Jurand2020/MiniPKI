using Microsoft.AspNetCore.Mvc;
using MiniPKI.Api.Models;
using MiniPKI.Core.Services;

namespace MiniPKI.Api.Controllers;

[ApiController]
[Route("api/audit")]
public class AuditController : ControllerBase
{
    private readonly IAuditService _audit;

    public AuditController(IAuditService audit)
    {
        _audit = audit;
    }

    [HttpGet]
    public async Task<ActionResult<List<AuditEntryResponse>>> List([FromQuery] int limit = 100)
    {
        var entries = await _audit.GetEntriesAsync(limit);
        return Ok(entries.Select(e => new AuditEntryResponse
        {
            Timestamp = e.Timestamp,
            Action = e.Action,
            Details = e.Details,
            Actor = e.Actor
        }));
    }
}
