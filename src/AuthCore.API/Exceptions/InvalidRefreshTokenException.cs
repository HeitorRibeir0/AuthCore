namespace AuthCore.API.Exceptions;

public class InvalidRefreshTokenException()
    : Exception("Invalid or expired refresh token.");
