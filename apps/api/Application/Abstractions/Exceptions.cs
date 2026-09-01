namespace VeriScan.Application.Abstractions;

public sealed class DataConcurrencyException : Exception
{
    public DataConcurrencyException()
        : base("数据已被其他请求修改。")
    {
    }
}

public abstract class ApplicationBaseException(string errorCode, string message) : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
}

public sealed class ResourceNotFoundException(string message)
    : ApplicationBaseException("resource_not_found", message);

public sealed class RequestConflictException(string message)
    : ApplicationBaseException("request_conflict", message);

public sealed class RequestValidationException(string message)
    : ApplicationBaseException("request_invalid", message);

public sealed class UnsupportedOperationException(string message)
    : ApplicationBaseException("operation_not_supported", message);
