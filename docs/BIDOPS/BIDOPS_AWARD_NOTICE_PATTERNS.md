# BidOps Award Notice Parsing Patterns

This document records real public award/result announcement patterns found during
BidOps parser hardening. Each pattern should capture source examples, field
locations, expected extraction output, and parser rules before code is changed.

## Pattern 1: Body Context + Two-Column Mso Result Table

Source example:

- `https://ecp.sgcc.com.cn/ecp2.0/portal/#/doc/doci-win/2606128518911213_2018060501171111`

Announcement:

- Title: `国家电网有限公司国调中心2026年调度技术支持系统运维项目邀请谈判采购-成交结果公告`
- WCM API: `index/getNoticeWin`
- Notice id: `2606128518911213`
- Publish time: `2026-06-12 12:14:18`
- WCM metadata buyer: `国家电网有限公司`
- WCM metadata agency: `国网物资有限公司`

### Shape

The common body text before the table contains announcement-level context:

```text
采购编号：0711-26OTL05312031
分标编号：SG26A5-9007-12003 分标名称：运维服务
```

The result table is a Word/WPS HTML table:

```html
<table class="MsoNormalTable" border="1" cellspacing="0" cellpadding="0">
```

The table has only package and supplier columns:

```text
包号 | 成交 候选 人
```

### Expected Extraction

Common context for every extracted row:

- Project/procurement code: `0711-26OTL05312031`
- Lot number: `SG26A5-9007-12003`
- Lot name: `运维服务`
- Buyer: `国家电网有限公司`
- Agency: `国网物资有限公司`
- Outcome type: `Awarded` when the announcement title/type is a result/win notice, even if the table header says `成交候选人`.

Package rows:

```text
包1  | 北京科东电力控制系统有限责任公司
包2  | 北京铁力山科技股份有限公司
包3  | 源代码（北京）通用技术有限公司
包4  | 中国电力科学研究院有限公司
包5  | 中国电力科学研究院有限公司
包6  | 南京南瑞水利水电科技有限公司
包7  | 国电南瑞南京控制系统有限公司
包8  | 国电南瑞南京控制系统有限公司
包9  | 北京科东电力控制系统有限责任公司
包10 | 华中科技大学
包11 | 北京四方继保工程技术有限公司
包12 | 北京科东电力控制系统有限责任公司
包13 | 北京科东电力控制系统有限责任公司
包14 | 北京科东电力控制系统有限责任公司
包15 | 北京四方继保工程技术有限公司
包16 | 北京科东电力控制系统有限责任公司
包17 | 中国电力科学研究院有限公司
包18 | 国电南瑞南京控制系统有限公司
包19 | 国电南瑞南京控制系统有限公司
包20 | 南京南瑞信息通信科技有限公司
包21 | 北京恒泰实达科技股份有限公司
包22 | 国网电力科学研究院武汉南瑞有限责任公司
包23 | 新华三技术有限公司
包24 | 国电南瑞南京控制系统有限公司
包25 | 泰豪软件股份有限公司
包26 | 南京南瑞信息通信科技有限公司
包27 | 北京科东电力控制系统有限责任公司
```

### Parser Rules

- Treat `成交候选人`, `中标候选人`, and `推荐成交候选人` as table header signals so the table is not discarded during table detection.
- When the announcement is a result/win notice (`doci-win`, `中标公告`, `成交结果公告`, `成交公告`), treat a supplier column named `成交候选人` as an awarded/result supplier column.
- Read `采购编号` / `招标编号` / `项目编号` from announcement-level body context when the table omits the column.
- Read `分标编号` and `分标名称` from the text immediately before the table when the table omits those columns. Do not apply a global first lot blindly across unrelated tables.
- Preserve original WCM `CONT` HTML so `MsoNormalTable` and `p.MsoNormal` structure remains available for review and re-extraction.
- Do not require supplier names to end with `公司`; valid suppliers may be universities, institutes, or other organizations.
- If a row status column exists, skip `流标`, `废标`, `未中标`, `未成交`, and `否决`. This pattern has no status column, so all package rows are treated as awarded because the document is a result announcement.

### Current Parser Gap

The observed pre-refactor extraction result for this sample was:

```text
awards=0; candidates=0; outcomes=0
```

Root cause:

- The generic table detector did not count `成交候选人` as a header signal, so the table was not passed to candidate/result parsers.
- Candidate-style supplier columns and awarded-result outcome mapping are not yet unified for result announcements.

### Variant 1B: Inline Mso Full Result Table With Professional-Service Suppliers

Additional source example:

- `https://ecp.sgcc.com.cn/ecp2.0/portal/#/doc/doci-win/2606128522182697_2018060501171111`

Announcement:

- Title: `国家电网有限公司2026年总部第一批外聘律师、法律中介机构选聘公开谈判采购成交公告`
- WCM API: `index/getNoticeWin`
- Notice id: `2606128522182697`
- Publish time: `2026-06-12 15:27:35`
- WCM metadata buyer: `国家电网有限公司`
- WCM metadata agency: `国网物资有限公司`
- No result attachment is needed; `getWinFile` returns an empty file list.

The body contains the project/procurement code:

```text
采购编号：0711-26OTL05433014
```

The inline Word/WPS table is complete:

```text
分标编号 | 分标名称 | 包号 | 中标状态 | 项目单位 | 成交供应商
```

Expected/current extraction:

```text
awards=13; candidates=13; outcomes=13
```

Representative rows:

```text
SG2648-9012-33008 | 综合服务（法律类1） | 包1  | 国家电网有限公司 | 北京德恒律师事务所
SG2648-9012-33008 | 综合服务（法律类1） | 包2  | 国家电网有限公司 | 北京市通商律师事务所
SG2648-9012-33008 | 综合服务（法律类1） | 包5  | 国家电网有限公司 | 北京天达共和律师事务所
SG2648-9012-33008 | 综合服务（法律类1） | 包8  | 国家电网有限公司 | 上海市锦天城律师事务所
SG2648-9012-33008 | 综合服务（法律类1） | 包13 | 国家电网有限公司 | 北京瀛和律师事务所
```

Parser notes:

- This is a positive regression sample for complete inline WCM result tables.
- Supplier normalization must keep professional-service organizations such as
  `律师事务所`; do not require `公司` suffixes.
- Because the table has `分标编号`, `分标名称`, `项目单位`, and `成交供应商`, body
  context is only needed for the procurement/project code.
- Current award/outcome extraction succeeds on this variant. Candidate extraction
  also returns rows because `成交供应商` is recognized as a supplier column; during
  refactor, result announcements should map these rows to `Awarded` outcomes.

### Variant 1C: Inline Mso Result Table With Multiple Suppliers Per Package

Additional source example:

- `https://ecp.sgcc.com.cn/ecp2.0/portal/#/doc/doci-win/2606128513815881_2018060501171111`

Announcement:

- Title: `国家电网有限公司2026年普通车辆框架协议（电商交易）公开谈判采购成交公告`
- WCM API: `index/getNoticeWin`
- Notice id: `2606128513815881`
- Publish time: `2026-06-12 09:42:00`
- WCM metadata buyer: `国家电网有限公司`
- WCM metadata agency: `国网物资有限公司`
- The detail API stores the notice content under `resultValue.notice.CONT`.
- No result attachment is needed; `getWinFile` returns an empty file list.

The body contains the project/procurement code:

```text
采购编号：0711-26OTL01232004
```

The inline Word/WPS table is:

```text
分标编号 | 包号 | 成交状态 | 成交供应商
```

Observed table shape:

- HTML table count: `1`
- `MsoNormalTable` count: `1`
- Data rows: `140`
- All data rows have status `成交`
- No `分标名称` column
- No `项目单位` column

Lot/package distribution:

```text
SG2661-1504-32004 | rows=17 | packages=7
SG2661-1504-32005 | rows=8  | packages=3
SG2661-1504-32022 | rows=13 | packages=6
SG2661-1504-32006 | rows=29 | packages=10
SG2661-1504-32007 | rows=73 | packages=21
```

Representative rows:

```text
SG2661-1504-32004 | 包1  | 成交 | 安徽奇瑞汽车销售有限公司
SG2661-1504-32004 | 包1  | 成交 | 广汽传祺汽车销售有限公司
SG2661-1504-32004 | 包2  | 成交 | 一汽红旗汽车销售有限公司
SG2661-1504-32004 | 包2  | 成交 | 上海安吉汽车销售有限公司
SG2661-1504-32004 | 包7  | 成交 | 一汽丰田汽车销售有限公司
SG2661-1504-32005 | 包1  | 成交 | 北汽福田汽车股份有限公司
SG2661-1504-32022 | 包6  | 成交 | 南京依维柯汽车有限公司
SG2661-1504-32007 | 包17 | 成交 | 郑州日产汽车有限公司
SG2661-1504-32007 | 包20 | 成交 | 东风汽车股份有限公司
SG2661-1504-32007 | 包21 | 成交 | 庆铃汽车股份有限公司
```

Expected extraction:

- Project/procurement code: `0711-26OTL01232004`
- Buyer/project unit fallback: `国家电网有限公司` from WCM metadata when the table
  omits `项目单位`.
- Agency: `国网物资有限公司`
- Outcome type: `Awarded`
- Awarded outcome rows: `140`
- Multiple suppliers under the same `分标编号 + 包号` must be preserved as separate
  awarded supplier records.

Observed extraction against the current parser:

```text
awards=140; candidates=127; outcomes=140
```

Current parser gaps:

- `ProjectCode` and `LotNo` are extracted correctly.
- `LotName` is empty because neither body nor table provides `分标名称`.
- `ProjectUnit`/outcome buyer is empty because the table omits `项目单位`; the
  parser should fall back to WCM metadata `bidOrgName`.
- Supplier cleanup incorrectly strips the leading `一` from valid supplier names
  such as `一汽红旗汽车销售有限公司` and `一汽丰田汽车销售有限公司`.
- Candidate extraction returns fewer rows than award/outcome extraction. During
  refactor, result announcements should use one awarded-result path and avoid
  relying on candidate count as the success signal.

Additional parser rules:

- Treat `成交状态` as a result status column.
- Preserve every row when the same package has multiple `成交供应商`; do not enforce
  one supplier per package for framework agreement/result tables.
- Leading Chinese numerals should only be trimmed when they are list markers
  such as `一、`, not when they are part of a supplier name such as `一汽`.

## Pattern 2: Result Rows In Attached PDF

Source example:

- `https://ecp.sgcc.com.cn/ecp2.0/portal/#/doc/doci-win/2606138588055959_2018060501171111`

Announcement:

- Title: `国网河南电力安阳供电公司2026年第一次服务授权框架协议竞争性谈判采购 成交结果公告`
- WCM API: `index/getNoticeWin`
- Notice id: `2606138588055959`
- Attachment API: `index/getWinFile`
- PDF attachment: `成交结果公告17FE02.pdf`
- Publish time: `2026-06-13 17:35:04`
- WCM metadata buyer: `国网河南省电力公司安阳供电公司`
- WCM metadata agency: `河南九域博大实业有限公司`

### Shape

The WCM `CONT` body contains only the cover/title and project code:

```text
国网河南电力安阳供电公司2026年第一次服务授权框架协议竞争性谈判采购
成交结果公告
（采购项目编号：17FE02）
```

The actual result rows are in the attached PDF. The PDF text extracted by
PdfPig starts with:

```text
国网河南电力安阳供电公司 2026 年第一次
服务授权框架协议竞争性谈判采购
成交结果公告
（采购项目编号：17FE02）
国网河南电力安阳供电公司 2026 年第一次服务授权框
架协议竞争性谈判采购工作已结束，现将成交结果公告如下：
序号 分标编号 分标名称 包名称 成交人
1 17FE02-9012002-
0001 办公服务
包 1-26 年展板
图文打印复印
服务
河南艺玖品创文化传媒有限公司
```

The PDF table columns are:

```text
序号 | 分标编号 | 分标名称 | 包名称 | 成交人
```

There is no separate `包号` column. The package number is embedded at the start
of `包名称`, for example `包 1-26 年展板图文打印复印服务`.

The PDF tail provides organization context:

```text
采购人：国网河南省电力公司安阳供电公司
代理机构：河南九域博大实业有限公司
2026 年 6 月 13 日
```

### Expected Extraction

Common context:

- Project/procurement code: `17FE02`
- Buyer: `国网河南省电力公司安阳供电公司`
- Agency: `河南九域博大实业有限公司`
- Outcome type: `Awarded`

Expected row counts:

- PDF sequence rows: `71`
- Awarded rows: `67`
- Skipped rows: `4` because the result text is `流标`

Representative awarded rows:

```text
1  | 17FE02-9012002-0001 | 办公服务 | 包1  | 26年展板图文打印复印服务 | 河南艺玖品创文化传媒有限公司
2  | 17FE02-9012002-0001 | 办公服务 | 包2  | 2026年科技项目文印 | 河南梵熙企业管理咨询有限公司
3  | 17FE02-9012002-0001 | 办公服务 | 包3  | 26年营销业务打印服务 | 河南艺玖品创文化传媒有限公司
10 | 17FE02-9011002-0002 | 车辆服务 | 包1  | 26年配网特种作业车辆维保 | 安阳市鼎龙巴士修理有限责任公司
14 | 17FE02-9013001-0005 | 房屋维修 | 包1  | 安阳公司南外环物资堆料场修缮 | 荣星建设集团有限公司
49 | 17FE02-9013003-0008 | 后勤服务 | 包1  | 内黄公司26年绿化美化 | 安阳优创实业有限责任公司内黄分公司
67 | 17FE02-9012021-0016 | 咨询服务 | 包1  | 林州北220千伏工程量审查 | 河南耀能工程管理咨询有限公司
```

Skipped flow rows:

```text
68 | 17FE02-9012023-0017 | 租赁服务 | 包1 | 2026年通勤班车租赁服务 | 流标
69 | 17FE02-9012023-0017 | 租赁服务 | 包2 | 林州26年客运车辆临时租赁 | 流标
70 | 17FE02-9001002-0004 | 电网工程施工 | 包1 | 26年低压台区线损治理及抢修 | 流标
71 | 17FE02-9013005-0013 | 特种设备维保 | 包1 | 安阳公司开发区立体车库拆除 | 流标
```

### Parser Rules

- Attachment text must be first-class evidence for result announcements. If WCM
  `CONT` has no result table but `getWinFile` returns a PDF, parse the PDF text
  before deciding the notice has no outcome rows.
- Treat repeated page headers such as `序号 分标编号 分标名称 包名称 成交人` as table
  boundaries, not data rows.
- Drop standalone page numbers such as `1`, `2`, `6`.
- Reconstruct a row from a sequence number followed by a split lot number:
  `17FE02-9012002-` plus next line `0001` becomes `17FE02-9012002-0001`.
- Continue consuming wrapped lines until the next sequence+lot line or the
  organization tail. The last organization-like text is the supplier/result
  value unless it is `流标`.
- Extract package number from the beginning of package name, supporting `包 1`,
  `包1`, `包 1-...`, and `包1...`.
- Preserve the remaining package text as package name after removing the package
  number prefix.
- Do not rely on fixed one-line rows. Supplier names and package names can wrap,
  including suffix splits such as `分公` + `司`.
- Skip `流标` rows and do not create supplier/outcome records for them.
- Use the PDF tail `采购人`/`代理机构` when present; otherwise fall back to WCM
  metadata.

### Current Parser Gap

Observed extraction against the current parser:

```text
awards=2; candidates=0; outcomes=65
```

Problems:

- The evidence award parser misclassified package-name fragments such as
  `林州公司` and `内黄公司` as awarded suppliers.
- The outcome text parser recovered many supplier rows, but lost structured lot
  fields and returned project code as `17FE02）` in some rows.
- One known supplier was partially trimmed: `九七（安阳）广告传媒有限公司` appeared as
  `（安阳）广告传媒有限公司`.
- The parser needs a dedicated PDF result-row reconstruction stage before the
  generic paragraph/table heuristics run.

### Variant 2B: Attached PDF Rows Without Sequence Column

Additional source example:

- `https://ecp.sgcc.com.cn/ecp2.0/portal/#/doc/doci-win/2606138571230714_2018060501171111`

Announcement:

- Title: `国网冀北电力有限公司2026年子公司、战新产业固定授权第三次服务框架协议（自主执行）公开竞争性谈判（送变电）成交公告`
- WCM API: `index/getNoticeWin`
- Notice id: `2606138571230714`
- Attachment API: `index/getWinFile`
- PDF attachment: `成交公告-服务框架竞谈.pdf`
- Publish time: `2026-06-13 11:34:56`
- WCM metadata buyer: `北京送变电有限公司（国网冀北电力有限公司输变电抢险检修中心）`
- WCM metadata agency: `国网冀北电力有限公司工程管理分公司（北京华联电力工程咨询有限公司）`

The WCM body contains only title/cover text and the project code:

```text
（采购编号：26BSZ07）
成交公告
详见附件
```

The PDF extracted text begins:

```text
分标编号 分标名称 包号 中标状
态 项目单位 成交人
26BSZ07-9011002 车辆服务 包 01 中标 国网冀北电力有限
公司所属子公司
首汽租赁有限责任
公司
26BSZ07-9011002 车辆服务 包 02 流标
```

Expected row counts:

- Result row starts: `42`
- Awarded rows: `31`
- Skipped rows: `11` because status is `流标`

Representative awarded rows:

```text
26BSZ07-9011002 | 车辆服务 | 包01 | 国网冀北电力有限公司所属子公司 | 首汽租赁有限责任公司
26BSZ07-9011002 | 车辆服务 | 包05 | 国网冀北电力有限公司所属子公司 | 国网河北电动汽车服务有限公司
26BSZ07-9013003 | 后勤服务 | 包15 | 国网冀北电力有限公司所属子公司 | 霸州市提香温泉小镇物业服务有限公司
26BSZ07-9012006 | 技术服务 | 包17 | 国网冀北电力有限公司所属子公司 | 山西科能昊通建设项目管理咨询有限公司
26BSZ07-9003001 | 综合服务 | 包38 | 国网冀北电力有限公司所属子公司 | 北京兴华会计师事务所（特殊普通合伙）
26BSZ07-9003001 | 综合服务 | 包42 | 国网冀北电力有限公司所属子公司 | 中光华建设工程造价咨询有限公司
```

Representative skipped rows:

```text
26BSZ07-9011002 | 车辆服务 | 包02 | 流标
26BSZ07-9011002 | 车辆服务 | 包03 | 流标
26BSZ07-9011002 | 车辆服务 | 包04 | 流标
26BSZ07-9011002 | 车辆服务 | 包06 | 流标
26BSZ07-9012006 | 技术服务 | 包27 | 流标
26BSZ07-9003002 | 运维服务 | 包29 | 流标
```

Additional parser rules for this variant:

- Support PDF row starts without a sequence column:
  `分标编号 分标名称 包号 状态 项目单位 [成交人 lines...]`.
- Rejoin split headers such as `中标状` + `态 项目单位 成交人`.
- Rejoin project unit text split across lines, for example
  `国网冀北电力有限` + `公司所属子公司`.
- Treat the supplier as the organization text after project unit, continuing
  until the next lot row start or document tail.
- Skip rows where status is `流标`.
- WCM body does not contain `采购人`/`代理机构`; use WCM metadata for those fields.

Observed extraction against the current parser:

```text
awards=0; candidates=0; outcomes=0
```

This confirms the current parser cannot yet recover this attached-PDF subtype.

### Variant 2C: Attached PDF Rows With Procurement Agency Service Fee

Additional source example:

- `https://ecp.sgcc.com.cn/ecp2.0/portal/#/doc/doci-win/2606128546355295_2018060501171111`

Announcement:

- Title: `国网陕西电力2026年新增第一次服务授权联合公开谈判采购（一）项目成交结果公告（二）`
- WCM API: `index/getNoticeWin`
- Notice id: `2606128546355295`
- Attachment API: `index/getWinFile`
- PDF attachment: `成交结果公告（二） .pdf`
- Publish time: `2026-06-12 22:30:20`
- WCM metadata buyer: `国网陕西省电力公司商洛供电公司`
- WCM metadata agency: `西北电能成套公司`

The WCM body is only a short placeholder:

```text
详见附件
```

The PDF text contains the project code and result table:

```text
国网陕西电力2026年新增第一次服务授权
联合公开谈判采购（一）项目
成交结果公告（二）
（采购项目编号：26FGBL）
...
序号 分标编号 包号 成交人 采购代理服务费 （元）
1 标 04 包 001 西安英诺视通科技有限公司 7879
2 标 04 包 002 西安英诺视通科技有限公司 1552
3 标 04 包 003 陕西尚智博纳品牌营销咨询
有限公司 6177
```

The PDF table columns are:

```text
序号 | 分标编号 | 包号 | 成交人 | 采购代理服务费（元）
```

There is no `分标名称`, `项目单位`, `包名称`, or explicit status column in the table.
Every sequence row in the sample is an awarded row.

Expected extraction:

- Project/procurement code: `26FGBL`
- Buyer: prefer PDF tail `国网陕西省电力有限公司商洛供电公司`; WCM metadata has
  the shorter `国网陕西省电力公司商洛供电公司`.
- Agency: prefer PDF tail `西北（西安）电能成套设备有限公司`; WCM metadata has
  the shorter `西北电能成套公司`.
- PDF sequence rows: `225`
- Awarded rows: `225`
- Procurement agency service fee rows: `225`
- Fee range: `35` to `48647` yuan

Representative rows:

```text
1   | 标04 | 包001 | 西安英诺视通科技有限公司 | 采购代理服务费=7879
3   | 标04 | 包003 | 陕西尚智博纳品牌营销咨询有限公司 | 采购代理服务费=6177
4   | 标04 | 包004 | 巨商控股（山东）集团有限公司 | 采购代理服务费=7587
28  | 标15 | 包008 | 中国能源建设集团陕西省电力设计院有限公司 | 采购代理服务费=399
50  | 标16 | 包020 | 陕西电力科隆发展有限责任公司 | 采购代理服务费=48647
74  | 标18 | 包020 | 中汇会计师事务所(特殊普通合伙) | 采购代理服务费=9562
81  | 标18 | 包029 | 西安市勘察测绘院（西安地理信息中心、西安市自然资源卫星应用技术中心）） | 采购代理服务费=16128
210 | 标42 | 包006 | 西安车咖物联网信息技术有限公司 | 采购代理服务费=35
225 | 标47 | 包004 | 西安卓飞科技工程有限责任公司 | 采购代理服务费=15448
```

The PDF tail contains fee-payment instructions and organization context:

```text
汇款备注：服务费-26FGBL
采购人：国网陕西省电力有限公司商洛供电公司
代理机构：西北（西安）电能成套设备有限公司
2026 年 6 月 12 日
```

Additional parser rules for this variant:

- Support row starts shaped as
  `序号 标 xx 包 xxx [成交人 text...] [采购代理服务费]`.
- The sequence row may not contain supplier text. For example row `81` starts as
  `81 标 18 包 029`, while supplier and fee appear on following lines.
- Reconstruct wrapped supplier names until the next sequence row, repeated table
  header, or document tail.
- Stop result-row reconstruction at `备注：`, fee-account instructions, `采购人：`,
  `代理机构：`, or date tail.
- Treat the final numeric token in a reconstructed row as
  `采购代理服务费（元）`, not as award amount. It should not populate
  `AwardAmount`.
- Add a separate structured field for the supplier-payable procurement agency
  service fee, for example `ProcurementAgencyServiceFeeAmount`, or preserve it in
  row metadata/evidence until a first-class column is added.
- Preserve raw `标04` / `包001` text while also allowing normalized lot/package
  keys for matching.
- Use PDF tail buyer/agency values when present; they are more complete than WCM
  metadata in this sample.

Observed extraction against the current parser:

```text
awards=350; candidates=0; outcomes=149
```

Current parser gaps:

- Generic award parsing over-extracts rows from PDF text.
- Outcome extraction creates malformed rows such as `lot=标`, `package=04`,
  `supplier=包`.
- Supplier names can be partially truncated, for example
  `巨商控股（山东）集团有限公司` becomes `巨商控股（山东）集团`.
- The current model has no dedicated place for `采购代理服务费（元）`; this field is
  materially different from award amount and should not be mixed with it.

### Variant 2D: Small Attached PDF With Lot Name Only And Procurement-Code Suffix

Additional source example:

- `https://ecp.sgcc.com.cn/ecp2.0/portal/#/doc/doci-win/2606128544681964_2018060501171111`

Announcement:

- Title: `国网河南电力漯河供电公司2026年第二次服务授权框架竞争性谈判采购成交结果公告`
- WCM API: `index/getNoticeWin`
- Notice id: `2606128544681964`
- Attachment API: `index/getWinFile`
- PDF attachment: `04成交结果公告.pdf`
- Publish time: `2026-06-12 21:45:55`
- WCM metadata buyer: `国网河南省电力公司漯河供电公司`
- WCM metadata agency: `河南立新监理咨询有限公司`

The WCM body is only:

```text
详见附件
```

The PDF is small and contains the actual project code and result rows:

```text
国网河南电力漯河供电公司 2026 年第二次服务授权框架竞
争性谈判采购成交结果公告
（采购编号：17FL04）
...
分标名称 包号及包名称 成交人
电网工程服务-电
网工程施工
包 1 城区低压台区运行维
护施工 山东梅格彤天电气有限公司
```

The PDF table columns are:

```text
分标名称 | 包号及包名称 | 成交人
```

There is no `序号`, no `分标编号`, and no explicit status column. The attachment
file name prefix `04` corresponds to the procurement code suffix in `17FL04`; it
is not a package number and should not be used as `PackageNo`.

Expected extraction:

- Project/procurement code: `17FL04`
- Lot number: empty unless an explicit mapping is available. Do not invent
  `04`, `17FL04`, or `包` as `LotNo`.
- Lot name: read from the first PDF column, for example
  `电网工程服务-电网工程施工`, `运维服务-房屋维修`,
  `综合服务-技术服务`.
- Package number/name: read from `包号及包名称`, for example
  `包1 | 城区低压台区运行维护施工`.
- Buyer: `国网河南省电力公司漯河供电公司`
- Agency: `河南立新监理咨询有限公司`
- Package markers/result rows: `42`
- Skipped rows: `2` because the supplier/result is `流标`
- Awarded rows: `40`

Representative rows:

```text
电网工程服务-电网工程施工 | 包1  | 城区低压台区运行维护施工 | 山东梅格彤天电气有限公司
运维服务-房屋维修 | 包1 | 变电站房屋维修 | 山东中星安装工程有限公司
运维服务-房屋维修 | 包5 | 临颍县供电公司零星维修 | 河南九域博大实业有限公司
零星服务-消防服务 | 包1 | 生产区域消防设施维保检测 | 河南科瑞消防工程有限公司
零星服务-广告宣传服务 | 包2 | 舞阳公司党组织工作服务 | 辽宁荔晟电力科技有限公司
运维服务-非电网设备维保 | 包1 | 实训基地维修 | 流标
综合服务-中介服务 | 包1 | 临颍公司项目两算委托审计服务 | 致同会计师事务所（特殊普通合伙）河南分所
综合服务-中介服务 | 包2 | 临颍公司26年外聘律师服务 | 流标
综合服务-电力设施保护 | 包7 | 电缆中间接头热熔接提升 | 杭州悦玛电力技术有限公司
综合服务-技术服务 | 包18 | 10千伏配线路选线规划技术 | 河南图惠数据科技有限公司
```

Additional parser rules for this variant:

- Extract `采购编号：17FL04` from PDF body and strip surrounding punctuation such
  as `）`; this is the project/procurement code for every row in this attachment.
- Use the attachment filename prefix only as a weak consistency hint:
  `04成交结果公告.pdf` matches the `04` suffix in `17FL04`.
- Support tables without sequence numbers by treating `包\s*\d+` as row starts
  only after a known lot-name context is available.
- Rejoin wrapped lot names such as `综合服务-技术服` + `务`.
- Rejoin wrapped package names and supplier names.
- Skip rows whose supplier/result is `流标`.
- The PDF text extractor can place footer lines before the final row. In this
  sample `采购人`/`代理机构`/date appear before `包18`; parser logic should filter
  those tail lines but continue scanning for subsequent table-like rows.
- Do not treat `分标名称` as `分标编号`; it should populate `LotName`, not `LotNo`.

Observed extraction against the current parser:

```text
awards=17; candidates=0; outcomes=55
```

Current parser gaps:

- Project code is sometimes emitted as `17FL04）` with a trailing Chinese
  closing parenthesis.
- Generic outcome extraction loses lot names and package names.
- Some malformed rows use `lot=包`, `lotName=包`, or supplier text copied from a
  package-name fragment such as `城区低压台区运行维`.
- Flow rows are not consistently excluded from the malformed generic output.
