namespace EcoActivist.Application.Common.Exceptions;

public sealed class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }

    public static NotFoundException For<TEntity>(Guid id) =>
        new($"{typeof(TEntity).Name} с идентификатором '{id}' не найден.");
}

public sealed class ConflictException : Exception
{
    public ConflictException(string message) : base(message)
    {
    }

    public ConflictException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public sealed class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException(string message) : base(message)
    {
    }
}

public sealed class AuthenticationFailedException : Exception
{
    public AuthenticationFailedException(string message) : base(message)
    {
    }
}

public sealed class RequestValidationException : Exception
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public RequestValidationException(string field, string error)
        : this(new Dictionary<string, string[]> { [field] = [error] })
    {
    }

    public RequestValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("Один или несколько параметров запроса некорректны.")
    {
        Errors = errors;
    }
}
