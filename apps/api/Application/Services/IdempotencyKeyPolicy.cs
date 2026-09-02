using VeriScan.Application.Abstractions;

namespace VeriScan.Application.Services;

/// <summary>统一校验写操作使用的 Idempotency-Key。</summary>
internal static class IdempotencyKeyPolicy
{
    private const int MinimumKeyLength = 16;
    private const int MaximumKeyLength = 128;

    public static string? ValidateOptional(string? idempotencyKey)
    {
        return idempotencyKey is null ? null : Validate(idempotencyKey);
    }

    public static string ValidateRequired(string? idempotencyKey)
    {
        if (idempotencyKey is null)
        {
            throw new RequestValidationException("取消审核批次必须提供且只能提供一个 Idempotency-Key。");
        }

        return Validate(idempotencyKey);
    }

    private static string Validate(string idempotencyKey)
    {
        if (idempotencyKey.Length is < MinimumKeyLength or > MaximumKeyLength ||
            idempotencyKey.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not '.' and not '_' and not ':' and not '-'))
        {
            throw new RequestValidationException(
                "Idempotency-Key 必须为 16 到 128 位，仅可包含 ASCII 字母、数字、点、下划线、冒号或连字符。");
        }

        return idempotencyKey;
    }
}
