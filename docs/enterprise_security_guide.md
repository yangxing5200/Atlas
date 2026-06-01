# Atlas 企业安全能力

本文说明 M5 安全脚手架的默认边界：RBAC、刷新令牌、会话撤销、审计事件和日志脱敏。

## RBAC 数据模型

租户库新增以下实体：

| 实体 | 作用 | 隔离键 |
| --- | --- | --- |
| `Permission` | 权限点目录，使用 `Code` 表达业务权限。 | 唯一键 `TenantId + Code` |
| `Role` | 租户内角色，可标记为平台、租户或门店范围。 | 唯一键 `TenantId + Code` |
| `RolePermission` | 角色和权限的授权关系。 | 唯一键 `TenantId + RoleId + PermissionId` |
| `UserRole` | 用户和角色的授权关系，`StoreId=0` 表示非门店限定。 | 唯一键 `TenantId + UserId + RoleId + StoreId` |

权限目录通过 `IRbacSeedService.SeedTenantAsync(tenantId)` 初始化。seed 必须显式传入 `tenantId`，不会向共享租户库写入无租户归属的权限数据。

Controller 使用 `AuthorizationPolicies.RequireUsersManage`、`RequireStoresRead` 等常量。动态权限策略统一以 `Permission:` 开头，由 `PermissionAuthorizationPolicyProvider` 解析。

## 权限缓存

`IPermissionChecker` 会按 `tenantId + userId + storeId` 缓存用户权限集合。角色、门店授权或权限关系变更后，必须调用：

```csharp
await permissionChecker.InvalidateUserPermissionsAsync(tenantId, userId, storeId);
```

样例用户服务中的 `AssignStoresAsync` 和 `AssignRolesAsync` 已执行缓存失效，并写入审计事件。

## 会话和刷新令牌

登录接口返回 `Token` 和 `RefreshToken`。访问令牌继续包含 `TenantId`、`StoreId`、`SessionId` 和 `TokenVersion`；刷新令牌只保存哈希，明文只在签发时返回。

配置项：

```json
{
  "Security": {
    "Token": {
      "ExpirationMinutes": 1440,
      "RefreshTokenExpirationDays": 7
    }
  }
}
```

退出登录、切换门店、修改密码、重置密码和强制下线都会撤销相关 session 或用户的 refresh token。`TokenVersionValidationMiddleware` 继续兜底校验旧访问令牌，已撤销 session 会返回 401。

## 审计事件

租户库新增 `AuditEvents`，标准字段包含：

- `TenantId`
- `UserId`
- `StoreId`
- `SessionId`
- `TraceId`
- `Category`
- `Action`
- `Outcome`
- `EntityType` / `EntityId`

`IAuditEventService` 写入失败时只记录错误，不改变主业务操作的返回结果。登录、登出、刷新令牌、切换门店、分配门店、分配角色、修改密码、重置密码、强制下线和租户开通已接入审计。

## 日志脱敏

`Logging:Atlas:SensitiveFields` 可配置敏感字段。默认覆盖 `password`、`token`、`accessToken`、`refreshToken`、`secret`、`phone`、`email` 等字段。

脱敏策略：

| 类型 | 示例 |
| --- | --- |
| 密码、token、secret | `***REDACTED***` |
| 手机号 | `138****5678` |
| 邮箱 | `j***@example.com` |

不要在日志 message template 中拼接明文密钥；业务日志应使用结构化字段，让脱敏策略在写出前处理属性。
