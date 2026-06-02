# 登录授权与数据范围隔离说明

本文说明 Atlas 当前登录、Token 鉴权、RefreshToken 换发、门店切换、RBAC 授权、Token 主动失效，以及直营共享、集团共享、门店独享数据隔离的运行方式。

> 适用范围：当前代码库中的示例 WebApi 和 Atlas 基础设施。示例接口主要位于 `samples/Atlas.Sample.WebApi/Controllers/UserController.cs`，核心服务位于 `src/Atlas.Services/UserService.cs`，Token/RefreshToken 基础设施位于 `src/Atlas.Infrastructure.Security`。

## 一、本地运行方式

### 1. 初始化本地 MySQL

本地 MySQL 连接约定：

```text
Host: localhost
Port: 3306
User: root
Password: root
```

执行初始化：

```powershell
dotnet run --project tools\Atlas.LocalSetup\Atlas.LocalSetup.csproj
```

该工具会重建两个库：

```text
atlas_global
atlas
```

初始化后的演示账号：

| 用户名 | 密码 | 默认门店 | 授权门店 |
| --- | --- | --- | --- |
| `hq_admin` | `Pass1234!` | `110001` 总部 | 总部、直营一店、直营二店、加盟一店 |
| `direct_a_mgr` | `Pass1234!` | `110011` 直营一店 | 直营一店 |
| `franchise_mgr` | `Pass1234!` | `110101` 加盟一店 | 加盟一店 |

### 2. 启动 WebApi

```powershell
dotnet run --project samples\Atlas.Sample.WebApi\Atlas.Sample.WebApi.csproj --urls http://localhost:5212
```

Swagger 地址：

```text
http://localhost:5212/swagger/index.html
```

## 二、整体认证授权设计结论

当前设计把“用户主体”“当前门店上下文”“数据范围”“功能权限”拆开处理：

| 概念 | 含义 | 数据来源/运行时来源 |
| --- | --- | --- |
| 租户 | SaaS 租户，先由登录域名定位 | Global 库 `Tenants` |
| 用户 | 租户内集团级用户主体，不天然附属于某个门店 | 租户库 `Users` |
| 可操作门店 | 用户被授权可以操作哪些门店 | 租户库 `UserStores`，含生效/失效时间 |
| 当前操作门店 | 本次请求以哪个门店身份执行 | AccessToken 中的 `StoreId` claim |
| 会话 | 一次登录或一次门店切换产生的 Token 会话 | AccessToken/RefreshToken 共享的 `SessionId` |
| 功能权限 | 用户能不能访问某个 API/能力 | `UserRoles` → `Roles` → `RolePermissions` → `Permissions`，并受套餐权益裁剪 |
| 数据可见范围 | 当前门店能看到哪些业务数据 | `DataScope` 根据 Token 中 `TenantId`、`StoreId` 和门店类型计算 |

核心结论：

1. **用户不是某个门店的附属账号**。只要 `UserStores` 中配置了用户可操作某门店，用户就可以切换到该门店。
2. **当前门店写在 AccessToken 中**。登录和切换门店都会生成新的 AccessToken，后续请求必须使用最新 Token，数据层才能按当前门店计算数据范围。
3. **RefreshToken 是服务端可撤销、单次轮换的长凭证**。客户端只保存明文 RefreshToken，服务端只保存哈希；每次刷新成功都会吊销旧 RefreshToken 并发放新 RefreshToken。
4. **授权分两层**：API 入口先通过 `[Authorize]`/`Permission:*` 做功能权限判断；数据查询再通过仓储和 `DataScope` 做租户/门店范围过滤。
5. **主动失效有三条线**：Session 黑名单、用户 `TokenVersion`、RefreshToken 撤销。三者分别覆盖“单会话 AccessToken 失效”“用户所有旧 AccessToken 失效”“旧会话不能继续换新 AccessToken”。

## 三、登录流程

入口：

```http
POST /api/User/login
```

请求体：

```json
{
  "domain": "demo",
  "userName": "hq_admin",
  "password": "Pass1234!",
  "rememberMe": false
}
```

登录时不需要传 `storeId`。用户第一次登录时通常还没有选择门店，因此系统会自动确定初始门店。

服务端流程：

1. 根据 `domain` 查询 Global 库的 `Tenants`，确认租户存在且不是禁用/过期状态。
2. 使用显式 `tenantId` 查询租户库中的 `Users`。此时还没有 Token，不能依赖 `ICurrentIdentity.TenantId`。
3. 校验用户是否可登录：未删除、未禁用、未锁定、已激活、密码正确。
4. 查询 `UserStores` 和 `Stores`，得到用户当前有效且门店启用的可操作门店列表。
5. 自动确定登录门店：
   - 优先 `UserStore.IsPrimary = true` 的门店；
   - 其次用户 `DefaultStoreId`；
   - 最后使用第一个可操作门店。
6. 写入用户当前 `TokenVersion` 到缓存，然后生成 AccessToken。
7. 基于同一个 `TokenInfo` 签发 RefreshToken。AccessToken 和 RefreshToken 会共享同一个 `SessionId`、`TenantId`、`UserId`、`StoreId`。
8. 重置登录失败次数、更新最后登录时间/IP、写入 `UserLoginLog` 和安全审计日志。
9. 返回 AccessToken、RefreshToken、当前门店和可切换门店列表。

响应关键字段：

```json
{
  "success": true,
  "token": "...",
  "refreshToken": "rt.租户ID.用户ID.TokenId.随机密钥",
  "expiresIn": 86400,
  "expiresAt": "...",
  "currentStore": {
    "id": "110001",
    "name": "总部",
    "typeName": "Headquarters"
  },
  "accessibleStores": [
    { "id": "110001", "name": "总部" },
    { "id": "110011", "name": "直营一店" },
    { "id": "110012", "name": "直营二店" },
    { "id": "110101", "name": "加盟一店" }
  ]
}
```

`rememberMe` 当前只影响登录时生成的 AccessToken 时长：

| `rememberMe` | AccessToken 时长 |
| --- | --- |
| `false` | 1440 分钟，即 24 小时 |
| `true` | 10080 分钟，即 7 天 |

注意：RefreshToken 的时长不由 `rememberMe` 决定，而由配置 `Security:Token:RefreshTokenExpirationDays` 决定，默认值为 7 天。

## 四、AccessToken 鉴权机制

### 1. Token 内容

AccessToken 由 `CustomTokenService` 生成，格式是：

```text
版本号.时间戳.加密后的TokenInfo.签名
```

`TokenInfo` 包含：

| 字段 | 说明 |
| --- | --- |
| `UserId` | 当前用户 |
| `UserName` | 用户名 |
| `TenantId` | 当前租户 |
| `StoreId` | 当前操作门店 |
| `ExpiresAt` | AccessToken 过期时间，Unix 秒 |
| `SessionId` | 会话 ID，用于单会话失效和关联 RefreshToken |
| `TokenVersion` | 用户 Token 版本，用于用户级批量失效 |

生成时会先序列化 `TokenInfo`，再使用 `ICryptoService` 加密 payload，最后使用 HMAC-SHA256 对 `版本号.时间戳.加密payload` 签名。

### 2. 请求鉴权入口

认证处理器是 `CustomTokenAuthenticationHandler`。它按以下顺序取 Token：

1. `Authorization: Bearer {token}`
2. Cookie：`atlas-auth-token`，实际 Cookie 名可由 `Security:Token:CookieName` 配置
3. QueryString：`access_token`，默认关闭，仅在 `Security:Token:EnableQueryStringToken = true` 时启用
4. Header：`X-Access-Token`，默认启用，可由 `Security:Token:EnableCustomHeader` 控制

校验通过后写入 Claims：

| Claim | 来源 |
| --- | --- |
| `uid` | `UserId` |
| `tid` | `TenantId` |
| `sid` | `StoreId` |
| `uname` | `UserName` |
| `session_id` | `SessionId` |
| `token_version` | `TokenVersion` |
| `token` | 原始 AccessToken |

`CurrentIdentity` 再从 Claims 中读取当前用户上下文，供服务层、数据层、审计拦截器使用。

### 3. AccessToken 校验步骤

`CustomTokenService` 校验 AccessToken 的主要顺序：

1. 检查 Token 格式是否正好有四段。
2. 检查版本号。
3. 检查 Token 外层时间戳是否在允许范围内，允许 AccessToken 过期时间外加 5 分钟偏移。
4. 先验签，签名正确后才解密 payload。
5. 解析 `TokenInfo` 并检查 `ExpiresAt`。
6. 检查 `SessionId` 是否已进入黑名单。
7. 检查缓存中的用户 `TokenVersion` 是否与 Token 内 `TokenVersion` 一致。
8. 校验成功后把解析结果短暂缓存 30 秒；命中短缓存时仍会检查 Session 黑名单，保证退出登录和切换门店能快速生效。

请求管道中还有 `TokenVersionValidationMiddleware`，它在 `UseAuthentication()` 之后、`UseAuthorization()` 之前执行。该中间件会再次检查 Session 黑名单和 `TokenVersion`；当 `TokenVersion` 缓存未命中时，会使用显式 `tenantId` 回源查询用户表并刷新缓存。

## 五、RefreshToken 设计详解

### 1. RefreshToken 的目标

AccessToken 负责每次 API 请求的身份认证和当前门店上下文，RefreshToken 负责在 AccessToken 过期或即将过期时换取新 AccessToken。

RefreshToken 当前具备这些安全属性：

| 属性 | 当前实现 |
| --- | --- |
| 服务端可撤销 | `RefreshTokens` 表记录 `RevokedAtUtc`、`RevokedReason` |
| 明文不落库 | 服务端只保存 `TokenHash = SHA256(refreshToken)` |
| 单次使用 | 刷新成功后旧 Token 标记为 `Rotated`，并写入 `ReplacedByTokenId` |
| 绑定租户和用户 | Token 明文格式中含 `tenantId`、`userId`、`tokenId`，查询时也按三者和哈希匹配 |
| 绑定会话和门店 | 表中保存 `SessionId`、`StoreId`，刷新后沿用原门店上下文 |
| 受 Session 黑名单影响 | 如果会话已被退出/切换门店/强制失效，RefreshToken 不能继续换发 |
| 受用户状态影响 | 刷新时回查用户，用户必须存在、未删除且状态为 Active |
| 受 TokenVersion 影响 | 刷新时读取用户当前 `TokenVersion`，新 AccessToken 使用最新版本 |

### 2. RefreshToken 明文格式

签发给客户端的 RefreshToken 明文格式是：

```text
rt.{tenantId}.{userId}.{tokenId}.{secret}
```

字段含义：

| 字段 | 说明 |
| --- | --- |
| `rt` | 固定前缀，用于快速识别 RefreshToken |
| `tenantId` | 租户 ID，用于定位租户库和查询分片 |
| `userId` | 用户 ID |
| `tokenId` | RefreshToken 实体 ID，雪花 ID |
| `secret` | 32 字节随机数的 Base64Url 字符串 |

安全要点：

1. `tenantId/userId/tokenId` 不是秘密，只用于快速定位数据库记录。
2. 真正防伪的是最后一段随机 `secret`，服务端不会保存明文，只保存完整 RefreshToken 的 SHA256 哈希。
3. 攻击者即使知道 `tenantId/userId/tokenId`，没有 `secret` 也无法构造出匹配 `TokenHash` 的 RefreshToken。

### 3. 服务端存储模型

RefreshToken 存储在租户库 `RefreshTokens` 表，对应实体字段：

| 字段 | 说明 |
| --- | --- |
| `TenantId` | 租户 ID |
| `UserId` | 用户 ID |
| `StoreId` | RefreshToken 对应的当前门店上下文 |
| `SessionId` | 会话 ID，和 AccessToken 中的 `SessionId` 对应 |
| `TokenHash` | 明文 RefreshToken 的 SHA256 哈希 |
| `ExpiresAtUtc` | RefreshToken 过期时间 |
| `RevokedAtUtc` | 撤销时间，非空表示不可再使用 |
| `RevokedReason` | 撤销原因，例如 `Rotated`、`Logout`、`SwitchStore`、`PasswordChanged` |
| `CreatedByIp` | 签发或轮换时的 IP |
| `UserAgent` | 签发或轮换时的 User-Agent |
| `ReplacedByTokenId` | 轮换后的新 RefreshToken ID |

`IsActive(now)` 的判断非常简单：`RevokedAtUtc == null && ExpiresAtUtc > now`。因此只要过期或被撤销，RefreshToken 就不能再换发。

### 4. 登录时如何签发 RefreshToken

登录成功后，服务端先构造 `TokenInfo`，再生成 AccessToken 和 RefreshToken：

```text
登录成功
  -> TokenInfo.Create(UserId, UserName, TenantId, StoreId, ExpiresAt, SessionId, TokenVersion)
  -> GenerateToken(TokenInfo) 生成 AccessToken
  -> RefreshTokenService.IssueAsync(TokenInfo, ipAddress, userAgent)
  -> RefreshTokens 表保存哈希、TenantId、UserId、StoreId、SessionId、过期时间
  -> 返回 AccessToken + RefreshToken 明文
```

关键点：

1. RefreshToken 和本次 AccessToken 使用同一个 `SessionId`。
2. RefreshToken 保存了当前登录门店 `StoreId`。
3. 登录成功返回的 RefreshToken 明文只出现一次，客户端必须保存好；服务端无法从哈希还原明文。

### 5. 刷新接口

入口：

```http
POST /api/User/refresh-token
```

请求体：

```json
{
  "refreshToken": "rt.1.10001.987654321.xxxxx"
}
```

该接口标记 `[AllowAnonymous]`，因为它不要求当前 AccessToken 仍然有效；RefreshToken 自身就是换发凭据。

成功响应当前复用 `LoginResponse`，关键字段是：

```json
{
  "success": true,
  "token": "新的AccessToken",
  "refreshToken": "新的RefreshToken",
  "expiresIn": 86400,
  "expiresAt": "新的AccessToken过期时间"
}
```

注意：刷新接口当前只返回新 Token 相关字段，不重新返回 `User`、`CurrentStore`、`AccessibleStores`。前端如需刷新完整用户/门店信息，应在刷新成功后继续调用对应查询接口，或保留登录时返回的门店信息。

### 6. RefreshToken 换发流程

`RefreshTokenService.ExchangeAsync(...)` 的核心流程：

```text
客户端提交 RefreshToken
  -> TryParse 检查 rt.tenantId.userId.tokenId.secret 格式
  -> 根据 tenantId/userId/tokenId/TokenHash 查询 RefreshTokens 表
  -> 检查 RefreshToken 是否未过期且未撤销
  -> 检查 SessionId 是否仍有效（不在 Session 黑名单）
  -> 回查 Users，确认用户存在、未删除且 Active
  -> 将用户当前 TokenVersion 写入缓存
  -> 用 RefreshToken 保存的 StoreId 生成新的 AccessToken
  -> 生成 replacement RefreshToken
  -> 旧 RefreshToken 标记 RevokedAtUtc、RevokedReason = Rotated、ReplacedByTokenId = 新TokenId
  -> 保存并返回新的 AccessToken + 新的 RefreshToken
```

这是一种 **RefreshToken Rotation（轮换）** 设计：每个 RefreshToken 成功使用一次后就立即失效。客户端必须用响应中的新 RefreshToken 覆盖旧值。

### 7. 为什么要轮换 RefreshToken

轮换的主要价值是降低 RefreshToken 泄露后的持续风险：

| 场景 | 行为 |
| --- | --- |
| 正常客户端刷新 | 旧 RefreshToken 被标记 `Rotated`，客户端保存新 RefreshToken |
| 客户端继续误用旧 RefreshToken | 因旧 Token 已撤销，返回 401 |
| 攻击者拿到旧 RefreshToken 后稍后使用 | 因旧 Token 已撤销，无法换发 |
| 攻击者和客户端几乎同时使用同一个 RefreshToken | 先成功的一方会轮换，后到的一方失败；当前实现会拒绝后到请求，但尚未实现“复用检测后撤销整条 Token family”的升级策略 |

前端实现上必须注意：

1. 同一时间只允许一个刷新请求在飞行中，避免多个并发请求同时拿同一个 RefreshToken 换发。
2. 刷新成功后必须原子替换本地 AccessToken 和 RefreshToken。
3. 刷新失败时应清理本地 Token 并跳转登录页，而不是继续重试旧 RefreshToken。

### 8. RefreshToken 与门店切换的关系

门店切换不是用旧 RefreshToken 改写门店，而是用当前有效 AccessToken 发起：

```http
POST /api/User/switch-store
Authorization: Bearer {当前AccessToken}
```

切换成功后服务端会：

1. 根据当前用户校验目标门店是否在 `UserStores` 授权范围内。
2. 生成新的 `TokenInfo`，其中 `StoreId = 目标门店ID`，并生成新的 `SessionId`。
3. 签发新的 AccessToken 和新的 RefreshToken。
4. 将旧 AccessToken 的 `SessionId` 加入黑名单。
5. 撤销旧 `SessionId` 下所有未撤销的 RefreshToken，撤销原因为 `SwitchStore`。

因此：

- 切换门店后，旧 AccessToken 会失效。
- 切换门店后，旧 RefreshToken 也会失效，不能再换回旧门店上下文。
- 前端必须同时替换 AccessToken 和 RefreshToken。

### 9. RefreshToken 撤销场景

当前 RefreshToken 撤销分为按 Session 撤销和按用户撤销。

| 场景 | 撤销方式 | 撤销原因 | 结果 |
| --- | --- | --- | --- |
| 刷新成功 | 当前 RefreshToken 被撤销 | `Rotated` | 旧 RefreshToken 单次使用后失效 |
| 退出登录 | `RevokeSessionAsync(tenantId, sessionId, "Logout")` | `Logout` | 当前会话不能继续刷新 |
| 切换门店 | `RevokeSessionAsync(tenantId, previousSessionId, "SwitchStore")` | `SwitchStore` | 旧门店上下文的刷新链路失效 |
| 修改密码 | `RevokeUserAsync(tenantId, userId, "PasswordChanged")` | `PasswordChanged` | 该用户所有 RefreshToken 失效 |
| 管理员重置密码 | `RevokeUserAsync(tenantId, userId, "PasswordReset")` | `PasswordReset` | 该用户所有 RefreshToken 失效 |
| 强制下线 | 递增 `TokenVersion`、标记登录日志、拉黑会话，并撤销该用户 RefreshToken | `ForceLogout` | 旧 AccessToken/RefreshToken 都不能继续使用 |
| 门店授权变更 | 递增 `TokenVersion` 并清理权限缓存；建议同时撤销用户 RefreshToken | `AssignStores` | 避免用户继续持有被撤销门店上下文 |

说明：当前代码中修改密码和重置密码已经显式撤销用户所有 RefreshToken；退出登录和切换门店已经显式撤销 Session 下 RefreshToken。门店授权变更当前会递增 `TokenVersion`，会使旧 AccessToken 失效；为了彻底避免旧 RefreshToken 在授权变更后换发新 Token，建议未来在 `AssignStoresAsync` 中补充 `RevokeUserAsync(..., "AssignStores")`。

### 10. RefreshToken 与 TokenVersion 的关系

`TokenVersion` 是用户级 AccessToken 版本号，存储在 `Users.TokenVersion`。AccessToken 内也携带一份 `TokenVersion`。

- AccessToken 请求时：如果缓存/数据库中的用户当前版本与 Token 内版本不一致，请求返回 401。
- RefreshToken 换发时：服务端会回查用户并使用用户当前 `TokenVersion` 生成新的 AccessToken。

因此，`TokenVersion` 主要解决“旧 AccessToken 继续访问 API”的问题；RefreshToken 撤销主要解决“旧会话继续换新 AccessToken”的问题。安全事件发生时通常应该两者配合：

```text
修改密码/重置密码/强制下线/高风险授权变更
  -> Users.TokenVersion++
  -> 更新 TokenVersion 缓存
  -> 拉黑已有 SessionId
  -> 撤销 RefreshToken
```

### 11. RefreshToken 客户端建议

客户端推荐策略：

1. AccessToken 放在内存或安全存储中；浏览器场景避免长期放在可被脚本读取的位置。
2. RefreshToken 也应按高敏感凭据处理，避免日志输出、URL 传递、埋点上报。
3. 遇到 401 时先判断是否已有刷新请求；如已有则等待同一个刷新结果，不要并发刷新。
4. 刷新成功后重放原请求。
5. 刷新失败后清理本地登录态并跳转登录页。
6. 切换门店、退出登录、修改密码后必须清理旧 Token。

## 六、门店切换流程

入口：

```http
POST /api/User/switch-store
Authorization: Bearer {旧AccessToken}
```

请求体：

```json
{
  "storeId": 110101
}
```

服务端流程：

1. 从旧 AccessToken 解析当前用户 `UserId`。
2. 查询用户信息。
3. 查询 `UserStores`，确认目标门店在该用户授权范围内且门店启用。
4. 生成一个新的 AccessToken，新 Token 的 `StoreId` 等于目标门店，并产生新的 `SessionId`。
5. 签发新的 RefreshToken。
6. 记录切换门店操作日志。
7. 将旧 Token 的 `SessionId` 加入失效黑名单。
8. 撤销旧 `SessionId` 下 RefreshToken。
9. 返回新 AccessToken、新 RefreshToken 和新的当前门店。

关键点：

```text
切换门店不是修改用户归属，而是更换当前操作上下文。
前端收到 switch-store 返回值后，必须同时替换本地保存的 access token 和 refresh token。
```

旧 AccessToken 被拉黑后，再使用旧 AccessToken 请求接口会得到 401。旧 RefreshToken 被撤销后，也不能继续换取旧门店上下文的新 AccessToken。

## 七、功能授权机制

### 1. API 授权入口

示例 WebApi 中的用户接口使用 ASP.NET Core 授权属性：

| 接口 | 授权要求 |
| --- | --- |
| `POST /api/User/login` | `[AllowAnonymous]` |
| `POST /api/User/refresh-token` | `[AllowAnonymous]` |
| `POST /api/User/logout` | `RequireIdentitySelf` |
| `POST /api/User/switch-store` | `RequireIdentitySelf` |
| 用户创建/更新/删除/状态/强制下线 | `RequireUsersManage` |
| 用户查询/会话查询 | `RequireUsersRead` |
| 角色分配 | `RequireRolesManage` |
| 审计日志查询 | `RequireAuditRead` |

`AuthorizationPolicies` 约定权限策略名称前缀为：

```text
Permission:{permissionCode}
```

例如：

```csharp
[Authorize(Policy = AuthorizationPolicies.RequireUsersManage)]
```

本质上会走 `PermissionAuthorizationPolicyProvider`，转换为 `PermissionRequirement`，然后由 `PermissionAuthorizationHandler` 调用 `IPermissionChecker`。

### 2. RBAC 权限计算

当前 `IPermissionChecker` 的实现是 `RbacPermissionService`。校验流程：

1. 从 Claims 读取 `uid`、`tid`、`sid`。
2. 回查用户，用户必须存在、未删除且状态为 Active。
3. `identity.self` 这类基础权限只检查租户权益是否可用。
4. `SystemAdmin` / `TenantAdmin` 兼容路径：不要求显式角色行，但仍要通过租户权益校验。
5. 普通用户读取 `UserRoles`：
   - `StoreId = 0` 表示租户级角色；
   - `StoreId = 当前门店` 表示当前门店角色。
6. 读取启用的 `Roles`：
   - `Role.StoreId = null` 表示租户级角色；
   - `Role.StoreId = 当前门店` 表示门店级角色。
7. 读取 `RolePermissions`，先收集 `Deny`，再收集没有被 Deny 覆盖的 `Allow`。
8. 读取启用的 `Permissions` 并归一化权限码。
9. 最后通过 `IEntitlementService` 做套餐/权益裁剪，确保角色授予的权限也必须在租户当前权益范围内。

权限结果会缓存 5 分钟，并带一个用户级权限缓存版本。角色或门店授权变化时应调用 `InvalidateUserPermissionsAsync` 更新版本。

### 3. TenantAdmin 策略

`RequireTenantAdmin` 不是完全相信 Token 中的角色声明，而是回查用户是否仍是 Active 且 `Type` 为 `TenantAdmin` 或 `SystemAdmin`；如果不是，再尝试检查 `tenant.admin` 权限。这可以避免用户被禁用、删除或降权后旧 Token 仍然通过管理员授权。

## 八、Token 主动失效控制

当前有三类失效机制：

| 机制 | 作用 | 使用场景 |
| --- | --- | --- |
| Session 黑名单 | 只让某个 AccessToken/Session 失效 | 退出登录、切换门店、强制下线 |
| TokenVersion | 让某个用户所有旧 AccessToken 失效 | 修改密码、重置密码、强制下线、重新分配门店 |
| RefreshToken 撤销 | 阻止旧会话继续换取新 AccessToken | 退出登录、切换门店、修改密码、重置密码、强制下线 |

### 1. Session 黑名单

`ITokenCacheService.InvalidateSession(sessionId)` 会把 Session 写入失效缓存。AccessToken 校验和 `TokenVersionValidationMiddleware` 都会检查该 Session 是否有效。

适合场景：

- 当前会话退出登录；
- 切换门店后废弃旧门店上下文；
- 管理员踢掉某个会话。

### 2. TokenVersion

`Users.TokenVersion` 是用户级版本号。只要递增版本号，所有携带旧版本的 AccessToken 都会失效。

适合场景：

- 修改密码；
- 管理员重置密码；
- 管理员强制下线；
- 门店授权变更；
- 高风险角色/权限变化。

### 3. RefreshToken 撤销

RefreshToken 撤销负责防止旧会话继续刷新。仅递增 `TokenVersion` 并不能阻止旧 RefreshToken 换出“带最新 TokenVersion 的新 AccessToken”，所以涉及账户安全或授权范围变化时，建议同时撤销 RefreshToken。

## 九、数据范围如何计算

核心服务是 `DataScope`：

```text
src/Atlas.Data.Tenant/DataScope.cs
```

`DataScope` 从 `ICurrentIdentity` 读取：

```text
TenantId
StoreId
```

然后根据当前 `StoreId` 查询 `Stores`，计算 `ShareStoreIds`。

规则如下：

| 当前门店类型 | 共享数据可见门店 |
| --- | --- |
| `Headquarters` | 当前总部 + 所有下级直营店 |
| `FranchiseHeadquarters` | 当前加盟总部 + 所有下级直营店 |
| `DirectOperated` | 父总部 + 同父级所有直营店 |
| `Franchised` | 当前加盟店自己 |
| 未知类型 | 当前门店自己 |

例如本地种子数据：

| 当前门店 | 类型 | `ShareStoreIds` |
| --- | --- | --- |
| `110001` 总部 | `Headquarters` | `110001, 110011, 110012` |
| `110011` 直营一店 | `DirectOperated` | `110001, 110011, 110012` |
| `110101` 加盟一店 | `Franchised` | `110101` |

`DataScope.ResolveAsync()` 会返回一个快照：

```csharp
public sealed record DataScopeSnapshot(
    long? TenantId,
    long? StoreId,
    IReadOnlyList<long> ShareStoreIds);
```

这个快照会被仓储层用于构造查询表达式。同一请求内如果 `TenantId` 和 `StoreId` 不变，`DataScope` 会复用已解析的快照，减少重复计算。

## 十、查询隔离是否已经由底层框架完成

结论：对通过标准仓储入口发起的查询，底层框架已经做了隔离。

标准入口包括：

```csharp
await repository.QueryAsync();
await repository.QueryTrackingAsync();
await repository.QueryDataScopeAsync(resourceCode, scopeType);
await db.ScopedSet<TEntity>(scope);
```

核心链路：

```text
Controller/Service
  -> IRepository<TEntity>.QueryAsync()
  -> RepositoryBase.QueryAsync()
  -> IDataScope.ResolveAsync()
  -> AtlasTenantDbContext.ScopedSet<TEntity>(scope)
  -> EntityScopeFilter<TEntity>.Apply(...)
  -> EF Core Where 表达式
```

也就是说，业务代码通常不需要每次手写：

```sql
WHERE TenantId = ...
WHERE StoreId = ...
WHERE StoreId IN (...)
```

这些由 `EntityScopeFilter` 根据实体实现的接口自动加上。

需要注意的边界：

| 情况 | 是否自动隔离 |
| --- | --- |
| 使用 `RepositoryBase.QueryAsync()` | 是 |
| 使用 `RepositoryBase.QueryTrackingAsync()` | 是 |
| 使用 `RepositoryBase.QueryDataScopeAsync(...)` | 是，且会额外叠加 RBAC 数据范围谓词 |
| 使用 `db.ScopedSet<TEntity>(scope)` | 是 |
| 直接使用 `db.Set<TEntity>()` | 否，业务/API 层已由 Analyzer 阻断；基础设施内部使用时必须显式限定 `TenantId` |
| 原生 SQL / `FromSqlRaw` | 否，租户库写 SQL 必须走 `ITenantSqlExecutor` 命名方法 |
| 实体没有实现任何范围接口 | 不做租户/门店隔离 |

## 十一、表类型如何表达

数据隔离不是靠表名判断，而是靠实体实现的接口判断。

### 1. 集团共享表

集团共享，指租户内所有门店都可见的数据。

实现方式：实体只实现 `ITenantEntity`，不要实现 `ISharedEntity` 或 `IStoreOnlyEntity`。

过滤规则：

```sql
WHERE TenantId = 当前TenantId
```

适合：

| 表/实体 | 说明 |
| --- | --- |
| `Store` | 门店基础资料 |
| `User` | 集团级用户 |
| `UserStore` | 用户可操作门店授权 |
| `UserRole` | 用户角色关系 |
| `Role` / `Permission` / `RolePermission` | RBAC 元数据和授权关系 |
| `RefreshToken` | 用户刷新令牌，按租户隔离 |
| `OperationLog` | 租户级操作日志，业务层可再按门店筛选 |

注意：如果某张集团共享表既需要 `StoreId` 作为创建来源，又希望全集团可见，当前没有单独的 `IGroupSharedEntity` 标记。此时不要直接继承 `SharedEntity` 或 `StoreOnlyEntity`，否则会被当成直营共享或门店独享。后续可以新增一个明确的 `IGroupSharedEntity` 来表达这种情况。

### 2. 直营共享表

直营共享，指总部和直营门店之间按组织体系共享的数据。

实现方式：实体实现 `ISharedEntity`，通常继承：

```csharp
SharedEntity
SharedVersionedEntity
```

过滤规则：

```sql
WHERE TenantId = 当前TenantId
  AND StoreId IN (ShareStoreIds)
```

适合：

| 表/实体 | 当前示例 |
| --- | --- |
| 商品 | `Product : SharedVersionedEntity` |
| 会员 | `Member : SharedVersionedEntity` |
| 促销 | `Promotion : SharedVersionedEntity` |

可见性示例：

| 当前门店 | 可见商品 |
| --- | --- |
| 总部 `110001` | 总部商品 + 直营一店商品 + 直营二店商品 |
| 直营一店 `110011` | 总部商品 + 直营一店商品 + 直营二店商品 |
| 加盟一店 `110101` | 加盟一店商品 |

加盟店虽然也是 `ISharedEntity` 查询路径，但它的 `ShareStoreIds` 只有自己，所以效果是加盟店独享。

### 3. 门店独有表

门店独有，指无论总部还是直营店，都只能看当前操作门店自己的数据。

实现方式：实体实现 `IStoreOnlyEntity`，通常继承：

```csharp
StoreOnlyEntity
StoreOnlyVersionedEntity
```

过滤规则：

```sql
WHERE TenantId = 当前TenantId
  AND StoreId = 当前StoreId
```

适合：

| 表/实体 | 当前示例 |
| --- | --- |
| 订单 | `Order : StoreOnlyVersionedEntity` |
| 库存 | `Inventory : StoreOnlyEntity` |
| 收银记录 | `CashierRecord : StoreOnlyEntity` |

可见性示例：

| 当前门店 | 可见库存 |
| --- | --- |
| 总部 `110001` | 总部库存 |
| 直营一店 `110011` | 直营一店库存 |
| 加盟一店 `110101` | 加盟一店库存 |

即使总部能看到直营共享商品，也不会自动看到直营门店库存。商品和库存属于不同数据分类。

## 十二、写入时如何保证 TenantId 和 StoreId

查询隔离解决的是读取问题。写入时，当前框架通过 `AuditInterceptor` 做默认填充：

| 实体接口 | 新增时自动填充 |
| --- | --- |
| `ITenantEntity` | `TenantId = 当前Token.TenantId` |
| `IStoreEntity` | `StoreId = 当前Token.StoreId` |
| `IAuditable` | `CreatedBy = 当前UserId` |
| `IBaseEntity` | `CreatedAt = 当前时间` |
| `ISnowflakeId` | 自动生成雪花 ID |

因此，业务接口在某个门店上下文下创建 `Product`、`Inventory` 等数据时，默认会落到当前 Token 的 `StoreId`。

如果是登录、租户初始化、RefreshToken 换发这类没有完整请求 Token 上下文的场景，需要使用显式 `tenantId` 的仓储方法：

```csharp
repository.QueryAsync(tenantId)
repository.AddAsync(entity, tenantId)
unitOfWork.SaveChangesAsync(tenantId)
```

## 十三、直营共享如何实现

以商品 `Product` 为例：

```csharp
public class Product : SharedVersionedEntity, ISnowflakeId
{
    public string Name { get; set; }
    public decimal Price { get; set; }
    public string Description { get; set; }
    public long? SourceStoreId { get; set; }
    public bool IsCustomized { get; set; }
}
```

因为 `Product` 继承了 `SharedVersionedEntity`，所以它实现了 `ISharedEntity`。

查询商品时，仓储层会自动变成类似逻辑：

```sql
SELECT *
FROM Products
WHERE TenantId = 当前TenantId
  AND StoreId IN (当前门店的ShareStoreIds)
```

当当前门店是总部 `110001`：

```text
ShareStoreIds = [110001, 110011, 110012]
```

所以总部能看到总部商品和直营店商品。

当当前门店是加盟一店 `110101`：

```text
ShareStoreIds = [110101]
```

所以加盟店只能看到自己的商品。

## 十四、集团级用户和门店上下文的关系

集团级用户的授权模型是：

```text
Users
  1:N
UserStores
  N:1
Stores
```

`UserStores` 只决定用户可切换哪些门店，不直接决定本次查询的数据范围。

本次查询的数据范围由 AccessToken 中的 `StoreId` 决定：

```text
登录或切换门店
  -> 生成包含 StoreId 的 AccessToken
  -> 请求时解析 StoreId 到 CurrentIdentity
  -> DataScope 根据 StoreId 计算 ShareStoreIds
  -> Repository 自动加查询过滤
```

这样做的好处是：

1. 用户可以是集团级的，不被固定到一个门店。
2. 前端有明确的当前门店上下文。
3. 数据层不需要知道用户授权了多少门店，只需要知道当前操作门店。
4. 权限变更后可以通过 TokenVersion 让旧 AccessToken 失效。
5. 会话变化后可以通过 Session 黑名单和 RefreshToken 撤销阻断旧上下文继续使用。

## 十五、本地验证方式

### 1. 登录总部账号

```http
POST http://localhost:5212/api/User/login
Content-Type: application/json
```

```json
{
  "domain": "demo",
  "userName": "hq_admin",
  "password": "Pass1234!",
  "rememberMe": false
}
```

预期：

```text
success = true
currentStore = 110001 总部
accessibleStores = 110001, 110011, 110012, 110101
返回 token 和 refreshToken
```

### 2. 使用 RefreshToken 换发

```http
POST http://localhost:5212/api/User/refresh-token
Content-Type: application/json
```

```json
{
  "refreshToken": "{登录或上次刷新返回的refreshToken}"
}
```

预期：

```text
success = true
返回新的 token
返回新的 refreshToken
旧 refreshToken 再次使用会返回 401
```

### 3. 查看总部上下文的数据范围

```http
GET http://localhost:5212/api/scope-demo/visibility
Authorization: Bearer {登录返回Token}
```

预期：

```text
currentStore = 110001
resolvedSharedStoreIds = 110001, 110011, 110012
visibleSharedProducts = 3
visibleStoreOnlyInventories = 1
```

### 4. 切换到加盟一店

```http
POST http://localhost:5212/api/User/switch-store
Authorization: Bearer {登录返回Token}
Content-Type: application/json
```

```json
{
  "storeId": 110101
}
```

预期：

```text
success = true
currentStore = 110101 加盟一店
返回新的 token
返回新的 refreshToken
旧 token 失效
旧 refreshToken 失效
```

### 5. 使用新 Token 查看加盟店数据范围

```http
GET http://localhost:5212/api/scope-demo/visibility
Authorization: Bearer {切换门店返回的新Token}
```

预期：

```text
currentStore = 110101
resolvedSharedStoreIds = 110101
visibleSharedProducts = 1
visibleStoreOnlyInventories = 1
```

### 6. 退出登录

```http
POST http://localhost:5212/api/User/logout
Authorization: Bearer {当前Token}
```

预期：

```text
当前 sessionId 加入黑名单
当前 sessionId 下 refreshToken 被撤销
再次使用当前 token 请求返回 401
再次使用当前 refreshToken 换发返回 401
```

## 十六、当前实现边界和建议

1. 数据隔离依赖实体接口。新表必须正确选择 `ITenantEntity`、`ISharedEntity`、`IStoreOnlyEntity`。
2. 直接 `db.Set<TEntity>()` 查询不会自动套范围，业务/API 层不得使用；基础设施代码必须显式限定 `TenantId`。
3. 原生 SQL 不会自动隔离，租户库写 SQL 必须通过 `ITenantSqlExecutor` 命名方法，并由 executor 生成带 `TenantId` 谓词的最终语句。
4. 当前没有独立的 `IGroupSharedEntity`。集团共享表建议只实现 `ITenantEntity`。如果业务需要“有 StoreId 归属但全集团可见”的表，建议新增专门标记接口。
5. `DataScope` 计算的是当前门店的业务共享范围，不是用户所有授权门店范围。用户所有授权门店只用于门店选择和切换。
6. RefreshToken 当前已实现单次轮换，但尚未实现“检测到旧 RefreshToken 复用后撤销整条 Token family”。如果未来对盗用检测要求更高，可增加 `FamilyId` / `ParentTokenId` 并在复用时撤销整个族。
7. RefreshToken 刷新接口当前复用 `LoginResponse`，但只填充 Token 相关字段；前端不应假设刷新响应包含完整用户和门店列表。
8. 门店授权变更当前会递增 `TokenVersion` 并清理权限缓存。为避免旧 RefreshToken 在授权变更后换发新 Token，建议同步撤销目标用户 RefreshToken。
9. QueryString Token 默认关闭是正确的；如为 WebSocket/SSE 打开，需要避免在日志、Referer、代理访问日志中泄露 `access_token`。
10. AccessToken 和 RefreshToken 都应避免写入业务日志、异常日志、URL、埋点和第三方监控明文。

## 十七、代码位置索引

| 主题 | 文件 |
| --- | --- |
| 用户相关 API，包括登录、刷新、退出、切换门店、分配门店/角色 | `samples/Atlas.Sample.WebApi/Controllers/UserController.cs` |
| 登录、刷新、切换门店、登出、用户门店授权 | `src/Atlas.Services/UserService.cs` |
| 登录请求/刷新请求模型 | `src/Atlas.Models.Tenant/Requests/CreateUserRequest.cs` |
| 切换门店请求模型 | `src/Atlas.Models.Tenant/Requests/SwitchStoreRequest.cs` |
| 登录响应模型 | `src/Atlas.Models.Tenant/Responses/UserPagedResponse.cs` |
| 切换门店响应模型 | `src/Atlas.Models.Tenant/Responses/SwitchStoreResponse.cs` |
| Token 数据结构 | `src/Atlas.Infrastructure.Security/TokenInfo.cs` |
| AccessToken 生成和验证 | `src/Atlas.Infrastructure.Security/CustomTokenService.cs` |
| Token 认证处理器 | `src/Atlas.Infrastructure.Security/CustomTokenAuthenticationHandler.cs` |
| Token 缓存、Session 黑名单、TokenVersion 缓存 | `src/Atlas.Infrastructure.Security/TokenCacheService.cs` |
| TokenVersion 二次校验中间件 | `src/Atlas.Infrastructure.Security/TokenVersionValidationMiddleware.cs` |
| RefreshToken 签发、轮换、撤销 | `src/Atlas.Infrastructure.Security/RefreshTokenService.cs` |
| RefreshToken 实体 | `src/Atlas.Core/Entities/Tenant/SecurityModels.cs` |
| 当前身份解析 | `src/Atlas.Data.Tenant/Identity/CurrentIdentity.cs` |
| 权限策略名称 | `src/Atlas.Infrastructure.Security/AuthorizationPolicies.cs` |
| Permission 策略 Provider/Handler | `src/Atlas.Infrastructure.Security/Permissions/PermissionAuthorization.cs` |
| RBAC 权限检查 | `src/Atlas.Infrastructure.Security/Permissions/RbacPermissionService.cs` |
| 数据范围服务 | `src/Atlas.Data.Tenant/DataScope.cs` |
| 范围过滤器 | `src/Atlas.Data.Tenant/EntityScopeFilter.cs` |
| 仓储统一查询入口 | `src/Atlas.Data.Tenant/Repositories/RepositoryBase.cs` |
| ScopedSet 入口 | `src/Atlas.Data.Tenant/Context/AtlasTenantDbContext.cs` |
| SQL 安全入口 | `src/Atlas.Data.Tenant/Sql/TenantSqlExecutor.cs` |
| 实体范围接口 | `src/Atlas.Core/Entities/Interfaces/IEntity.cs` |
| 共享/独享实体基类 | `src/Atlas.Core/Entities/Base/BaseEntity.cs` |
| 本地数据初始化 | `tools/Atlas.LocalSetup/Program.cs` |
| 数据范围演示接口 | `samples/Atlas.Sample.WebApi/Controllers/ScopeDemoController.cs` |
