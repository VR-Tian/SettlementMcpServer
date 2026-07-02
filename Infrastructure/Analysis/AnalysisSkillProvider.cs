using SettlementMcpServer.Contracts;
using SettlementMcpServer.Models;

namespace SettlementMcpServer.Infrastructure.Analysis;

/// <summary>
/// 分析维度提供者实现
/// </summary>
/// <remarks>
/// <para>
/// 维护预定义的分析维度列表，包含医院、科室、险种、时间、诊断等维度的 SQL 查询模板。
/// SQL 使用 DuckDB 语法，查询 Parquet 文件视图。
/// </para>
/// </remarks>
public sealed class AnalysisSkillProvider : IAnalysisSkillProvider
{
    private readonly IReadOnlyList<AnalysisDimension> _dimensions;

    /// <summary>
    /// 初始化分析维度提供者
    /// </summary>
    public AnalysisSkillProvider()
    {
        _dimensions = new List<AnalysisDimension>
        {
            // 医保结算数据据分析维度
            new()
            {
                Name = "hospital_settlement_summary",
                Description = "按医院维度统计结算金额和就诊人次",
                DataType = "YuehaiSettlement",
                SqlTemplate = """
                    SELECT 
                        InstitutionCode as 医院编码,
                        InstitutionName as 医院名称,
                        COUNT(*) as 就诊人次,
                        SUM(FeeDetailTotalAmount) as 费用总额,
                        AVG(FeeDetailTotalAmount) as 平均费用
                    FROM yuehai_settlements
                    WHERE InstitutionCode IS NOT NULL
                    GROUP BY InstitutionCode, InstitutionName
                    ORDER BY 费用总额 DESC
                    LIMIT 20
                    """
            },
            new()
            {
                Name = "department_visit_statistics",
                Description = "按科室维度统计就诊人次和住院天数",
                DataType = "YuehaiSettlement",
                SqlTemplate = """
                    SELECT 
                        AdmissionDeptName as 入院科室,
                        COUNT(*) as 就诊人次,
                        SUM(HospitalDays) as 总住院天数,
                        AVG(HospitalDays) as 平均住院天数
                    FROM yuehai_settlements
                    WHERE AdmissionDeptName IS NOT NULL
                    GROUP BY AdmissionDeptName
                    ORDER BY 就诊人次 DESC
                    LIMIT 20
                    """
            },
            new()
            {
                Name = "insurance_type_distribution",
                Description = "按险种类型统计费用分布",
                DataType = "YuehaiSettlement",
                SqlTemplate = """
                    SELECT 
                        InsuranceType1 as 险种类型,
                        COUNT(*) as 就诊人次,
                        SUM(FeeDetailTotalAmount) as 费用总额,
                        SUM(FullSelfPayAmount) as 全自费金额,
                        SUM(AdvanceSelfPayAmount) as 先行自付金额,
                        SUM(InScopeAmount) as 符合范围金额
                    FROM yuehai_settlements
                    WHERE InsuranceType1 IS NOT NULL
                    GROUP BY InsuranceType1
                    ORDER BY 费用总额 DESC
                    """
            },
            new()
            {
                Name = "settlement_time_trend",
                Description = "按时间维度统计结算趋势（按月统计）",
                DataType = "YuehaiSettlement",
                SqlTemplate = """
                    SELECT 
                        strftime('%Y-%m', SettlementTime) as 结算月份,
                        COUNT(*) as 就诊人次,
                        SUM(FeeDetailTotalAmount) as 费用总额,
                        AVG(FeeDetailTotalAmount) as 平均费用
                    FROM yuehai_settlements
                    WHERE SettlementTime IS NOT NULL
                    GROUP BY strftime('%Y-%m', SettlementTime)
                    ORDER BY 结算月份
                    """
            },
            new()
            {
                Name = "diagnosis_frequency_analysis",
                Description = "按诊断维度统计高频疾病",
                DataType = "YuehaiSettlement",
                SqlTemplate = """
                    SELECT 
                        PrimaryDiagnosisName as 主诊断名称,
                        COUNT(*) as 就诊人次,
                        SUM(FeeDetailTotalAmount) as 费用总额,
                        AVG(HospitalDays) as 平均住院天数
                    FROM yuehai_settlements
                    WHERE PrimaryDiagnosisName IS NOT NULL
                    GROUP BY PrimaryDiagnosisName
                    ORDER BY 就诊人次 DESC
                    LIMIT 30
                    """
            },
            new()
            {
                Name = "medical_category_analysis",
                Description = "按医疗类别统计费用分布",
                DataType = "YuehaiSettlement",
                SqlTemplate = """
                    SELECT 
                        MedicalCategory as 医疗类别,
                        COUNT(*) as 就诊人次,
                        SUM(FeeDetailTotalAmount) as 费用总额,
                        AVG(FeeDetailTotalAmount) as 平均费用
                    FROM yuehai_settlements
                    WHERE MedicalCategory IS NOT NULL
                    GROUP BY MedicalCategory
                    ORDER BY 费用总额 DESC
                    """
            },

            // 审核数据分析维度
            new()
            {
                Name = "hospital_audit_summary",
                Description = "按医院维度统计审核违规数量和涉及金额",
                DataType = "AuditedResult",
                SqlTemplate = """
                    SELECT 
                        HospitalCode as 医院编码,
                        HospitalName as 医院名称,
                        COUNT(*) as 违规记录数,
                        SUM(InvolvedDetailAmount) as 涉及金额,
                        COUNT(DISTINCT RuleCode) as 涉及规则数
                    FROM audited_results
                    WHERE HospitalCode IS NOT NULL
                    GROUP BY HospitalCode, HospitalName
                    ORDER BY 涉及金额 DESC
                    LIMIT 20
                    """
            },
            new()
            {
                Name = "rule_violation_analysis",
                Description = "按规则维度统计违规类型分布",
                DataType = "AuditedResult",
                SqlTemplate = """
                    SELECT 
                        RuleCode as 规则编码,
                        RuleName as 规则名称,
                        COUNT(*) as 违规次数,
                        SUM(InvolvedDetailAmount) as 涉及金额
                    FROM audited_results
                    WHERE RuleCode IS NOT NULL
                    GROUP BY RuleCode, RuleName
                    ORDER BY 违规次数 DESC
                    LIMIT 20
                    """
            },
            new()
            {
                Name = "department_audit_statistics",
                Description = "按科室维度统计审核情况",
                DataType = "AuditedResult",
                SqlTemplate = """
                    SELECT 
                        Department as 科室,
                        COUNT(*) as 违规记录数,
                        SUM(InvolvedDetailAmount) as 涉及金额,
                        COUNT(DISTINCT MedicalRecordNo) as 涉及病案数
                    FROM audited_results
                    WHERE Department IS NOT NULL
                    GROUP BY Department
                    ORDER BY 涉及金额 DESC
                    LIMIT 20
                    """
            },
            new()
            {
                Name = "insurance_type_audit_analysis",
                Description = "按参保类型统计审核情况",
                DataType = "AuditedResult",
                SqlTemplate = """
                    SELECT 
                        InsuranceType as 参保类型,
                        COUNT(*) as 违规记录数,
                        SUM(InvolvedDetailAmount) as 涉及金额,
                        AVG(InvolvedDetailAmount) as 平均涉及金额
                    FROM audited_results
                    WHERE InsuranceType IS NOT NULL
                    GROUP BY InsuranceType
                    ORDER BY 涉及金额 DESC
                    """
            }
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<AnalysisDimension> GetAllDimensions()
    {
        return _dimensions;
    }

    /// <inheritdoc />
    public AnalysisDimension? GetDimensionByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return _dimensions.FirstOrDefault(d => 
            string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
