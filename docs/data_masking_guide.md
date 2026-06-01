# Atlas 数据脱敏指南

Atlas 提供框架级 API 响应脱敏能力。普通业务接口默认输出脱敏值；需要查看明文时，业务模块必须提供专用 Reveal 接口，并通过框架 Reveal 执行器完成权限校验和审计。

## 总开关

```json
{
  "DataMasking": {
    "Enabled": true
  }
}
```

`DataMasking:Enabled` 默认值为 `true`。当开关关闭时，普通 API 响应不再自动脱敏，但 Reveal 接口仍然必须执行权限校验和审计。

## 标注 DTO 字段

```csharp
public sealed class CustomerDto
{
    [SensitiveData(MaskKind.Phone)]
    public string? Phone { get; set; }

    [SensitiveData(MaskKind.Email)]
    public string? Email { get; set; }
}
```

框架会在 MVC 响应写出前对标注字段进行脱敏。业务服务内部仍可以使用原始 DTO，避免缓存或内部流程被脱敏值污染。

## 跳过默认脱敏

只有专用 Reveal 接口允许使用 `[DisableDataMasking]`：

```csharp
[DisableDataMasking]
[Authorize(Policy = AuthorizationPolicies.PermissionPrefix + AtlasPermissionCodes.UsersSensitiveReveal)]
[HttpPost("{userId:long}/sensitive-fields/reveal")]
public Task<RevealSensitiveFieldsResponse> RevealSensitiveFields(...)
```

Reveal 接口必须通过 `ISensitiveDataRevealExecutor` 执行，不能直接读取明文后返回。

## Reveal 接口规则

1. 普通列表、详情接口即使用户有 Reveal 权限，也默认返回脱敏值。
2. 明文查看必须调用业务专用 Reveal 接口。
3. Reveal 请求必须包含 `fields` 和 `reason`。
4. 业务模块必须维护字段白名单，禁止万能 Reveal Controller。
5. Reveal 成功和失败都必须记录审计。
6. 审计日志只记录字段名、对象、原因、工单、结果等元数据，禁止记录明文敏感值。
7. Reveal 响应应设置 `Cache-Control: no-store` 和 `Pragma: no-cache`。

## 审计内容示例

`OperationLog.Changes` 推荐内容：

```json
{
  "operation": "SensitiveDataReveal",
  "entityType": "User",
  "entityId": 10001,
  "fields": ["phone", "email"],
  "reason": "客服核验用户身份",
  "ticketNo": "CS-001",
  "result": "Success"
}
```

禁止记录：

```json
{
  "phone": "13812345678",
  "email": "user@example.com"
}
```
