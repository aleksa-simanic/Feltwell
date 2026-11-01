namespace Feltwell.Api.Errors;

public abstract class AppException(string message) : Exception(message);

public sealed class NotFoundException(string message) : AppException(message);

public sealed class ValidationException(string message) : AppException(message);

public sealed class ConflictException(string message) : AppException(message);
