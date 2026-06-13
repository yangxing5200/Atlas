# Codex 执行说明：BidOps 国网 ECP ZIP + Word 采购范围表格解析增强

## 0. 背景

用户上传了一个国网 ECP 公告附件 ZIP 样例：

```text
64370755-4288-459e-8a1f-0e97a37bc116.zip
```

该 ZIP 内部实际包含 1 个 Word 文档：

```text
北京电力交易中心有限公司2026年第一次服务公开谈判采购-采购公告.docx
```

该 Word 文档正文中写明：

```text
2.项目概况与采购范围
详见采购公告附件。

3.2 响应供应商须满足如下专用资格要求：
详见采购公告附件。
```

真正的包件级采购范围和专用资格要求位于 Word 文档末尾的“采购公告附件”部分，主要是两个表格：

1. `项目概况与采购范围` 表
2. `响应供应商须满足如下专用资格要求` 表

BidOps 必须能从此类 ZIP + Word + 表格附件中抽取结构化包件和要求项，否则只解析公告页面正文会漏掉最关键的商机经营信息。

---

## 1. 本样例的实测结构

### 1.1 ZIP 结构

ZIP 内部只有 1 个 `.docx` 文件。

需要特别注意：该 ZIP 存在文件名编码不一致问题。

实测现象：

```text
Local File Header filename: UTF-8
Central Directory filename: GBK / GB18030
```

部分严格 ZIP 库在读取时会因为“中心目录文件名”和“本地文件头文件名”不一致而失败。因此实现时不能只假设所有 ZIP 都是标准 UTF-8 ZIP。

实现要求：

1. 不要使用简单 `ExtractToDirectory`。
2. 必须通过安全流式方式读取条目。
3. 必须兼容 UTF-8、GBK/GB18030、CP437 文件名。
4. 文件名只用于展示和溯源，不应影响内容读取。
5. 读取失败时要记录 `ArchiveEntryReadFailed`，不能让整个附件处理任务直接崩溃。

### 1.2 DOCX 文档结构

该 DOCX 可解析出：

```text
段落数量：73
表格数量：2
```

核心元数据可从正文抽出：

```json
{
  "projectName": "北京电力交易中心有限公司2026年第一次服务公开谈判采购",
  "procurementNo": "0711-26OTL07533027",
  "buyerName": "北京电力交易中心有限公司",
  "agentName": "国网物资有限公司",
  "fileGetStart": "2026年06月12日",
  "fileGetEnd": "2026年06月18日17:00时",
  "responseDeadline": "2026年06月23日上午9: 00"
}
```

### 1.3 表 1：项目概况与采购范围

该表共有：

```text
45 行：1 行表头 + 44 行包件
7 列：分标编号、分标名称、包号、包名称、采购范围、服务期/框架协议有效期、实施地点
```

表头为：

```text
分标编号 | 分标名称 | 包号 | 包名称 | 采购范围 | 服务期/框架协议有效期 | 实施地点
```

示例数据：

```text
362601-9011 | 零星服务 | 包1 | 2026年微信公众号运营项目 | 本项目要求供应商按照交易中心信息发布要求，对微信内容进行审校、美工、制图、配图，做好后台运维管理，快速响应交易中心需求。 | 自合同签订日起至2026年12月31日止 | 北京

362601-9010 | 数字化服务 | 包2 | 电力交易平台数据交互规范设计支撑 | 1.电力交易平台数据交互规范、安全及监控方案研究；2.开展电力交易数据能力中心服务能力提升工作 | 自合同签订之日起至2026年12月31日 | 北京
```

### 1.4 表 2：响应供应商专用资格要求

该表共有：

```text
46 行：2 行表头 + 44 行要求
6 列：分标、包号、包名称、资质要求、业绩要求、人员要求
```

该表存在合并表头。第一行后 3 列均为：

```text
响应供应商专用资格要求
```

第二行才是真正子表头：

```text
分标 | 包号 | 包名称 | 资质要求 | 业绩要求 | 人员要求
```

示例数据：

```text
362601-9011零星服务 | 包1 | 2026年微信公众号运营项目 | / | 自2021年1月1日至首次响应截止日，响应供应商具有宣传服务业绩。 | /

362601-9009生产辅助技改大修 | 包1 | 2026年北京电力交易中心交易大厅改造-勘察设计 | （1）具有建设行政主管部门核发的工程设计综合资质，或建筑工程设计行业乙级及以上资质，或建筑工程设计专业乙级及以上资质或建筑智能化工程通用专业乙级及以上资质。 | 自2021年1月1日至首次响应截止日，响应供应商具有类似工程设计业绩不少于1项。 | /

362601-9012综合服务 | 包3 | 适应分布式新能源发展新模式的微市场设计与协同运营关键技术研究 | 接受联合体响应 | 自2021年1月1日至首次响应截止日，响应供应商具有技术开发或技术服务业绩。 | /
```

### 1.5 样例抽取结果

本样例可确定抽出：

```text
包件数量：44
非空要求项数量：54
每个包件都有业绩要求：44 个
有明确资质/联合体/许可证要求的包件：10 个
接受联合体响应的包件：7 个
人员要求：本样例均为 /
```

---

## 2. 本次目标

实现 BidOps 对国网 ECP 常见附件格式的稳定解析：

```text
公告页面
  -> ZIP 附件
    -> Word 文档
      -> 采购公告附件
        -> 项目概况与采购范围表
        -> 专用资格要求表
          -> TenderPackage
          -> RequirementItem
```

必须优先使用确定性表格解析，不要把这类规则明确的表格全部丢给 AI。

AI 可以作为补充：

1. 表格识别失败时辅助判断。
2. 表头变化较大时辅助映射字段。
3. 采购范围长文本进一步拆分能力标签。
4. 资质/业绩/人员要求进一步分类。

但本样例这种标准表格必须由规则解析稳定完成。

---

## 3. 禁止事项

1. 不要执行 ZIP 内任何文件。
2. 不要信任 ZIP 内路径。
3. 不要使用 `ExtractToDirectory` 直接解压到业务目录。
4. 不要把 Word 文档中的宏、脚本、OLE 对象当作可执行内容处理。
5. 不要为了解析 `.doc` 自动启用不受控的外部转换器。
6. 不要把采购公告的“权利声明”绕过为自动转载功能；这里只做业务内部解析、审核、溯源。
7. 不要把同包件多厂家协同投标、自动报价、自动投标纳入本次实现。

---

## 4. 推荐实现架构

### 4.1 新增或改造组件

建议在 `src/Atlas.Modules.BidOps` 下新增/改造：

```text
Documents/
  BidOpsArchiveReader.cs
  BidOpsArchiveEntry.cs
  BidOpsDocumentExtractionResult.cs
  BidOpsExtractedTable.cs
  BidOpsExtractedTableRow.cs
  BidOpsWordTableExtractor.cs
  BidOpsEcpProcurementTableParser.cs

Services/
  BidOpsAttachmentProcessingService.cs        # 接入 archive/docx/table 解析
  BidOpsAiParsingService.cs                   # 优先使用结构化解析 sidecar，再 fallback AI

Tests/
  BidOpsArchiveReaderTests.cs
  BidOpsWordTableExtractorTests.cs
  BidOpsEcpProcurementTableParserTests.cs
```

如现有项目测试目录命名不同，以现有测试项目结构为准。

---

## 5. 安全 ZIP 读取要求

### 5.1 限制参数

建议默认参数：

```csharp
MaxEntries = 100;
MaxNestedArchiveDepth = 2;
MaxSingleEntryBytes = 50 * 1024 * 1024;
MaxTotalUncompressedBytes = 100 * 1024 * 1024;
AllowNestedArchives = false; // 首版默认 false
AllowLegacyDocConversion = false;
```

### 5.2 跳过危险文件

跳过并记录日志：

```text
.exe .dll .bat .cmd .ps1 .vbs .js .scr .msi .com .sh .jar
```

### 5.3 文件名归一化

输出字段建议：

```csharp
public sealed class BidOpsArchiveEntry
{
    public string DisplayName { get; init; } = string.Empty;
    public string SafeRelativePath { get; init; } = string.Empty;
    public string Extension { get; init; } = string.Empty;
    public long CompressedBytes { get; init; }
    public long UncompressedBytes { get; init; }
    public int Depth { get; init; }
    public string? EncodingGuess { get; init; }
    public bool IsSkipped { get; init; }
    public string? SkipReason { get; init; }
}
```

文件名解码优先级建议：

```text
1. 如果本地文件头标记 UTF-8 且能解码，使用 UTF-8
2. 如果中心目录名称 GB18030 可读，使用 GB18030
3. 如果 CP437 可读但明显乱码，尝试 GB18030 回转
4. 最后 fallback 为 entry-0001.docx
```

注意：展示名和内容读取要解耦。即使文件名解码失败，也应尽量读取文件内容。

---

## 6. DOCX 表格抽取要求

当前只读 `word/document.xml` 并去掉 XML 标签是不够的。采购范围在表格里，必须保留行列结构。

### 6.1 输出结构

建议抽取结果同时输出纯文本和结构化表格：

```csharp
public sealed class BidOpsDocumentExtractionResult
{
    public string PlainText { get; init; } = string.Empty;
    public IReadOnlyList<BidOpsExtractedTable> Tables { get; init; } = Array.Empty<BidOpsExtractedTable>();
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

public sealed class BidOpsExtractedTable
{
    public int TableIndex { get; init; }
    public string? CaptionOrNearbyHeading { get; init; }
    public IReadOnlyList<string> HeaderRows { get; init; } = Array.Empty<string>();
    public IReadOnlyList<BidOpsExtractedTableRow> Rows { get; init; } = Array.Empty<BidOpsExtractedTableRow>();
    public string Markdown { get; init; } = string.Empty;
}

public sealed class BidOpsExtractedTableRow
{
    public int RowIndex { get; init; }
    public IReadOnlyList<string> Cells { get; init; } = Array.Empty<string>();
}
```

### 6.2 表格 Markdown 输出

为了让现有 AI 解析链路也能吃到，必须把表格同时输出成 Markdown 表格，例如：

```markdown
## 表格 1：项目概况与采购范围

| 分标编号 | 分标名称 | 包号 | 包名称 | 采购范围 | 服务期/框架协议有效期 | 实施地点 |
|---|---|---|---|---|---|---|
| 362601-9011 | 零星服务 | 包1 | 2026年微信公众号运营项目 | 本项目要求供应商按照交易中心信息发布要求，对微信内容进行审校、美工、制图、配图，做好后台运维管理，快速响应交易中心需求。 | 自合同签订日起至2026年12月31日止 | 北京 |
```

### 6.3 附近标题识别

表格分类时需要读取表格前最近的非空段落。

本样例的顺序是：

```text
采购公告附件
1 项目概况与采购范围
[表格 0]
2 响应供应商须满足如下专用资格要求
[表格 1]
```

`CaptionOrNearbyHeading` 应分别识别为：

```text
1 项目概况与采购范围
2 响应供应商须满足如下专用资格要求
```

---

## 7. 表格分类与字段映射

### 7.1 采购范围表识别

表格满足以下条件时，判定为 `EcpProcurementScopeTable`：

必备表头命中至少 5 个：

```text
分标编号
分标名称
包号
包名称
采购范围
服务期
框架协议有效期
实施地点
交货地点
服务地点
```

标准字段映射：

```text
分标编号 -> LotCode / SegmentCode / FenBiaoCode
分标名称 -> LotName / SegmentName / FenBiaoName
包号 -> PackageNo
包名称 -> PackageName
采购范围 -> PurchaseScope
服务期/框架协议有效期 -> ServicePeriod
实施地点 -> ImplementationPlace
```

### 7.2 专用资格要求表识别

表格满足以下条件时，判定为 `EcpSupplierQualificationTable`：

必备字段：

```text
分标
包号
包名称
资质要求
业绩要求
人员要求
```

需支持两行表头：

```text
第 0 行：分标 | 包号 | 包名称 | 响应供应商专用资格要求 | 响应供应商专用资格要求 | 响应供应商专用资格要求
第 1 行：分标 | 包号 | 包名称 | 资质要求 | 业绩要求 | 人员要求
```

表头归一化后应得到：

```text
分标
包号
包名称
资质要求
业绩要求
人员要求
```

### 7.3 分标字段拆分

专用资格要求表中 `分标` 字段可能是合并文本：

```text
362601-9011零星服务
362601-9009生产辅助技改大修
362601-9010数字化服务
362601-9012综合服务
```

拆分规则：

```regex
^(?<fenbiaoCode>\d{6}-\d+)(?<fenbiaoName>.+)$
```

输出：

```json
{
  "fenbiaoCode": "362601-9011",
  "fenbiaoName": "零星服务"
}
```

---

## 8. 包件合并规则

采购范围表生成 `TenderPackage` 主数据。

专用资格要求表生成 `RequirementItem`，并挂到对应包件。

首选匹配键：

```text
fenbiaoCode + packageNo + normalized(packageName)
```

降级匹配键：

```text
fenbiaoCode + packageNo
```

模糊匹配：

```text
fenbiaoCode + packageNo + packageName similarity >= 0.85
```

匹配失败时：

1. 不要丢弃该资格要求行。
2. 生成 `OrphanRequirementCandidate`。
3. 审核页展示“未匹配到包件”的解析告警。
4. 日志记录 `PackageRequirementMatchFailed`。

---

## 9. RequirementItem 生成规则

对专用资格要求表每一行：

### 9.1 资质要求

如果值不是 `/`、`无`、空字符串，则生成：

```json
{
  "requirementType": "Qualification",
  "originalText": "...",
  "isMandatory": true,
  "isRejectRisk": true,
  "requiredEvidenceType": "QualificationCertificate"
}
```

如果文本包含：

```text
接受联合体响应
接受联合体
联合体
```

同时设置包件：

```json
{
  "acceptJointVenture": true
}
```

并生成要求项：

```json
{
  "requirementType": "JointVenture",
  "originalText": "接受联合体响应",
  "isMandatory": false,
  "isRejectRisk": false
}
```

如果文本包含：

```text
许可证
资质
认证证书
```

风险等级建议为 `High` 或 `Medium`，具体由现有 RiskLevel 枚举决定。

### 9.2 业绩要求

如果值不是 `/`、`无`、空字符串，则生成：

```json
{
  "requirementType": "Performance",
  "originalText": "自2021年1月1日至首次响应截止日，响应供应商具有宣传服务业绩。",
  "isMandatory": true,
  "isRejectRisk": false,
  "requiredEvidenceType": "PerformanceContract"
}
```

可以尝试进一步抽取：

```text
业绩起始日期：2021-01-01
业绩截止日期：首次响应截止日
业绩类别：宣传服务 / 数字化服务 / 信息系统运维 / 税务服务 / 印刷服务
最低数量：不少于1项，若文本出现
```

但首版不强制结构化这些子字段，必须保留原文。

### 9.3 人员要求

如果值不是 `/`、`无`、空字符串，则生成：

```json
{
  "requirementType": "Personnel",
  "originalText": "...",
  "isMandatory": true,
  "requiredEvidenceType": "PersonnelCertificate"
}
```

本样例人员要求均为 `/`，不生成人员要求项。

---

## 10. 输出 sidecar JSON

为保证审核可追溯，附件处理阶段应保存结构化 sidecar JSON，例如：

```text
{rawAttachmentStorageKey}.structured.json
```

建议结构：

```json
{
  "source": {
    "archiveFileName": "64370755-4288-459e-8a1f-0e97a37bc116.zip",
    "documentPath": "北京电力交易中心有限公司2026年第一次服务公开谈判采购-采购公告.docx",
    "documentType": "docx"
  },
  "metadata": {
    "projectName": "北京电力交易中心有限公司2026年第一次服务公开谈判采购",
    "procurementNo": "0711-26OTL07533027",
    "buyerName": "北京电力交易中心有限公司",
    "agentName": "国网物资有限公司",
    "responseDeadline": "2026年06月23日上午9: 00"
  },
  "packages": [
    {
      "fenbiaoCode": "362601-9011",
      "fenbiaoName": "零星服务",
      "packageNo": "包1",
      "packageName": "2026年微信公众号运营项目",
      "purchaseScope": "本项目要求供应商按照交易中心信息发布要求，对微信内容进行审校、美工、制图、配图，做好后台运维管理，快速响应交易中心需求。",
      "servicePeriod": "自合同签订日起至2026年12月31日止",
      "implementationPlace": "北京",
      "requirements": [
        {
          "requirementType": "Performance",
          "originalText": "自2021年1月1日至首次响应截止日，响应供应商具有宣传服务业绩。",
          "sourceTable": "响应供应商专用资格要求",
          "sourceRowIndex": 2
        }
      ],
      "sourceTable": "项目概况与采购范围",
      "sourceRowIndex": 1,
      "confidence": 0.98
    }
  ],
  "warnings": []
}
```

---

## 11. 进入正式业务表的建议

当前 BidOps 已有 `TenderPackage`、`RequirementItem` 等概念时，映射建议：

```text
TenderPackage.PackageNo      <- 包号
TenderPackage.PackageName    <- 包名称
TenderPackage.Category       <- 分标名称
TenderPackage.DeliveryPlace  <- 实施地点
TenderPackage.DeliveryPeriod <- 服务期/框架协议有效期
TenderPackage.Status         <- Draft / PendingReview / Active，以现有枚举为准
```

对当前表中没有预算金额、最高限价的情况：

```text
BudgetAmount = null
MaxPrice = null
```

不要为了字段完整性臆造金额。

`RequirementItem` 映射：

```text
RequirementType        <- Qualification / Performance / Personnel / JointVenture
OriginalText           <- 表格原文
IsMandatory            <- true，JointVenture 可为 false
IsRejectRisk           <- 资质/许可证类通常 true，业绩类首版 false
RequiredEvidenceType   <- QualificationCertificate / PerformanceContract / PersonnelCertificate
RiskLevel              <- High / Medium / Low，以现有枚举为准
SourceDocumentPath     <- docx 文件名
SourceTableName        <- 采购范围表 / 专用资格表
SourceRowIndex         <- 表格行号
```

如果现有实体没有 Source 字段，首版可以先保存到解析 sidecar JSON，并在审核页展示；不要强行破坏性迁移。

---

## 12. 与 AI 解析链路的关系

必须调整解析顺序：

```text
1. 附件文本抽取
2. 附件结构化表格抽取
3. 国网 ECP 表格规则解析
4. 如果规则解析成功，生成确定性 ParsedNotice/Package/Requirement 候选
5. AI 只做补充字段增强和异常兜底
6. 人工审核通过后入正式业务表
```

不要把规则表格先压成一大段纯文本再交给 AI 猜字段。那样容易丢行、串包、串要求。

---

## 13. 审核页展示要求

前端审核详情页需要能看到：

1. 附件 ZIP 名称。
2. ZIP 内 Word 文件名称。
3. 采购范围表解析状态。
4. 专用资格表解析状态。
5. 包件数量。
6. 要求项数量。
7. 每个包件的来源表格、来源行号。
8. 未匹配要求行、解析告警。
9. “查看原表格”入口。
10. “重新解析附件”按钮。

建议审核页展示类似：

```text
附件结构化解析
- 文档：北京电力交易中心有限公司2026年第一次服务公开谈判采购-采购公告.docx
- 表格：项目概况与采购范围，44 个包件，成功
- 表格：响应供应商专用资格要求，44 行，成功
- 合并结果：44 个包件，54 个要求项，0 个未匹配行
```

---

## 14. 运营看板指标

BidOps 后台任务看板中增加附件解析指标：

```text
ZIP 附件数
ZIP 展开成功数
ZIP 展开失败数
DOCX 表格抽取成功数
采购范围表识别成功数
专用资格表识别成功数
包件抽取数量
要求项抽取数量
规则解析失败需 AI 兜底数量
```

单个 RawNotice 流水线中展示：

```text
RawNotice
  -> AttachmentDownload
  -> ArchiveExpand
  -> DocxTableExtract
  -> EcpProcurementTableParse
  -> StructuredParse
  -> ReviewTask
```

---

## 15. 测试要求

使用用户上传样例 ZIP 增加测试 fixture。

测试数据可以放到：

```text
tests/fixtures/bidops/sgcc-ecp-procurement-scope-sample.zip
```

如仓库不允许提交真实公告附件，可改用脱敏后的结构等价 DOCX/ZIP。

### 15.1 ZIP 读取测试

必须验证：

```text
Given: 中心目录 GBK、本地文件头 UTF-8 的 ZIP
When: ArchiveReader 读取
Then: 不抛异常
And: 能得到 1 个 docx 条目
And: DisplayName 为可读中文或安全 fallback
```

### 15.2 DOCX 表格抽取测试

必须验证：

```text
Tables.Count == 2
采购范围表 Rows.Count == 45
专用资格表 Rows.Count == 46
```

### 15.3 采购范围解析测试

必须验证：

```text
Packages.Count == 44
Packages[0].FenBiaoCode == "362601-9011"
Packages[0].FenBiaoName == "零星服务"
Packages[0].PackageNo == "包1"
Packages[0].PackageName == "2026年微信公众号运营项目"
Packages[0].ImplementationPlace == "北京"
```

### 15.4 专用资格要求合并测试

必须验证：

```text
RequirementItems.Count == 54
PerformanceRequirementPackageCount == 44
QualificationRequirementPackageCount == 10
AcceptJointVenturePackageCount == 7
UnmatchedQualificationRows.Count == 0
```

### 15.5 表头变化测试

至少构造 3 个变体：

```text
服务期/框架协议有效期 -> 服务期限
实施地点 -> 服务地点
采购范围 -> 项目内容
```

解析器应通过表头别名识别，或返回清晰的低置信度告警。

---

## 16. 实施顺序

### P0：不破坏现有链路

1. 增加安全 ArchiveReader。
2. 增加 DOCX 表格结构抽取。
3. 现有 `.extracted.txt` 输出继续保留。
4. Markdown 表格追加到 extracted text。
5. 不改正式业务表结构。

### P1：规则结构化

1. 增加 `BidOpsEcpProcurementTableParser`。
2. 输出 sidecar JSON。
3. 在 `StructuredParse` 前优先读取 sidecar。
4. 审核页展示包件和要求项候选。
5. 单测覆盖本样例。

### P2：入库与运营

1. 审核通过后稳定生成正式 `TenderPackage` 与 `RequirementItem`。
2. 增加附件解析流水线状态。
3. 运维看板加入 ZIP/DOCX 表格解析指标。
4. 支持单个附件重新解析。

### P3：泛化增强

1. 支持 Excel 采购清单。
2. 支持嵌套 ZIP，默认深度不超过 2。
3. 支持 `.doc` 受控转换，默认关闭。
4. 支持多 Word、多表格、多采购范围表合并。
5. 支持表格布局差异更大的 AI 辅助映射。

---

## 17. 验收标准

Codex 执行完成后，必须给出以下结果：

```text
1. 是否能安全读取样例 ZIP：是/否
2. ZIP 内可解析文件数量：1
3. DOCX 表格数量：2
4. 采购范围包件数量：44
5. 专用资格要求行数量：44
6. 合并后 RequirementItem 数量：54
7. 未匹配要求行数量：0
8. 是否生成 extracted text：是/否
9. 是否生成 structured sidecar JSON：是/否
10. 是否有单元测试覆盖：是/否
```

如果某个指标不达标，不要假装成功，必须在总结中写清失败原因和下一步。

---

## 18. Codex 执行总结格式

执行完成后，请按以下格式输出：

```markdown
## BidOps SGCC ECP 附件解析增强执行结果

### 已完成
- ...

### 本次样例解析结果
- ZIP 条目数：
- DOCX 条目数：
- 表格数：
- 包件数：
- 要求项数：
- 未匹配行数：

### 修改文件
- ...

### 新增测试
- ...

### 已知限制
- ...

### 后续建议
- ...
```
