# 多租户门店层级数据共享设计方案

## 一、核心设计思想

### 1.1 统一门店概念

**所有节点都是门店（Store）**，包括：
- **平台总部**：虚拟门店，作为整个组织的根节点
- **加盟商总部**：加盟商的管理中心门店
- **直营门店**：实际经营的直营店
- **加盟门店**：实际经营的加盟店

### 1.2 层级关系

通过 **`ParentStoreId`** 字段建立树形结构：

```
平台总部（StoreId=0, Type=Headquarters, ParentStoreId=NULL）
│
├─ 平台直营门店A（StoreId=1, Type=DirectOperated, ParentStoreId=0）
├─ 平台直营门店B（StoreId=2, Type=DirectOperated, ParentStoreId=0）
├─ 平台直营门店C（StoreId=3, Type=DirectOperated, ParentStoreId=0）
│
├─ 加盟商X总部（StoreId=100, Type=FranchiseHeadquarters, ParentStoreId=0）
│   ├─ 加盟商X直营门店D（StoreId=101, Type=DirectOperated, ParentStoreId=100）
│   ├─ 加盟商X直营门店E（StoreId=102, Type=DirectOperated, ParentStoreId=100）
│   ├─ 加盟商X加盟门店F（StoreId=103, Type=Franchised, ParentStoreId=100）
│   └─ 加盟商X加盟门店G（StoreId=104, Type=Franchised, ParentStoreId=100）
│
└─ 加盟商Y总部（StoreId=200, Type=FranchiseHeadquarters, ParentStoreId=0）
    ├─ 加盟商Y直营门店H（StoreId=201, Type=DirectOperated, ParentStoreId=200）
    ├─ 加盟商Y直营门店I（StoreId=202, Type=DirectOperated, ParentStoreId=200）
    ├─ 加盟商Y直营门店J（StoreId=203, Type=DirectOperated, ParentStoreId=200）
    ├─ 加盟商Y加盟门店K（StoreId=204, Type=Franchised, ParentStoreId=200）
    └─ 加盟商Y加盟门店L（StoreId=205, Type=Franchised, ParentStoreId=200）
```

---

## 二、门店类型定义

### 2.1 StoreType 枚举

| 类型 | 说明 | ParentStoreId | 特点 |
|------|------|---------------|------|
| **Headquarters** | 平台总部 | NULL | 虚拟节点，不实际经营 |
| **FranchiseHeadquarters** | 加盟商总部 | 0（平台总部） | 加盟商管理中心 |
| **DirectOperated** | 直营门店 | 0 或加盟商总部ID | 实际经营的直营店 |
| **Franchised** | 加盟门店 | 加盟商总部ID | 实际经营的加盟店 |

### 2.2 表结构

```sql
CREATE TABLE Stores (
    Id BIGINT PRIMARY KEY,
    TenantId BIGINT NOT NULL,          -- 所有门店同一个TenantId
    Name NVARCHAR(100) NOT NULL,       -- 门店名称
    Type INT NOT NULL,                 -- 门店类型（枚举）
    ParentStoreId BIGINT NULL,         -- 上级门店ID（NULL表示根节点）
    IsActive BIT DEFAULT 1,            -- 是否启用
    CreatedAt DATETIME NOT NULL,
    UpdatedAt DATETIME NULL,
    
    INDEX IX_ParentStoreId (ParentStoreId),
    INDEX IX_TenantId_Type (TenantId, Type)
);
```

---

## 三、数据共享规则

### 3.1 核心原则

| 规则编号 | 规则描述 | 示例 |
|---------|---------|------|
| **R1** | **总部与下级直营门店双向共享** | 平台总部 ↔ A/B/C；加盟商X总部 ↔ D/E |
| **R2** | 同一总部下的**所有直营门店**之间**互相共享** | A、B、C 互相可见；D、E 互相可见 |
| **R3** | **加盟门店数据独享**，不与任何门店共享 | F、G、K、L 各自独享 |
| **R4** | 不同总部体系之间的门店**完全隔离** | 平台体系 ↮ 加盟商X体系 ↮ 加盟商Y体系 |
| **R5** | 总部创建的数据，下级直营门店可见可用 | 平台总部创建商品 → A/B/C 可见 |

### 3.2 共享范围计算逻辑

给定当前门店 `CurrentStoreId`，计算可访问的门店列表 `ShareStoreIds`：

```plaintext
IF CurrentStore.Type == Franchised THEN
    -- 加盟门店：独享
    ShareStoreIds = [CurrentStoreId]
    
ELSE IF CurrentStore.Type IN (Headquarters, FranchiseHeadquarters) THEN
    -- 总部：与下级所有直营门店共享
    ShareStoreIds = SELECT Id FROM Stores 
                    WHERE (Id = CurrentStoreId  -- 总部自己
                           OR (ParentStoreId = CurrentStoreId 
                               AND Type = DirectOperated))  -- 下级直营门店
    
ELSE IF CurrentStore.Type == DirectOperated THEN
    -- 直营门店：与同级直营门店 + 上级总部共享
    ShareStoreIds = SELECT Id FROM Stores 
                    WHERE ((ParentStoreId = CurrentStore.ParentStoreId 
                            AND Type = DirectOperated)  -- 同级直营门店
                           OR Id = CurrentStore.ParentStoreId)  -- 上级总部
    
ELSE
    ShareStoreIds = [CurrentStoreId]
END IF
```

---

## 四、数据实体分类

### 4.1 ISharedEntity（共享数据）

**适用场景**：商品、会员、促销、优惠券等需要在一定范围内共享的数据

**共享规则**：
- 平台直营门店创建 → 平台所有直营门店可见
- 加盟商直营门店创建 → 该加盟商所有直营门店可见
- 加盟门店创建 → 仅创建门店自己可见

**查询过滤**：`WHERE StoreId IN (ShareStoreIds)`

### 4.2 IStoreOnlyEntity（门店独享数据）

**适用场景**：订单、库存、收银记录等门店独立经营数据

**隔离规则**：无论门店类型，数据完全独享

**查询过滤**：`WHERE StoreId = CurrentStoreId`

---

## 五、完整可见性矩阵

### 5.1 门店基础数据

| StoreId | 门店名称 | Type | ParentStoreId | 所属组织 |
|---------|---------|------|---------------|---------|
| 0 | 平台总部 | Headquarters | NULL | - |
| 1 | 平台直营A | DirectOperated | 0 | 平台 |
| 2 | 平台直营B | DirectOperated | 0 | 平台 |
| 3 | 平台直营C | DirectOperated | 0 | 平台 |
| 100 | 加盟商X总部 | FranchiseHeadquarters | 0 | 加盟商X |
| 101 | 加盟商X直营D | DirectOperated | 100 | 加盟商X |
| 102 | 加盟商X直营E | DirectOperated | 100 | 加盟商X |
| 103 | 加盟商X加盟F | Franchised | 100 | 加盟商X |
| 104 | 加盟商X加盟G | Franchised | 100 | 加盟商X |
| 200 | 加盟商Y总部 | FranchiseHeadquarters | 0 | 加盟商Y |
| 201 | 加盟商Y直营H | DirectOperated | 200 | 加盟商Y |
| 202 | 加盟商Y直营I | DirectOperated | 200 | 加盟商Y |
| 203 | 加盟商Y直营J | DirectOperated | 200 | 加盟商Y |
| 204 | 加盟商Y加盟K | Franchised | 200 | 加盟商Y |
| 205 | 加盟商Y加盟L | Franchised | 200 | 加盟商Y |

### 5.2 ISharedEntity 可见性矩阵

假设每个门店/总部创建了一条商品：

| 当前门店 | ShareStoreIds | 可见商品（创建门店） |
|---------|---------------|-------------------|
| **平台总部 (0)** | [0, 1, 2, 3] | Product_平台, Product_A, Product_B, Product_C |
| 平台直营A (1) | [0, 1, 2, 3] | Product_平台, Product_A, Product_B, Product_C |
| 平台直营B (2) | [0, 1, 2, 3] | Product_平台, Product_A, Product_B, Product_C |
| 平台直营C (3) | [0, 1, 2, 3] | Product_平台, Product_A, Product_B, Product_C |
| **加盟商X总部 (100)** | [100, 101, 102] | Product_X总部, Product_D, Product_E |
| 加盟商X直营D (101) | [100, 101, 102] | Product_X总部, Product_D, Product_E |
| 加盟商X直营E (102) | [100, 101, 102] | Product_X总部, Product_D, Product_E |
| 加盟商X加盟F (103) | [103] | Product_F（仅自己） |
| 加盟商X加盟G (104) | [104] | Product_G（仅自己） |
| **加盟商Y总部 (200)** | [200, 201, 202, 203] | Product_Y总部, Product_H, Product_I, Product_J |
| 加盟商Y直营H (201) | [200, 201, 202, 203] | Product_Y总部, Product_H, Product_I, Product_J |
| 加盟商Y直营I (202) | [200, 201, 202, 203] | Product_Y总部, Product_H, Product_I, Product_J |
| 加盟商Y直营J (203) | [200, 201, 202, 203] | Product_Y总部, Product_H, Product_I, Product_J |
| 加盟商Y加盟K (204) | [204] | Product_K（仅自己） |
| 加盟商Y加盟L (205) | [205] | Product_L（仅自己） |

### 5.3 IStoreOnlyEntity 可见性

| 当前门店 | 可见订单 |
|---------|---------|
| 平台直营A (1) | Order_A（仅自己） |
| 平台直营B (2) | Order_B（仅自己） |
| 加盟商X直营D (101) | Order_D（仅自己） |
| 加盟商X加盟F (103) | Order_F（仅自己） |
| ... | ... |

---

## 六、业务场景示例

### 6.1 场景：总部创建基础商品（ISharedEntity）

```
- 平台总部创建「基础商品套餐」
- 加盟商X总部创建「X品牌专属商品」
- 加盟商Y总部创建「Y品牌专属商品」
```

**查询结果**：

| 查询门店 | 可见商品 |
|---------|---------|
| 平台总部 | 基础商品套餐 |
| 平台直营A/B/C | 基础商品套餐 |
| 加盟商X总部 | X品牌专属商品 |
| 加盟商X直营D/E | X品牌专属商品 |
| 加盟商X加盟F/G | 无（不可见总部商品） |
| 加盟商Y总部 | Y品牌专属商品 |
| 加盟商Y直营H/I/J | Y品牌专属商品 |
| 加盟商Y加盟K/L | 无（不可见总部商品） |

**业务价值**：
- ✅ 平台总部可统一创建标准商品，自动同步到所有平台直营门店
- ✅ 加盟商总部可创建品牌专属商品，仅在自己的直营门店销售
- ✅ 加盟门店保持独立性，不受总部商品库影响

### 6.2 场景：门店创建本地商品（ISharedEntity）

```
- 平台直营A创建「A店特色商品」
- 加盟商X直营D创建「D店本地商品」
- 加盟商X加盟F创建「F店独家商品」
```

**查询结果**：

| 查询门店 | 可见商品 |
|---------|---------|
| 平台总部 | 基础商品套餐 + A店特色商品 |
| 平台直营A | 基础商品套餐 + A店特色商品 |
| 平台直营B/C | 基础商品套餐 + A店特色商品 |
| 加盟商X总部 | X品牌专属商品 + D店本地商品 |
| 加盟商X直营D | X品牌专属商品 + D店本地商品 |
| 加盟商X直营E | X品牌专属商品 + D店本地商品 |
| 加盟商X加盟F | F店独家商品（仅自己） |
| 加盟商X加盟G | 无 |

### 6.3 场景：总部分发数据到门店

**分发流程**：

```
1. 平台总部创建「双十一促销活动」
   - StoreId = 0（平台总部）
   - 自动对所有平台直营门店可见

2. 加盟商X总部创建「X品牌周年庆」
   - StoreId = 100（加盟商X总部）
   - 自动对加盟商X的直营门店D、E可见

3. 门店可基于总部数据创建本地副本
   - 平台直营A基于「双十一促销活动」创建本地活动
   - StoreId = 1（平台直营A）
   - 保留对总部活动的引用（可选：SourceStoreId = 0）
```

**数据模型示例**：

| Id | StoreId | Title | SourceStoreId | 创建来源 |
|----|---------|-------|---------------|---------|
| 1 | 0 | 双十一促销活动 | NULL | 平台总部创建 |
| 2 | 1 | 双十一促销活动-A店 | 0 | 基于总部分发 |
| 3 | 2 | 双十一促销活动-B店 | 0 | 基于总部分发 |
| 4 | 100 | X品牌周年庆 | NULL | 加盟商X总部创建 |
| 5 | 101 | X品牌周年庆-D店 | 100 | 基于总部分发 |

### 6.4 场景：会员注册（ISharedEntity）

```
- 平台总部导入「VIP会员批量数据」
- 平台直营A注册会员「张三」
- 加盟商X直营D注册会员「王五」
- 加盟商X加盟F注册会员「赵六」
```

**查询结果**：

| 查询门店 | 可见会员 |
|---------|---------|
| 平台总部 | VIP批量数据、张三 |
| 平台直营A | VIP批量数据、张三 |
| 平台直营B/C | VIP批量数据、张三 |
| 加盟商X总部 | 王五 |
| 加盟商X直营D | 王五 |
| 加盟商X直营E | 王五 |
| 加盟商X加盟F | 赵六（仅自己） |
| 加盟商X加盟G | 无 |

```
所有门店各自生成订单
```

**查询结果**：每个门店只能看到自己的订单，无共享

---

## 七、设计优势

### 7.1 结构统一性

- **所有节点都是门店**，概念清晰
- **通过 Type 区分角色**，通过 ParentStoreId 建立关系
- **树形结构天然支持多层级**（可扩展为城市代理、区域代理等）

### 7.2 查询灵活性

- **向上查找**：快速找到所属加盟商总部（`ParentStoreId`）
- **向下查找**：快速找到所有下级门店（`WHERE ParentStoreId = X`）
- **同级查找**：快速找到共享范围（`WHERE ParentStoreId = X AND Type = DirectOperated`）

### 7.3 扩展性

支持未来扩展为更复杂的层级：

```
平台总部
├─ 大区总部（新增 Type: RegionHeadquarters）
│   ├─ 城市代理（新增 Type: CityAgent）
│   │   ├─ 加盟商总部
│   │   │   ├─ 直营门店
│   │   │   └─ 加盟门店
```

### 7.4 权限管理友好

- **基于 ParentStoreId 的层级权限**：
  - 平台总部可管理所有门店
  - 加盟商总部只能管理自己的下级门店
  - 门店只能管理自己的数据

---

## 八、关键验证点

### 8.1 ✅ 正确的访问关系

| 门店A | 门店B | 能否互访 | 原因 |
|-------|-------|---------|------|
| 平台总部 | 平台直营A | ✅ | 总部与下级直营门店双向共享 |
| 平台直营A | 平台直营B | ✅ | ParentStoreId 都是 0，都是直营 |
| 平台总部 | 平台直营B | ✅ | 总部与下级直营门店双向共享 |
| 加盟商X总部 | 加盟商X直营D | ✅ | 总部与下级直营门店双向共享 |
| 加盟商X直营D | 加盟商X直营E | ✅ | ParentStoreId 都是 100，都是直营 |
| 加盟商Y总部 | 加盟商Y直营H | ✅ | 总部与下级直营门店双向共享 |
| 加盟商Y直营H | 加盟商Y直营I | ✅ | ParentStoreId 都是 200，都是直营 |

### 8.2 ❌ 错误的访问关系

| 门店A | 门店B | 能否互访 | 原因 |
|-------|-------|---------|------|
| 平台总部 | 加盟商X总部 | ❌ | 不同总部体系完全隔离 |
| 平台总部 | 加盟商X直营D | ❌ | 不同总部体系完全隔离 |
| 平台直营A | 加盟商X直营D | ❌ | ParentStoreId 不同（0 vs 100） |
| 加盟商X总部 | 加盟商X加盟F | ❌ | 加盟门店独享，总部不可见 |
| 加盟商X直营D | 加盟商Y直营H | ❌ | ParentStoreId 不同（100 vs 200） |
| 加盟商X直营D | 加盟商X加盟F | ❌ | 加盟门店独享 |
| 加盟商X加盟F | 加盟商X加盟G | ❌ | 加盟门店之间互相独享 |

---

## 九、注意事项

### 9.1 总部数据分发策略

#### 策略1：直接共享（推荐）

**适用场景**：标准化商品、会员、促销等

**实现方式**：
- 总部创建数据时 `StoreId = 总部ID`
- 利用共享规则自动对下级直营门店可见
- 下级门店**直接使用总部数据**，无需复制

**优点**：
- ✅ 数据唯一，总部修改实时同步
- ✅ 无数据冗余
- ✅ 实现简单

**缺点**：
- ⚠️ 门店无法个性化修改

#### 策略2：复制分发（可选）

**适用场景**：需要门店个性化调整的数据（如价格、库存）

**实现方式**：

```sql
-- 数据表增加 SourceStoreId 字段
ALTER TABLE Products ADD SourceStoreId BIGINT NULL;

-- 总部创建原始数据
INSERT INTO Products (StoreId, Name, Price, SourceStoreId)
VALUES (0, '基础商品', 100, NULL);

-- 分发到门店（创建副本）
INSERT INTO Products (StoreId, Name, Price, SourceStoreId)
SELECT 1, Name, Price * 0.9, 0  -- 门店可调整价格
FROM Products WHERE Id = 原始商品ID;
```

**优点**：
- ✅ 门店可独立修改
- ✅ 保留数据溯源（SourceStoreId）

**缺点**：
- ⚠️ 数据冗余
- ⚠️ 总部修改不会自动同步

#### 策略3：混合模式（灵活）

**实现方式**：

```sql
-- 增加 IsCustomized 字段
ALTER TABLE Products ADD IsCustomized BIT DEFAULT 0;

-- 门店使用总部数据时：
--   IsCustomized = 0：显示总部数据（实时）
--   IsCustomized = 1：显示门店自定义数据（副本）
```

**查询逻辑**：

```sql
-- 门店查询商品
SELECT * FROM Products
WHERE StoreId IN (ShareStoreIds)  -- 共享范围
  AND (IsCustomized = 0           -- 使用总部数据
       OR StoreId = CurrentStoreId);  -- 或自己的自定义数据
```

### 9.2 总部门店的特殊处理

- **平台总部（StoreId=0）**和**加盟商总部**是管理中心门店
- **总部可以创建业务数据**（商品、会员、促销等），自动对下级直营门店可见
- 总部数据 = 标准化基础数据，门店可直接使用或基于此创建个性化版本

### 9.3 数据溯源

所有业务数据应记录：
- `StoreId`：创建门店（当前门店ID）
- `ParentStoreId`：创建门店的上级（冗余字段，便于快速查询归属）
- `SourceStoreId`：数据来源门店（可选，用于分发场景）
  - `NULL`：原始创建
  - `具体值`：基于某门店数据创建的副本

### 9.4 缓存策略

```
缓存Key: ShareStores:{StoreId}
缓存内容: List<long> shareStoreIds
失效策略:
  - 门店创建/删除时清除相关缓存
  - 门店 ParentStoreId 变更时清除相关缓存
  - 门店 Type 变更时清除相关缓存
```

### 9.5 权限控制

```
平台总部（StoreId=0）
  ├─ 可查看、管理所有平台直营门店数据（超级管理员）
  ├─ 可创建标准化数据，自动同步到下级直营门店
  ├─ 不可查看加盟商体系数据
  
加盟商总部（StoreId=100）
  ├─ 可查看、管理 ParentStoreId=100 的所有直营门店数据
  ├─ 可创建品牌数据，自动同步到下级直营门店
  ├─ 不可查看平台直营数据
  ├─ 不可查看其他加盟商数据
  ├─ 不可查看加盟门店数据（加盟门店独享）
  
直营门店（StoreId=1/101）
  ├─ 可查看总部数据 + 同级直营门店数据
  ├─ 可创建门店数据，在共享范围内可见
  ├─ 不可查看加盟门店数据
  
加盟门店（StoreId=103）
  ├─ 只能查看自己的数据（完全独享）
  ├─ 不可查看总部数据
  ├─ 不可查看其他门店数据
```

---