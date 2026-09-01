namespace VeriScan.Domain.Entities;

/// <summary>规则匹配后的处理动作。</summary>
public enum RuleAction
{
    /// <summary>可信命中，直接拒绝内容。</summary>
    HardReject,

    /// <summary>产生风险信号，交由后续审核链路判定。</summary>
    RiskSignal,

    /// <summary>抑制同分类的风险信号。</summary>
    ContextException,

    /// <summary>直接返回人工复审状态，不再请求外部 AI。</summary>
    ForceReview,

    /// <summary>仅记录命中，不改变最终判定。</summary>
    MonitorOnly
}

/// <summary>词条的规范化匹配方式。</summary>
public enum RuleMatchMode
{
    /// <summary>在规范化文本中按连续片段匹配。</summary>
    NormalizedContains
}

/// <summary>正则表达式执行引擎模式。</summary>
public enum RegexRuleEngineMode
{
    /// <summary>优先使用 .NET 非回溯引擎。</summary>
    NonBacktracking,

    /// <summary>仅在静态安全检查通过后使用带超时的回溯引擎。</summary>
    Backtracking
}

/// <summary>规则集使用的文本规范化边界。</summary>
public enum RuleNormalizationProfile
{
    /// <summary>执行 Unicode NFKC、大小写、空白和零宽字符规范化。</summary>
    Default,

    /// <summary>在基础规范化上启用受控的繁简字符映射。</summary>
    TraditionalSimplified
}
