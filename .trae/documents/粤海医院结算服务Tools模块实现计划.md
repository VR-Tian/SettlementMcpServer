# 粤海医院结算服务 Tools 模块实现计划

## 当前状态分析

项目已有一套完整的 MCP 服务架构，包括：

- **Tools 层**: `AuditServerTools`（医保审核工具）、`RandomNumberTools`
- **Contracts 层**: `IAuditDataRepository`、`IDbConnectionFactory`、`IExcelExportService`
- **Infrastructure 层**: `OracleAuditDataRepository`、`OracleDbConnectionFactory`、`AuditedResultTypeMap`、`MiniExcelExportService`
- **Models 层**: `AuditedResult`、`AuditedResultQueryFilter`、`AuditedResultPagination`
- **依赖注入**: 通过 `ServiceCollectionExtensions` 注册服务

## 新增功能：粤海医院结算数据分页查询

### 实现内容

遵循现有架构模式，新增以下文件/代码：

### 1. Models 层 - 结算数据模型

**文件**: `Models/YuehaiSettlement.cs`

- **YuehaiSettlementQueryFilter**: 结算查询过滤器， 可选过滤条件
  - 就诊ID、结算ID、人员编号、病历号、住院/门诊号、险种类型、医疗类别作为可选过滤字段
- **YuehaiSettlement**: 结算数据结果模型，映射到表 `YB_粤海医保结算全量数据`
  - 所有字段使用英文属性名，通过 TypeMap 映射到中文列名
  - 字段类型：字符串用 `string?`，数值用 `decimal?`
- **YuehaiSettlementPagination**: 分页元数据 record（复用现有 `AuditedResultPagination` 模式）

### 2. Contracts 层 - 结算数据仓储接口

**文件**: `Contracts/IYuehaiSettlementDataRepository.cs`

- `IYuehaiSettlementDataRepository` 接口：
  - `Task<IReadOnlyList<YuehaiSettlement>> QuerySettlementsAsync(YuehaiSettlementQueryFilter filter, CancellationToken ct)`
  - `Task<int> CountSettlementsAsync(YuehaiSettlementQueryFilter filter, CancellationToken ct)`

### 3. Infrastructure 层 - Oracle 结算数据仓储实现

**文件**: `Infrastructure/OracleYuehaiSettlementDataRepository.cs`

- 实现 `IYuehaiSettlementDataRepository` 接口
- 复用现有 `OracleAuditDataRepository` 的分页模式（ROWNUM 嵌套查询）
- 复用 `IDbConnectionFactory` 和 Dapper 模式
- 目标表: `YB_粤海医保结算全量数据`

### 4. Infrastructure 层 - Dapper 类型映射

**文件**: `Infrastructure/YuehaiSettlementTypeMap.cs`

- 实现 `SqlMapper.ITypeMap` 接口（复用 `AuditedResultTypeMap` 模式）
- 维护中文列名到英文属性名的映射字典

### 5. Tools 层 - 结算服务 MCP 工具

**文件**: `Tools/YuehaiSettlementTools.cs`

- `YuehaiSettlementTools` 类，依赖注入 `IYuehaiSettlementDataRepository`
- 提供两个 MCP 工具方法：
  1. `GetSettlementCountAsync` - 获取总记录数和分页元数据（步骤 1）
  2. `QuerySettlementsAsync` - 分页查询结算明细数据（步骤 2）

### 6. Extensions 层 - 服务注册扩展

**文件**: 修改 `Extensions/ServiceCollectionExtensions.cs`

- 新增 `AddYuehaiSettlementDataAccess()` 扩展方法：
  - 注册 Dapper 类型映射 `YuehaiSettlementTypeMap.Register()`
  - 复用已有的 `IDbConnectionFactory`（如果连接字符串相同则不需重复注册）
  - 注册 `IYuehaiSettlementDataRepository → OracleYuehaiSettlementDataRepository`

### 7. Program.cs 修改

**文件**: `Program.cs`

- 在现有 `AddOracleDataAccess()` 后调用 `builder.Services.AddYuehaiSettlementDataAccess()`
- 在 `.WithTools<AuditServerTools>()` 后添加 `.WithTools<YuehaiSettlementTools>()`

## 字段映射表

| Oracle 列名 | C# 属性名                 | 类型       |
| --------- | ---------------------- | -------- |
| 就诊ID      | VisitId                | string?  |
| 结算ID      | SettlementId           | string?  |
| 记账流水号     | AccountingSerialNo     | string?  |
| 有效标志      | ValidFlag              | string?  |
| 处方医嘱号     | PrescriptionOrderNo    | string?  |
| 定点医药机构编号  | InstitutionCode        | string?  |
| 定点医药机构名称  | InstitutionName        | string?  |
| 人员编号      | PersonnelNo            | string?  |
| 人员姓名      | PersonnelName          | string?  |
| 人员证件类型    | IDType                 | string?  |
| 证件号码      | IDNumber               | string?  |
| 年龄        | Age                    | decimal? |
| 病历号       | MedicalRecordNo        | string?  |
| 住院\_门诊号   | InpatientOutpatientNo  | string?  |
| 住院天数      | HospitalDays           | decimal? |
| 开始日期      | StartDate              | string?  |
| 结束日期      | EndDate                | string?  |
| 结算时间      | SettlementTime         | string?  |
| 住院主诊断名称   | PrimaryDiagnosisName   | string?  |
| 入院科室名称    | AdmissionDeptName      | string?  |
| 出院科室名称    | DischargeDeptName      | string?  |
| 人员参保关系ID  | InsuranceRelationId    | string?  |
| 参保所属医保区划  | InsuranceRegion        | string?  |
| 险种类型1     | InsuranceType1         | string?  |
| INSUTYPE  | InsuType               | string?  |
| 支付地点类别1   | PaymentLocationType1   | string?  |
| 支付地点类别    | PaymentLocationType    | string?  |
| 医疗类别1     | MedicalCategory1       | string?  |
| 医疗类别      | MedicalCategory        | string?  |
| 录入方式      | EntryMode              | string?  |
| 数据分割      | DataSplit              | string?  |
| 费用明细流水号   | FeeDetailSerialNo      | string?  |
| 费用发生时间    | FeeOccurrenceTime      | string?  |
| 数量        | Quantity               | decimal? |
| 单价        | UnitPrice              | decimal? |
| 明细项目费用总额  | FeeDetailTotalAmount   | decimal? |
| 定价上限金额    | PricingCapAmount       | decimal? |
| 自付比例      | SelfPayRatio           | decimal? |
| 先支付类型     | PrePaymentType         | string?  |
| 全自费金额     | FullSelfPayAmount      | decimal? |
| 超限价金额     | OverLimitAmount        | decimal? |
| 先行自付金额    | AdvanceSelfPayAmount   | decimal? |
| 符合范围金额    | InScopeAmount          | decimal? |
| 公务员床位费金额  | CivilServantBedAmount  | decimal? |
| 医院减免金额    | HospitalDiscountAmount | decimal? |
| 医院垫付金额    | HospitalAdvanceAmount  | decimal? |
| 收费项目等级    | ChargeItemLevel        | string?  |
| 医保目录编码    | InsuranceCatalogCode   | string?  |
| 医保目录名称    | InsuranceCatalogName   | string?  |
| 目录类别      | CatalogCategory        | string?  |
| 医疗目录编码    | MedicalCatalogCode     | string?  |
| 医药机构目录编码  | InstitutionCatalogCode | string?  |
| 医药机构目录名称  | InstitutionCatalogName | string?  |
| 医疗收费项目类别1 | MedicalChargeCategory1 | string?  |
| 医疗收费项目类别  | MedicalChargeCategory  | string?  |
| 规格        | Specification          | string?  |
| 剂型名称      | DosageFormName         | string?  |
| 开单科室编码    | OrderingDeptCode       | string?  |
| 开单科室名称    | OrderingDeptName       | string?  |
| 开单医师代码    | OrderingDoctorCode     | string?  |
| 开单医师姓名    | OrderingDoctorName     | string?  |
| 受单科室编码    | ReceivingDeptCode      | string?  |
| 受单科室名称    | ReceivingDeptName      | string?  |
| 受单医师代码    | ReceivingDoctorCode    | string?  |
| 受单医师姓名    | ReceivingDoctorName    | string?  |

## 实现步骤

1. **创建** **`Models/YuehaiSettlement.cs`** - 定义查询过滤器、结果模型、分页元数据
2. **创建** **`Contracts/IYuehaiSettlementDataRepository.cs`** - 定义仓储接口
3. **创建** **`Infrastructure/OracleYuehaiSettlementDataRepository.cs`** - 实现 Oracle 查询
4. **创建** **`Infrastructure/YuehaiSettlementTypeMap.cs`** - Dapper 中文列名映射
5. **创建** **`Tools/YuehaiSettlementTools.cs`** - MCP 工具类
6. **修改** **`Extensions/ServiceCollectionExtensions.cs`** - 添加服务注册扩展
7. **修改** **`Program.cs`** - 注册新工具和服务

## 验证

- 确保编译通过 (`dotnet build`)
- 确认所有 MCP 工具正确注册并可通过 MCP 协议调用
- 验证分页查询逻辑与现有 `AuditServerTools` 行为一致

