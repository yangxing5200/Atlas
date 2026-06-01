using Atlas.Core.Services;
using Atlas.Infrastructure.Security;
using Atlas.Infrastructure.Security.Permissions;
using Atlas.Models.DTOs;
using Atlas.Models.Requests;
using Atlas.Models.Responses;
using Atlas.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Sample.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class UserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ICurrentIdentity _currentIdentity;

    public UserController(
        IUserService userService,
        ICurrentIdentity currentIdentity)
    {
        _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        _currentIdentity = currentIdentity ?? throw new ArgumentNullException(nameof(currentIdentity));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var userAgent = Request.Headers["User-Agent"].ToString();
        var response = await _userService.LoginAsync(request, ipAddress, userAgent);

        return response == null
            ? Unauthorized(new { message = "Invalid username or password" })
            : Ok(response);
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<LoginResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var userAgent = Request.Headers["User-Agent"].ToString();
        var response = await _userService.RefreshTokenAsync(request, ipAddress, userAgent);
        return response.Success ? Ok(response) : Unauthorized(response);
    }

    [HttpPost("logout")]
    [Authorize(Policy = AuthorizationPolicies.RequireIdentitySelf)]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult>> Logout()
    {
        if (string.IsNullOrEmpty(_currentIdentity.SessionId))
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

        var result = await _userService.SwitchStoreAsync(_currentIdentity.UserId.Value, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("accessible-stores")]
    [Authorize(Policy = AuthorizationPolicies.RequireIdentitySelf)]
    [ProducesResponseType(typeof(List<StoreInfoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<StoreInfoDto>>> GetMyAccessibleStores()
    {
        if (!_currentIdentity.UserId.HasValue)
            return Unauthorized(new { message = "User ID not found in token" });

        return Ok(await _userService.GetAccessibleStoresAsync(_currentIdentity.UserId.Value));
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.RequireUsersManage)]
    [ProducesResponseType(typeof(OperationResult<UserDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<OperationResult<UserDto>>> CreateUser([FromBody] CreateUserRequest request)
    {
        var result = await _userService.CreateUserAsync(request);
        if (!result.Success || result.Data == null)
            return BadRequest(result);

        return CreatedAtAction(nameof(GetById), new { id = result.Data.Id }, result);
    }

    [HttpPut]
    [Authorize(Policy = AuthorizationPolicies.RequireUsersManage)]
    [ProducesResponseType(typeof(OperationResult<UserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult<UserDto>>> UpdateUser([FromBody] UpdateUserRequest request)
    {
        return ToActionResult(await _userService.UpdateUserAsync(request));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = AuthorizationPolicies.RequireUsersManage)]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult>> DeleteUser([FromRoute] long id)
    {
        var result = await _userService.DeleteUserAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = AuthorizationPolicies.RequireUsersRead)]
    [ProducesResponseType(typeof(UserDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserDetailDto>> GetById([FromRoute] long id)
    {
        var user = await _userService.GetUserByIdAsync(id);
        return user == null
            ? NotFound(new { message = $"User with ID {id} not found" })
            : Ok(user);
    }

    [HttpGet("by-username/{userName}")]
    [Authorize(Policy = AuthorizationPolicies.RequireUsersRead)]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserDto>> GetByUserName([FromRoute] string userName)
    {
        var user = await _userService.GetUserByUserNameAsync(userName);
        return user == null
            ? NotFound(new { message = $"User with username '{userName}' not found" })
            : Ok(user);
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.RequireUsersRead)]
    [ProducesResponseType(typeof(UserPagedResponse), StatusCodes.Status200OK)]
    public Task<UserPagedResponse> GetUsers([FromQuery] UserQueryRequest request)
    {
        return _userService.GetUsersAsync(request);
    }

    [HttpPost("change-password")]
    [Authorize(Policy = AuthorizationPolicies.RequireIdentitySelf)]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult>> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (!_currentIdentity.UserId.HasValue)
            return BadRequest(new { message = "User ID not found in token" });

        return ToActionResult(await _userService.ChangePasswordAsync(_currentIdentity.UserId.Value, request));
    }

    [HttpPost("reset-password")]
    [Authorize(Policy = AuthorizationPolicies.RequireUsersManage)]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult>> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        return ToActionResult(await _userService.ResetPasswordAsync(request));
    }

    [HttpPost("assign-stores")]
    [Authorize(Policy = AuthorizationPolicies.RequireUsersManage)]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult>> AssignStores([FromBody] AssignStoresRequest request)
    {
        return ToActionResult(await _userService.AssignStoresAsync(request));
    }

    [HttpPost("assign-roles")]
    [Authorize(Policy = AuthorizationPolicies.RequireRolesManage)]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult>> AssignRoles([FromBody] AssignRolesRequest request)
    {
        return ToActionResult(await _userService.AssignRolesAsync(request));
    }

    [HttpPut("{userId:long}/status")]
    [Authorize(Policy = AuthorizationPolicies.RequireUsersManage)]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult>> SetUserStatus(
        [FromRoute] long userId,
        [FromQuery] bool isActive)
    {
        return ToActionResult(await _userService.SetUserStatusAsync(userId, isActive));
    }

    [HttpPost("{userId:long}/unlock")]
    [Authorize(Policy = AuthorizationPolicies.RequireUsersManage)]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult>> UnlockUser([FromRoute] long userId)
    {
        return ToActionResult(await _userService.UnlockUserAsync(userId));
    }

    [HttpGet("login-logs")]
    [Authorize(Policy = AuthorizationPolicies.RequireAuditRead)]
    [ProducesResponseType(typeof(LoginLogPagedResponse), StatusCodes.Status200OK)]
    public Task<LoginLogPagedResponse> GetLoginLogs([FromQuery] LoginLogQueryRequest request)
    {
        return _userService.GetLoginLogsAsync(request);
    }

    [HttpPost("{userId:long}/force-logout")]
    [Authorize(Policy = AuthorizationPolicies.RequireUsersManage)]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult>> ForceLogoutAll([FromRoute] long userId)
    {
        return ToActionResult(await _userService.ForceLogoutAllAsync(userId));
    }

    [HttpGet("{userId:long}/sessions")]
    [Authorize(Policy = AuthorizationPolicies.RequireUsersRead)]
    [ProducesResponseType(typeof(List<UserLoginLogDto>), StatusCodes.Status200OK)]
    public Task<List<UserLoginLogDto>> GetActiveSessions([FromRoute] long userId)
    {
        return _userService.GetActiveSessionsAsync(userId);
    }

    private ActionResult<OperationResult> ToActionResult(OperationResult result)
    {
        return result.Success ? Ok(result) : BadRequest(result);
    }

    private ActionResult<OperationResult<T>> ToActionResult<T>(OperationResult<T> result)
    {
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
