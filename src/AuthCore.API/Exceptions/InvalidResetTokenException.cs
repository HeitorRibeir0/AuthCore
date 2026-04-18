namespace AuthCore.API.Exceptions;

public class InvalidResetTokenException()
    : Exception("Invalid or expired password reset token.");
