using Atlas.Models.DTOs;
using Atlas.Models.Requests;
using Atlas.Models.Responses;
using Atlas.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Sample.WebApi.Controllers
{
    /// <summary>
    /// User management endpoints
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILogger<UserController> _logger;

        public UserController(
            IUserService userService,
            ILogger<UserController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        /// <summary>
        /// User login
        /// </summary>
        /// <param name="request">Login credentials</param>
        /// <returns>Login response with token</returns>
        [HttpPost("login")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
                var userAgent = Request.Headers["User-Agent"].ToString();

                var response = await _userService.LoginAsync(request, ipAddress, userAgent);

                if (response == null)
                {
                    return Unauthorized(new { message = "Invalid username or password" });
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for user {UserName}", request.UserName);
                return StatusCode(500, new { message = "An error occurred during login" });
            }
        }

        /// <summary>
        /// User logout
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        /// <returns>Operation result</returns>
        [HttpPost("logout")]
        [Authorize]
        [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<OperationResult>> Logout([FromBody] string sessionId)
        {
            try
            {
                var result = await _userService.LogoutAsync(sessionId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout for session {SessionId}", sessionId);
                return StatusCode(500, new { message = "An error occurred during logout" });
            }
        }

        /// <summary>
        /// Create a new user
        /// </summary>
        /// <param name="request">User creation request</param>
        /// <returns>Created user</returns>
        [HttpPost]
        [Authorize]
        [ProducesResponseType(typeof(OperationResult<UserDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<OperationResult<UserDto>>> CreateUser([FromBody] CreateUserRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _userService.CreateUserAsync(request);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return CreatedAtAction(
                    nameof(GetById),
                    new { id = result.Data.Id },
                    result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user");
                return StatusCode(500, new { message = "An error occurred while creating the user" });
            }
        }

        /// <summary>
        /// Update an existing user
        /// </summary>
        /// <param name="request">User update request</param>
        /// <returns>Updated user</returns>
        [HttpPut]
        [Authorize]
        [ProducesResponseType(typeof(OperationResult<UserDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<OperationResult<UserDto>>> UpdateUser([FromBody] UpdateUserRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _userService.UpdateUserAsync(request);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user");
                return StatusCode(500, new { message = "An error occurred while updating the user" });
            }
        }

        /// <summary>
        /// Delete a user (soft delete)
        /// </summary>
        /// <param name="id">User ID</param>
        /// <returns>Operation result</returns>
        [HttpDelete("{id}")]
        [Authorize]
        [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<OperationResult>> DeleteUser([FromRoute] long id)
        {
            try
            {
                var result = await _userService.DeleteUserAsync(id);

                if (!result.Success)
                {
                    return NotFound(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user {UserId}", id);
                return StatusCode(500, new { message = "An error occurred while deleting the user" });
            }
        }

        /// <summary>
        /// Get user by ID
        /// </summary>
        /// <param name="id">User ID</param>
        /// <returns>User details</returns>
        [HttpGet("{id}")]
        [Authorize]
        [ProducesResponseType(typeof(UserDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<UserDetailDto>> GetById([FromRoute] long id)
        {
            try
            {
                var user = await _userService.GetUserByIdAsync(id);

                if (user == null)
                {
                    return NotFound(new { message = $"User with ID {id} not found" });
                }

                return Ok(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user {UserId}", id);
                return StatusCode(500, new { message = "An error occurred while retrieving the user" });
            }
        }

        /// <summary>
        /// Get user by username
        /// </summary>
        /// <param name="userName">Username</param>
        /// <returns>User information</returns>
        [HttpGet("by-username/{userName}")]
        [Authorize]
        [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<UserDto>> GetByUserName([FromRoute] string userName)
        {
            try
            {
                var user = await _userService.GetUserByUserNameAsync(userName);

                if (user == null)
                {
                    return NotFound(new { message = $"User with username '{userName}' not found" });
                }

                return Ok(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user by username {UserName}", userName);
                return StatusCode(500, new { message = "An error occurred while retrieving the user" });
            }
        }

        /// <summary>
        /// Get users with pagination and filtering
        /// </summary>
        /// <param name="request">Query parameters</param>
        /// <returns>Paged list of users</returns>
        [HttpGet]
        [Authorize]
        [ProducesResponseType(typeof(UserPagedResponse), StatusCodes.Status200OK)]
        public async Task<ActionResult<UserPagedResponse>> GetUsers([FromQuery] UserQueryRequest request)
        {
            try
            {
                var result = await _userService.GetUsersAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving users");
                return StatusCode(500, new { message = "An error occurred while retrieving users" });
            }
        }

        /// <summary>
        /// Change user password
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="request">Password change request</param>
        /// <returns>Operation result</returns>
        [HttpPost("{userId}/change-password")]
        [Authorize]
        [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<OperationResult>> ChangePassword(
            [FromRoute] long userId,
            [FromBody] ChangePasswordRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _userService.ChangePasswordAsync(userId, request);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for user {UserId}", userId);
                return StatusCode(500, new { message = "An error occurred while changing password" });
            }
        }

        /// <summary>
        /// Reset user password (admin operation)
        /// </summary>
        /// <param name="request">Password reset request</param>
        /// <returns>Operation result</returns>
        [HttpPost("reset-password")]
        [Authorize]
        [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<OperationResult>> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _userService.ResetPasswordAsync(request);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password");
                return StatusCode(500, new { message = "An error occurred while resetting password" });
            }
        }

        /// <summary>
        /// Assign stores to user
        /// </summary>
        /// <param name="request">Store assignment request</param>
        /// <returns>Operation result</returns>
        [HttpPost("assign-stores")]
        [Authorize]
        [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<OperationResult>> AssignStores([FromBody] AssignStoresRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _userService.AssignStoresAsync(request);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning stores to user");
                return StatusCode(500, new { message = "An error occurred while assigning stores" });
            }
        }

        /// <summary>
        /// Enable or disable user
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="isActive">Active status</param>
        /// <returns>Operation result</returns>
        [HttpPut("{userId}/status")]
        [Authorize]
        [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<OperationResult>> SetUserStatus(
            [FromRoute] long userId,
            [FromQuery] bool isActive)
        {
            try
            {
                var result = await _userService.SetUserStatusAsync(userId, isActive);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting status for user {UserId}", userId);
                return StatusCode(500, new { message = "An error occurred while setting user status" });
            }
        }

        /// <summary>
        /// Unlock user account
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>Operation result</returns>
        [HttpPost("{userId}/unlock")]
        [Authorize]
        [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<OperationResult>> UnlockUser([FromRoute] long userId)
        {
            try
            {
                var result = await _userService.UnlockUserAsync(userId);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unlocking user {UserId}", userId);
                return StatusCode(500, new { message = "An error occurred while unlocking user" });
            }
        }

        /// <summary>
        /// Get user login logs
        /// </summary>
        /// <param name="request">Query parameters</param>
        /// <returns>Paged list of login logs</returns>
        [HttpGet("login-logs")]
        [Authorize]
        [ProducesResponseType(typeof(LoginLogPagedResponse), StatusCodes.Status200OK)]
        public async Task<ActionResult<LoginLogPagedResponse>> GetLoginLogs([FromQuery] LoginLogQueryRequest request)
        {
            try
            {
                var result = await _userService.GetLoginLogsAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving login logs");
                return StatusCode(500, new { message = "An error occurred while retrieving login logs" });
            }
        }

        /// <summary>
        /// Force user logout (revoke all tokens)
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>Operation result</returns>
        [HttpPost("{userId}/force-logout")]
        [Authorize]
        [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<OperationResult>> ForceLogoutAll([FromRoute] long userId)
        {
            try
            {
                var result = await _userService.ForceLogoutAllAsync(userId);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forcing logout for user {UserId}", userId);
                return StatusCode(500, new { message = "An error occurred while forcing logout" });
            }
        }

        /// <summary>
        /// Get active user sessions
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>List of active sessions</returns>
        [HttpGet("{userId}/sessions")]
        [Authorize]
        [ProducesResponseType(typeof(List<UserLoginLogDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<UserLoginLogDto>>> GetActiveSessions([FromRoute] long userId)
        {
            try
            {
                var sessions = await _userService.GetActiveSessionsAsync(userId);
                return Ok(sessions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active sessions for user {UserId}", userId);
                return StatusCode(500, new { message = "An error occurred while retrieving active sessions" });
            }
        }
    }
}