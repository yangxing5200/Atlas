using Atlas.Models.DTOs;
using Atlas.Models.Requests;
using Atlas.Models.Responses;

namespace Atlas.Services.Abstractions;

public interface IUserManagementService
{
    Task<OperationResult<UserDto>> CreateUserAsync(CreateUserRequest request);

    Task<OperationResult<UserDto>> UpdateUserAsync(UpdateUserRequest request);

    Task<OperationResult> DeleteUserAsync(long userId);

    Task<UserDetailDto?> GetUserByIdAsync(long userId);

    Task<UserDto?> GetUserByUserNameAsync(string userName);

    Task<UserPagedResponse> GetUsersAsync(UserQueryRequest request);

    Task<OperationResult> ChangePasswordAsync(long userId, ChangePasswordRequest request);

    Task<OperationResult> ResetPasswordAsync(ResetPasswordRequest request);

    Task<OperationResult> SetUserStatusAsync(long userId, bool isActive);

    Task<OperationResult> UnlockUserAsync(long userId);
}
