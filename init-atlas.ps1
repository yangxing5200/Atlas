# Atlas 多租户框架初始化脚本
# PowerShell 版本

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Atlas 多租户框架初始化脚本" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 1. 创建根目录
Write-Host "1. 创建项目目录..." -ForegroundColor Yellow
New-Item -ItemType Directory -Force -Path "Atlas" | Out-Null
Set-Location "Atlas"

# 2. 创建解决方案
Write-Host "2. 创建解决方案..." -ForegroundColor Yellow
dotnet new sln -n Atlas

# 3. 创建目录结构
Write-Host "3. 创建目录结构..." -ForegroundColor Yellow
$directories = @(
    "src/1.Core",
    "src/2.Models",
    "src/3.Data",
    "src/4.Services",
    "src/5.Infrastructure",
    "src/6.Messaging",
    "tests",
    "samples",
    "docs"
)

foreach ($dir in $directories) {
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
}

# 4. 创建 Core 层项目
Write-Host "4. 创建 Core 层项目..." -ForegroundColor Yellow
Set-Location "src/1.Core"
dotnet new classlib -n Atlas.Core -f net8.0
dotnet sln ../../Atlas.sln add Atlas.Core/Atlas.Core.csproj
Set-Location "../.."

# 5. 创建 Models 层项目
Write-Host "5. 创建 Models 层项目..." -ForegroundColor Yellow
Set-Location "src/2.Models"
dotnet new classlib -n Atlas.Models.Global -f net8.0
dotnet sln ../../Atlas.sln add Atlas.Models.Global/Atlas.Models.Global.csproj
Set-Location "../.."

# 6. 创建 Data 层项目
Write-Host "6. 创建 Data 层项目..." -ForegroundColor Yellow
Set-Location "src/3.Data"

# 6.1 Data.Abstractions
dotnet new classlib -n Atlas.Data.Abstractions -f net8.0
dotnet sln ../../Atlas.sln add Atlas.Data.Abstractions/Atlas.Data.Abstractions.csproj

# 6.2 Data.Global
dotnet new classlib -n Atlas.Data.Global -f net8.0
dotnet sln ../../Atlas.sln add Atlas.Data.Global/Atlas.Data.Global.csproj

# 6.3 Data.Global.Migrations
dotnet new classlib -n Atlas.Data.Global.Migrations -f net8.0
dotnet sln ../../Atlas.sln add Atlas.Data.Global.Migrations/Atlas.Data.Global.Migrations.csproj

# 6.4 Data.Tenant
dotnet new classlib -n Atlas.Data.Tenant -f net8.0
dotnet sln ../../Atlas.sln add Atlas.Data.Tenant/Atlas.Data.Tenant.csproj

# 6.5 Data.Tenant.Migrations
dotnet new classlib -n Atlas.Data.Tenant.Migrations -f net8.0
dotnet sln ../../Atlas.sln add Atlas.Data.Tenant.Migrations/Atlas.Data.Tenant.Migrations.csproj

Set-Location "../.."

# 7. 创建 Services 层项目
Write-Host "7. 创建 Services 层项目..." -ForegroundColor Yellow
Set-Location "src/4.Services"

# 7.1 Services.Abstractions
dotnet new classlib -n Atlas.Services.Abstractions -f net8.0
dotnet sln ../../Atlas.sln add Atlas.Services.Abstractions/Atlas.Services.Abstractions.csproj

# 7.2 Services
dotnet new classlib -n Atlas.Services -f net8.0
dotnet sln ../../Atlas.sln add Atlas.Services/Atlas.Services.csproj

Set-Location "../.."

# 8. 创建 Infrastructure 层项目
Write-Host "8. 创建 Infrastructure 层项目..." -ForegroundColor Yellow
Set-Location "src/5.Infrastructure"

# 8.1 Infrastructure.Caching
dotnet new classlib -n Atlas.Infrastructure.Caching -f net8.0
dotnet sln ../../Atlas.sln add Atlas.Infrastructure.Caching/Atlas.Infrastructure.Caching.csproj

# 8.2 Infrastructure.Logging
dotnet new classlib -n Atlas.Infrastructure.Logging -f net8.0
dotnet sln ../../Atlas.sln add Atlas.Infrastructure.Logging/Atlas.Infrastructure.Logging.csproj

# 8.3 Infrastructure.Security
dotnet new classlib -n Atlas.Infrastructure.Security -f net8.0
dotnet sln ../../Atlas.sln add Atlas.Infrastructure.Security/Atlas.Infrastructure.Security.csproj

Set-Location "../.."

# 9. 创建 Messaging 层项目
Write-Host "9. 创建 Messaging 层项目..." -ForegroundColor Yellow
Set-Location "src/6.Messaging"

# 9.1 Messaging.Abstractions
dotnet new classlib -n Atlas.Messaging.Abstractions -f net8.0
dotnet sln ../../Atlas.sln add Atlas.Messaging.Abstractions/Atlas.Messaging.Abstractions.csproj

# 9.2 Messaging.RabbitMQ
dotnet new classlib -n Atlas.Messaging.RabbitMQ -f net8.0
dotnet sln ../../Atlas.sln add Atlas.Messaging.RabbitMQ/Atlas.Messaging.RabbitMQ.csproj

Set-Location "../.."

# 10. 创建测试项目
Write-Host "10. 创建测试项目..." -ForegroundColor Yellow
Set-Location "tests"

dotnet new xunit -n Atlas.Core.Tests -f net8.0
dotnet sln ../Atlas.sln add Atlas.Core.Tests/Atlas.Core.Tests.csproj

dotnet new xunit -n Atlas.Data.Tests -f net8.0
dotnet sln ../Atlas.sln add Atlas.Data.Tests/Atlas.Data.Tests.csproj

dotnet new xunit -n Atlas.Services.Tests -f net8.0
dotnet sln ../Atlas.sln add Atlas.Services.Tests/Atlas.Services.Tests.csproj

dotnet new xunit -n Atlas.Integration.Tests -f net8.0
dotnet sln ../Atlas.sln add Atlas.Integration.Tests/Atlas.Integration.Tests.csproj

Set-Location ".."

# 11. 创建示例项目
Write-Host "11. 创建示例项目..." -ForegroundColor Yellow
Set-Location "samples"

dotnet new webapi -n Atlas.Sample.WebApi -f net8.0
dotnet sln ../Atlas.sln add Atlas.Sample.WebApi/Atlas.Sample.WebApi.csproj

dotnet new console -n Atlas.Sample.Console -f net8.0
dotnet sln ../Atlas.sln add Atlas.Sample.Console/Atlas.Sample.Console.csproj

Set-Location ".."

# 12. 配置项目依赖关系
Write-Host "12. 配置项目依赖关系..." -ForegroundColor Yellow

# Models.Global -> Core
dotnet add src/2.Models/Atlas.Models.Global reference src/1.Core/Atlas.Core

# Data.Abstractions -> Core, Models.Global
dotnet add src/3.Data/Atlas.Data.Abstractions reference src/1.Core/Atlas.Core
dotnet add src/3.Data/Atlas.Data.Abstractions reference src/2.Models/Atlas.Models.Global

# Data.Global -> Data.Abstractions
dotnet add src/3.Data/Atlas.Data.Global reference src/3.Data/Atlas.Data.Abstractions

# Data.Global.Migrations -> Data.Global
dotnet add src/3.Data/Atlas.Data.Global.Migrations reference src/3.Data/Atlas.Data.Global

# Data.Tenant -> Data.Abstractions
dotnet add src/3.Data/Atlas.Data.Tenant reference src/3.Data/Atlas.Data.Abstractions

# Data.Tenant.Migrations -> Data.Tenant
dotnet add src/3.Data/Atlas.Data.Tenant.Migrations reference src/3.Data/Atlas.Data.Tenant

# Services.Abstractions -> Core, Models.Global
dotnet add src/4.Services/Atlas.Services.Abstractions reference src/1.Core/Atlas.Core
dotnet add src/4.Services/Atlas.Services.Abstractions reference src/2.Models/Atlas.Models.Global

# Services -> Services.Abstractions, Data.Abstractions
dotnet add src/4.Services/Atlas.Services reference src/4.Services/Atlas.Services.Abstractions
dotnet add src/4.Services/Atlas.Services reference src/3.Data/Atlas.Data.Abstractions

# Infrastructure.Caching -> Core
dotnet add src/5.Infrastructure/Atlas.Infrastructure.Caching reference src/1.Core/Atlas.Core

# Infrastructure.Logging -> Core
dotnet add src/5.Infrastructure/Atlas.Infrastructure.Logging reference src/1.Core/Atlas.Core

# Infrastructure.Security -> Core, Models.Global
dotnet add src/5.Infrastructure/Atlas.Infrastructure.Security reference src/1.Core/Atlas.Core
dotnet add src/5.Infrastructure/Atlas.Infrastructure.Security reference src/2.Models/Atlas.Models.Global

# Messaging.Abstractions -> Core
dotnet add src/6.Messaging/Atlas.Messaging.Abstractions reference src/1.Core/Atlas.Core

# Messaging.RabbitMQ -> Messaging.Abstractions
dotnet add src/6.Messaging/Atlas.Messaging.RabbitMQ reference src/6.Messaging/Atlas.Messaging.Abstractions

# 13. 添加 NuGet 包
Write-Host "13. 添加 NuGet 包..." -ForegroundColor Yellow

# EF Core 包 (Data 层)
dotnet add src/3.Data/Atlas.Data.Global package Microsoft.EntityFrameworkCore --version 8.0.0
dotnet add src/3.Data/Atlas.Data.Global package Microsoft.EntityFrameworkCore.SqlServer --version 8.0.0
dotnet add src/3.Data/Atlas.Data.Global package Microsoft.EntityFrameworkCore.Design --version 8.0.0

dotnet add src/3.Data/Atlas.Data.Global.Migrations package Microsoft.EntityFrameworkCore.Tools --version 8.0.0

dotnet add src/3.Data/Atlas.Data.Tenant package Microsoft.EntityFrameworkCore --version 8.0.0
dotnet add src/3.Data/Atlas.Data.Tenant package Microsoft.EntityFrameworkCore.SqlServer --version 8.0.0

# Redis 包
dotnet add src/5.Infrastructure/Atlas.Infrastructure.Caching package StackExchange.Redis --version 2.7.0
dotnet add src/5.Infrastructure/Atlas.Infrastructure.Caching package Microsoft.Extensions.Caching.Memory --version 8.0.0

dotnet add src/6.Messaging/Atlas.Messaging.RabbitMQ package MassTransit.RabbitMQ --version 8.4.1

# Serilog 包
dotnet add src/5.Infrastructure/Atlas.Infrastructure.Logging package Serilog --version 3.1.0
dotnet add src/5.Infrastructure/Atlas.Infrastructure.Logging package Serilog.Sinks.Console --version 5.0.0
dotnet add src/5.Infrastructure/Atlas.Infrastructure.Logging package Serilog.Sinks.File --version 5.0.0

# 其他包
dotnet add src/4.Services/Atlas.Services package AutoMapper --version 12.0.0
dotnet add src/4.Services/Atlas.Services package FluentValidation --version 11.0.0

# 14. 创建 .gitignore
Write-Host "14. 创建 .gitignore..." -ForegroundColor Yellow
@"
## Ignore Visual Studio temporary files, build results, and
## files generated by popular Visual Studio add-ons.

# User-specific files
*.suo
*.user
*.userosscache
*.sln.docstates

# Build results
[Dd]ebug/
[Dd]ebugPublic/
[Rr]elease/
[Rr]eleases/
x64/
x86/
[Aa][Rr][Mm]/
[Aa][Rr][Mm]64/
bld/
[Bb]in/
[Oo]bj/

# Visual Studio cache/options
.vs/
.vscode/

# NuGet Packages
*.nupkg
**/packages/*

# Others
*.log
*.sql
*.cache
"@ | Out-File -FilePath ".gitignore" -Encoding UTF8
