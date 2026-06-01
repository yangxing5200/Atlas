# Atlas Observability Guide

Atlas 通过 `AddAtlasCore()` 注册 OpenTelemetry，默认关闭，生产或排障环境通过配置打开。

## 配置

```json
{
  "Observability": {
    "OpenTelemetry": {
      "Enabled": true,
      "ServiceName": "Atlas.WebApi",
      "Exporter": "Console",
      "OtlpEndpoint": "http://otel-collector:4317",
      "OtlpProtocol": "Grpc",
      "InstrumentRuntime": true
    }
  }
}
```

`Exporter` 支持 `None`、`Console`、`Otlp`。`OtlpProtocol` 支持 `Grpc`、`HttpProtobuf`、`Http/Protobuf`、`Http`。

## Trace 覆盖

启用后会采集：

- ASP.NET Core 请求 span。
- `HttpClient` 出站调用 span。
- MassTransit activity source。
- Atlas 自定义后台任务 span：`atlas.background_job.execute`。
- Atlas tenant outbox 发布 span：`atlas.tenant_outbox.publish`。
- Atlas tenant event 消费 span：`atlas.tenant_event.consume`。

HTTP 请求、消息发布、消息消费和后台任务执行都使用同一个 `Atlas` activity source 作为业务 span，方便从基础设施 span 进入业务执行点。

## 日志关联

`LogContextMiddleware` 会把 `TraceId` 和 `SpanId` 写入 Serilog LogContext。日志中同时保留原有 `CorrelationId`、`OperationId`、租户、门店和用户上下文。

## 敏感数据规则

框架包只依赖稳定版 OpenTelemetry 包，默认不引入仍处于 prerelease 的 EF Core 或 Redis instrumentation。需要采集数据库或 Redis 细节时，由宿主应用显式评估并引入对应 instrumentation 包，避免框架稳定包间接携带 prerelease 依赖。

## 本地验证

Console exporter：

```powershell
$env:Observability__OpenTelemetry__Enabled="true"
$env:Observability__OpenTelemetry__Exporter="Console"
dotnet run --project src\Atlas.WebApi\Atlas.WebApi.csproj
```

OTLP exporter：

```powershell
$env:Observability__OpenTelemetry__Enabled="true"
$env:Observability__OpenTelemetry__Exporter="Otlp"
$env:Observability__OpenTelemetry__OtlpEndpoint="http://localhost:4317"
dotnet run --project src\Atlas.Worker\Atlas.Worker.csproj
```

验证标准：

1. WebApi 日志包含 `TraceId`。
2. 一个 HTTP 请求触发的消息发布和消费在同一 trace 下可关联。
3. BackgroundJob 执行产生 `atlas.background_job.execute` span。
