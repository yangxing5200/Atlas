using Atlas.Core.Exceptions;
using Atlas.Exporting;
using Atlas.Infrastructure.Security;
using Atlas.Models.DTOs;
using Atlas.Models.Tenant.Requests;
using Atlas.Models.Tenant.Responses;
using Atlas.Sample.ECommerce;
using Atlas.Sample.ECommerce.Models;
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
    private readonly IExportJobService _exports;

    public ProductController(
        IProductService productService,
        IExportJobService exports)
    {
        _productService = productService ?? throw new ArgumentNullException(nameof(productService));
        _exports = exports ?? throw new ArgumentNullException(nameof(exports));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + SampleECommercePermissionCodes.ProductsRead)]
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

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + SampleECommercePermissionCodes.ProductsRead)]
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProductDto>), StatusCodes.Status200OK)]
    public Task<PagedResult<ProductDto>> GetPaged(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10)
    {
        return _productService.PageQueryAsync(x => x.Id > 0, pageIndex, pageSize);
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + SampleECommercePermissionCodes.ProductsCreate)]
    [HttpPost]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductDto>> Create([FromBody] CreateProductRequest request)
    {
        var created = await _productService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + SampleECommercePermissionCodes.ProductsUpdate)]
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

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + SampleECommercePermissionCodes.ProductsDelete)]
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

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + SampleECommercePermissionCodes.ProductsRead)]
    [HttpGet("{id:long}/exists")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public Task<bool> Exists([FromRoute] long id, CancellationToken ct = default)
    {
        return _productService.ExistsAsync(p => p.Id == id, ct);
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + SampleECommercePermissionCodes.ProductsRead)]
    [HttpGet("count")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public Task<int> Count(CancellationToken ct = default)
    {
        return _productService.CountAsync(p => true, ct);
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + SampleECommercePermissionCodes.ProductsRead)]
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

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + SampleECommercePermissionCodes.ProductsExport)]
    [HttpPost("exports")]
    [ProducesResponseType(typeof(ExportEnqueueResult), StatusCodes.Status202Accepted)]
    public async Task<ActionResult<ExportEnqueueResult>> ExportAsync(
        [FromBody] ExportProductsRequest request,
        CancellationToken ct = default)
    {
        var result = await _exports.EnqueueAsync(
            new ExportEnqueueRequest<ExportProductsRequest>
            {
                ExportTaskType = SampleECommerceExportTaskTypes.ProductList,
                Query = request
            },
            ct);

        return Accepted($"/api/product/exports/{result.ExportJobId}", result);
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + SampleECommercePermissionCodes.ProductsExport)]
    [HttpGet("exports/{exportJobId:long}")]
    [ProducesResponseType(typeof(ExportJobStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExportJobStatusDto>> GetExportAsync(
        long exportJobId,
        CancellationToken ct = default)
    {
        var result = await _exports.GetAsync(exportJobId, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + SampleECommercePermissionCodes.ProductsExport)]
    [HttpGet("exports/{exportJobId:long}/download")]
    public async Task<IActionResult> DownloadExportAsync(
        long exportJobId,
        CancellationToken ct = default)
    {
        var result = await _exports.OpenDownloadAsync(exportJobId, ct);
        return File(result.Content, result.ContentType, result.FileName);
    }
}
