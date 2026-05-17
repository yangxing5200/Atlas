using Atlas.Core.Exceptions;
using Atlas.Models.DTOs;
using Atlas.Models.Tenant.Requests;
using Atlas.Models.Tenant.Responses;
using Atlas.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Sample.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public sealed class ProductController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductController(IProductService productService)
    {
        _productService = productService ?? throw new ArgumentNullException(nameof(productService));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> GetById([FromRoute] long id)
    {
        var product = await _productService.GetByIdAsync(id);
        return product == null
            ? NotFound(new { message = $"Product with ID {id} not found" })
            : Ok(product);
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProductDto>), StatusCodes.Status200OK)]
    public Task<PagedResult<ProductDto>> GetPaged(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10)
    {
        return _productService.PageQueryAsync(x => x.Id > 0, pageIndex, pageSize);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductDto>> Create([FromBody] CreateProductRequest request)
    {
        var created = await _productService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        [FromRoute] long id,
        [FromBody] UpdateProductRequest request)
    {
        try
        {
            await _productService.UpdateAsync(id, request);
            return NoContent();
        }
        catch (AtlasException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete([FromRoute] long id)
    {
        try
        {
            await _productService.RemoveAsync(id);
            return NoContent();
        }
        catch (AtlasException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("{id:long}/exists")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public Task<bool> Exists([FromRoute] long id, CancellationToken ct = default)
    {
        return _productService.ExistsAsync(p => p.Id == id, ct);
    }

    [HttpGet("count")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public Task<int> Count(CancellationToken ct = default)
    {
        return _productService.CountAsync(p => true, ct);
    }

    [HttpGet("search")]
    [ProducesResponseType(typeof(PagedResult<ProductDto>), StatusCodes.Status200OK)]
    public Task<PagedResult<ProductDto>> Search(
        [FromQuery] string? keyword,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        return string.IsNullOrWhiteSpace(keyword)
            ? _productService.PageQueryAsync(p => true, pageIndex, pageSize, ct)
            : _productService.PageQueryAsync(p => p.Name.Contains(keyword), pageIndex, pageSize, ct);
    }
}
