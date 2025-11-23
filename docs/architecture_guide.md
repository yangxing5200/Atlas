# Atlas 项目结构指南（Architecture Guide）

本文档用于指导 Atlas 系统内每个项目（模块）应该放置什么内容、承担什么职责，以保持整个项目结构可维护、清晰、可扩展。

---

# 📚 目录

1. [总体架构概览](#总体架构概览)
2. [项目结构说明](#项目结构说明)
   - [1. Core](#1-core)
   - [2. Models](#2-models)
   - [3. Data 层](#3-data-层)
     - [3.1 Data.Abstractions](#31-atlasdataabstractions)
     - [3.2 Data.Common](#32-atlasdatacommon)
     - [3.3 Data.Global](#33-atlasdataglobal)
     - [3.4 Global.Migrations](#34-atlasdataglobalmigrations)
     - [3.5 Data.Tenant](#35-atlasdatatenant)
     - [3.6 Tenant.Migrations](#36-atlasdatatenantmigrations)
   - [4. Services 层](#4-services-层)
     - [4.1 Services.Abstractions](#41-atlasservicesabstractions)
     - [4.2 Services](#42-atlasservices)
   - [5. Infrastructure](#5-infrastructure)
   - [6. Messaging](#6-messaging)
   - [7. Extensions](#7-extensions)
   - [8. Application 层](#8-application-层)
3. [依赖原则](#依赖原则)
4. [最佳实践](#最佳实践)

---

# 总体架构概览

Atlas 采用 **Clean Architecture + DDD + 企业级分层模式**，结构如下：

- 越底层越独立，越上层依赖越多  
- 抽象在上，实现向下  
- 业务逻辑在 Services  
- API / gRPC / 后台任务在 Application

---

# 项目结构说明

---

# 1. Core

📁 **Atlas.Core**  
系统的最底层，不依赖任何其他项目。

### ✔ 应放内容：
- 领域实体（Entity）
- 值对象（ValueObject）
- 通用接口（ISoftDelete、IHasId 等）
- 领域事件（纯抽象）
- 基础异常类型
- 与业务无关的工具类（不依赖外部库）

### ❌ 不应放：
- EF Core 相关内容  
- AutoMapper  
- 业务逻辑  
- DTO、ViewModels

---

# 2. Models

📁 **Atlas.Models**  
跨层共享的数据结构（主要用于 API、Service、Mapping）。

### ✔ 应放内容：
- DTO / ViewModel
- Query / Command 模型
- PagedResult、Result 等通用模型
- 公共枚举

### ❌ 不应放：
- 领域实体（应该放 Core）
- AutoMapper Profiles（应该放 Services）
- 数据访问逻辑

---

# 3. Data 层

Atlas.Data 负责数据访问、ORM 实现、仓储和数据库上下文。

## 3.1 Atlas.Data.Abstractions

👉 数据访问抽象层，不包含任何实现。

### ✔ 应放：
- IRepository<T>  
- IUnitOfWork  
- IDataScopeProvider（抽象）  
- 数据过滤规则抽象（如 IEntityScope）  

### ❌ 不放：
- EF Core DbContext  
- SQL 查询  
- 任何数据库实现

---

## 3.2 Atlas.Data.Common

### ✔ 应放：
- 多项目共享的数据层工具类
- 查询扩展方法
- 数据范围过滤（如果不依赖 EF）
- 分页帮助类

---

## 3.3 Atlas.Data.Global

### ✔ 应放：
- GlobalDbContext（EF 实现）
- Global 数据仓储实现（EF Repository）
- 全局数据库逻辑

---

## 3.4 Atlas.Data.Global.Migrations

存放 GlobalDbContext 的 EF Migration 文件。

---

## 3.5 Atlas.Data.Tenant

### ✔ 应放：
- TenantDbContext（EF 实现）
- 租户仓储实现
- 租户数据过滤（基于 TenantId）

---

## 3.6 Atlas.Data.TenantMigrations

存放 TenantDbContext 的 Migration 文件。

---

# 4. Services 层

业务核心逻辑在这里。

---

## 4.1 Atlas.Services.Abstractions

👉 服务抽象层，只包含接口和不依赖实现的基础类。

### ✔ 应放：
- IServiceX 接口
- ServiceBase（抽象、无 Autofac/Mapper/EF 引用）
- 服务层 DTO（可选）
- 领域服务接口（Domain Service）

### ❌ 不应放：
- AutoMapper Profile  
- EF/Redis/HttpClient 实现  
- 具体业务代码

---

## 4.2 Atlas.Services

👉 业务逻辑的实际实现层。

### ✔ 应放：
- XxxService : IServiceX
- AutoMapper Profiles（如 ProductProfile）
- 调用 Data 层逻辑的业务实现
- 缓存装饰器（CacheService）
- 数据校验、业务流程、计算逻辑

### ❌ 不放：
- Controller
- DbContext
- 领域实体（属于 Core）

---

# 5. Infrastructure

基础设施层，用于适配外部系统。

包括以下子模块：

## ✔ Atlas.Infrastructure.Caching
- ICacheProvider 实现  
- MemoryCache / RedisCache

## ✔ Atlas.Infrastructure.Common
- 文件操作
- HttpClient 工具
- 时间、配置类实现

## ✔ Atlas.Infrastructure.Logging
- 日志适配器  
- Serilog / NLog 实现

## ✔ Atlas.Infrastructure.Security
- Token、JWT、加解密
- 权限校验基础逻辑

---

# 6. Messaging

消息队列（事件总线）基本结构：

## Atlas.Messaging.Abstractions
- 消息发布接口（IEventPublisher、IMessageBus）
- 消息模型（EventMessage）

## Atlas.Messaging.Redis
- Redis Stream / PubSub 的具体实现
- 消费者组逻辑

---

# 7. Extensions

📁 **Atlas.Extensions.DependencyInjection**

负责 **整个系统的依赖注入注册**。

### ✔ 应放：
- AddServices()
- AddData()
- AddCaching()
- AddMessaging()
- AddInfrastructure()
- AddAuthorization()
- AddDomain()

这是系统“组装器”，将所有模块 glue 到一起。

---

# 8. Application 层

系统的最上层，向外提供服务。

## 8.1 WebApi
- Controllers
- Filters
- Middlewares
- API 服务入口
- 配置 Swagger、DI 启动

## 8.2 BackgroundServices
- IHostedService 实现
- 定时任务、事件消费者

## 8.3 Grpc
- gRPC 服务
- .proto 文件生成的代码

## 8.4 Console
- 命令行工具
- 调试脚本
- 数据迁移脚本（可选）

---

# 依赖原则

ore 不依赖任何层
Models 只依赖 Core
Data 依赖 Core + Models
Services 依赖 Data + Models + Core
Infrastructure 可被 Services 或 Application 使用
Application 依赖所有下层，不被任何层依赖
---

# 最佳实践

### ✔ 单向依赖：上层依赖下层，禁止反向引用  
### ✔ 抽象在上（Abstractions），实现向下（Data/Services/Infrastructure）  
### ✔ 业务逻辑集中在 Services  
### ✔ API 层只做输入/输出，不写业务  
### ✔ Profile 总是放在 Services（不要放 Abstractions）  
### ✔ DTO 不要和 Entity 混用  
### ✔ Application 只负责对外接口  

---

# 结束语

该文档用于作为团队开发、模块放置、架构规范的统一指导。  
如项目新增模块，建议参考以上规范确保结构清晰、一致、可扩展。




