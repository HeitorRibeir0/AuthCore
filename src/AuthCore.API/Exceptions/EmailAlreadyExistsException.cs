namespace AuthCore.API.Exceptions;

public class EmailAlreadyExistsException()
    : Exception("Email already in use.");
