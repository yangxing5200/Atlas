using Atlas.Models.DTOs;
using Atlas.Models.Tenant.Responses;
using Atlas.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Sample.WebApi.Controllers
{
    /// <summary>
    /// Store management endpoints
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class StoreController : ControllerBase
    {
        private readonly IStoreService _storeService;
        private readonly ILogger<StoreController> _logger;

        public StoreController(
            IStoreService storeService,
            ILogger<StoreController> logger)
        {
            _storeService = storeService;
            _logger = logger;
        }

        /// <summary>
        /// Get store by ID
        /// </summary>
        /// <param name="id">Store ID</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Store details</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(StoreDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<StoreDto>> GetById(
            [FromRoute] long id,
            CancellationToken ct = default)
        {
            try
            {
                var store = await _storeService.GetByIdAsync(id, ct);
                if (store == null)
                {
                    return NotFound(new { message = $"Store with ID {id} not found" });
                }

                return Ok(store);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving store {StoreId}", id);
                return StatusCode(500, new { message = "An error occurred while retrieving the store" });
            }
        }

        /// <summary>
        /// Get stores with pagination
        /// </summary>
        /// <param name="pageIndex">Page index (starts from 1)</param>
        /// <param name="pageSize">Page size</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Paged list of stores</returns>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<StoreDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedResult<StoreDto>>> GetPaged(
            [FromQuery] int pageIndex = 1,
            [FromQuery] int pageSize = 10,
            CancellationToken ct = default)
        {
            try
            {
                var result = await _storeService.PageQueryAsync(
                    s => true,  // Get all stores, you can add filtering here
                    pageIndex,
                    pageSize,
                    ct);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving stores page {PageIndex}", pageIndex);
                return StatusCode(500, new { message = "An error occurred while retrieving stores" });
            }
        }

        /// <summary>
        /// Create a new store
        /// </summary>
        /// <param name="storeDto">Store details</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Created store</returns>
        [HttpPost]
        [ProducesResponseType(typeof(StoreDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<StoreDto>> Create(
            [FromBody] StoreDto storeDto,
            CancellationToken ct = default)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var created = await _storeService.AddAsync(storeDto, ct);
                return CreatedAtAction(
                    nameof(GetById),
                    new { id = created.Id },
                    created);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating store");
                return StatusCode(500, new { message = "An error occurred while creating the store" });
            }
        }

        /// <summary>
        /// Update an existing store
        /// </summary>
        /// <param name="id">Store ID</param>
        /// <param name="storeDto">Updated store details</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>No content</returns>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(
            [FromRoute] long id,
            [FromBody] StoreDto storeDto,
            CancellationToken ct = default)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var exists = await _storeService.ExistsAsync(s => s.Id == id, ct);
                if (!exists)
                {
                    return NotFound(new { message = $"Store with ID {id} not found" });
                }

                await _storeService.UpdateAsync(id, storeDto, ct);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating store {StoreId}", id);
                return StatusCode(500, new { message = "An error occurred while updating the store" });
            }
        }

        /// <summary>
        /// Delete a store
        /// </summary>
        /// <param name="id">Store ID</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>No content</returns>
        [HttpDelete("{id}")]
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting store {StoreId}", id);
                return StatusCode(500, new { message = "An error occurred while deleting the store" });
            }
        }

        /// <summary>
        /// Check if store exists
        /// </summary>
        /// <param name="id">Store ID</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>True if exists</returns>
        [HttpGet("{id}/exists")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        public async Task<ActionResult<bool>> Exists(
            [FromRoute] long id,
            CancellationToken ct = default)
        {
            try
            {
                var exists = await _storeService.ExistsAsync(s => s.Id == id, ct);
                return Ok(exists);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking store existence {StoreId}", id);
                return StatusCode(500, new { message = "An error occurred while checking store existence" });
            }
        }

        /// <summary>
        /// Get total count of stores
        /// </summary>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Total count</returns>
        [HttpGet("count")]
        [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
        public async Task<ActionResult<int>> Count(CancellationToken ct = default)
        {
            try
            {
                var count = await _storeService.CountAsync(s => true, ct);
                return Ok(count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting stores");
                return StatusCode(500, new { message = "An error occurred while counting stores" });
            }
        }
    }
}