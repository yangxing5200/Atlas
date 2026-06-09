using Atlas.Core.Entities.Tenant;
using Atlas.Models.DTOs;

namespace Atlas.Services;

internal static class UserDtoMapper
{
    public static UserDto ToDto(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            TenantId = user.TenantId,
            UserName = user.UserName,
            RealName = user.RealName,
            NickName = user.NickName,
            Phone = user.Phone,
            Email = user.Email,
            Avatar = user.Avatar,
            Gender = user.Gender,
            Type = user.Type,
            Status = user.Status,
            IsActivated = user.IsActivated,
            DefaultStoreId = user.DefaultStoreId,
            DefaultStoreName = user.DefaultStore?.Name,
            EmployeeNo = user.EmployeeNo,
            Position = user.Position,
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt
        };
    }
}
