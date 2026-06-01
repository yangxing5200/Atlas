# Atlas 包管理规范

Atlas 使用 Central Package Management，所有 NuGet 版本必须定义在根目录 `Directory.Packages.props` 中。

## NuGet 源

仓库根目录的 `NuGet.config` 会清理用户级 NuGet 源，并默认只使用：

```text
nuget.org=https://api.nuget.org/v3/index.json
```

这样可以避免本机私有源影响 restore 输出，也避免中央包管理在多源配置下产生 `NU1507` 警告。后续如果确实需要私有包，应在仓库级 `NuGet.config` 中显式增加对应源，并同时配置 package source mapping，不能依赖个人机器上的 NuGet 源。

## 版本声明规则

允许：

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" />
```

禁止：

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.0" />
```

禁止：

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore">
  <Version>8.0.0</Version>
</PackageReference>
```

检查命令：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-central-package-versions.ps1
```

该脚本会扫描 `*.csproj`、`*.props` 和 `*.targets`，忽略 `bin`、`obj` 和 `Directory.Packages.props`，发现内联包版本时直接失败。
