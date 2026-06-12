using Atlas.Core.Services;
using Atlas.Infrastructure.Security;
using Atlas.Models.DTOs;
using Atlas.Models.Requests;
using Atlas.Models.Responses;
using Atlas.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Extensions.DependencyInjection.Controllers;

[ApiController]
[Route("api/user")]
[Produces("application/json")]
public sealed class AtlasUserAuthController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ICurrentIdentity _currentIdentity;

    public AtlasUserAuthController(
        IUserService userService,
        ICurrentIdentity currentIdentity)
    {
        _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        _currentIdentity = currentIdentity ?? throw new ArgumentNullException(nameof(currentIdentity));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var response = await _userService.LoginAsync(request, GetIpAddress(), GetUserAgent());
        return response.Success ? Ok(response) : Unauthorized(response);
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var response = await _userService.RefreshTokenAsync(request, GetIpAddress(), GetUserAgent());
        return response.Success ? Ok(response) : Unauthorized(response);
    }

    [HttpPost("logout")]
    [Authorize(Policy = AuthorizationPolicies.RequireIdentitySelf)]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult>> Logout()
    {
        if (string.IsNullOrWhiteSpace(_currentIdentity.SessionId))
            return BadRequest(new { message = "Session ID not found in token" });

        return Ok(await _userService.LogoutAsync(_currentIdentity.SessionId));
    }

    [HttpPost("switch-store")]
    [Authorize(Policy = AuthorizationPolicies.RequireIdentitySelf)]
    [ProducesResponseType(typeof(SwitchStoreResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SwitchStoreResponse>> SwitchStore([FromBody] SwitchStoreRequest request)
    {
        if (!_currentIdentity.UserId.HasValue)
            return Unauthorized(new { message = "User ID not found in token" });

        var response = await _userService.SwitchStoreAsync(_currentIdentity.UserId.Value, request);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpGet("accessible-stores")]
    [Authorize(Policy = AuthorizationPolicies.RequireIdentitySelf)]
    [ProducesResponseType(typeof(List<StoreInfoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<StoreInfoDto>>> GetAccessibleStores()
    {
        if (!_currentIdentity.UserId.HasValue)
            return Unauthorized(new { message = "User ID not found in token" });

        return Ok(await _userService.GetAccessibleStoresAsync(_currentIdentity.UserId.Value));
    }

    private string GetIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
    }

    private string GetUserAgent()
    {
        return Request.Headers["User-Agent"].ToString();
    }
}
