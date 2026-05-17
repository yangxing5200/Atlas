using Atlas.Core.Exceptions;
using Atlas.Infrastructure.Security;
using Atlas.Models.DTOs;
using Atlas.Models.Tenant.Responses;
using Atlas.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Sample.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.RequireTenantAdmin)]
public sealed class StoreController : ControllerBase
{
    private readonly IStoreService _storeService;

    public StoreController(IStoreService storeService)
    {
        _storeService = storeService ?? throw new ArgumentNullException(nameof(storeService));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(StoreDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StoreDto>> GetById(
        [FromRoute] long id,
        CancellationToken ct = default)
    {
        var store = await _storeService.GetByIdAsync(id, ct);
        return store == null
            ? NotFound(new { message = $"Store with ID {id} not found" })
            : Ok(store);
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<StoreDto>), StatusCodes.Status200OK)]
    public Task<PagedResult<StoreDto>> GetPaged(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        return _storeService.PageQueryAsync(s => true, pageIndex, pageSize, ct);
    }

    [HttpPost]
    [ProducesResponseType(typeof(StoreDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<StoreDto>> Create(
        [FromBody] StoreDto storeDto,
        CancellationToken ct = default)
    {
        var created = await _storeService.AddAsync(storeDto, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        [FromRoute] long id,
        [FromBody] StoreDto storeDto,
        CancellationToken ct = default)
    {
        var exists = await _storeService.ExistsAsync(s => s.Id == id, ct);
        if (!exists)
            return NotFound(new { message = $"Store with ID {id} not found" });

        await _storeService.UpdateAsync(id, storeDto, ct);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        [FromRoute] long id,
        CancellationToken ct = default)
    {
        try
        {
            await _storeService.RemoveAsync(id, ct);
            return NoContent();
        }
        catch (AtlasException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("{id:long}/exists")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public Task<bool> Exists(
        [FromRoute] long id,
        CancellationToken ct = default)
    {
        return _storeService.ExistsAsync(s => s.Id == id, ct);
    }

    [HttpGet("count")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public Task<int> Count(CancellationToken ct = default)
    {
        return _storeService.CountAsync(s => true, ct);
    }
}
