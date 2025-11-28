using PokeSharp.Engine.Debug.Common;

namespace PokeSharp.Engine.Debug.Tests.Common;

public class ResultTests
{
    [Fact]
    public void Success_CreatesSuccessfulResult()
    {
        // Arrange
        var value = "test value";

        // Act
        var result = Result<string>.Success(value);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(value, result.Value);
        Assert.Null(result.Error);
        Assert.Null(result.Exception);
    }

    [Fact]
    public void Failure_WithErrorMessage_CreatesFailedResult()
    {
        // Arrange
        var errorMessage = "Something went wrong";

        // Act
        var result = Result<string>.Failure(errorMessage);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Null(result.Value);
        Assert.Equal(errorMessage, result.Error);
        Assert.Null(result.Exception);
    }

    [Fact]
    public void Failure_WithException_CreatesFailedResultWithException()
    {
        // Arrange
        var exception = new InvalidOperationException("Test error");

        // Act
        var result = Result<string>.Failure(exception);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Null(result.Value);
        Assert.Equal(exception.Message, result.Error);
        Assert.Equal(exception, result.Exception);
    }

    [Fact]
    public void Failure_WithErrorAndException_CreatesFailedResultWithBoth()
    {
        // Arrange
        var errorMessage = "Custom error";
        var exception = new InvalidOperationException("Test error");

        // Act
        var result = Result<string>.Failure(errorMessage, exception);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Null(result.Value);
        Assert.Equal(errorMessage, result.Error);
        Assert.Equal(exception, result.Exception);
    }

    [Fact]
    public void NonGenericResult_Success_CreatesSuccessfulResult()
    {
        // Act
        var result = Result.Success();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Null(result.Error);
        Assert.Null(result.Exception);
    }

    [Fact]
    public void NonGenericResult_Failure_CreatesFailedResult()
    {
        // Arrange
        var errorMessage = "Operation failed";

        // Act
        var result = Result.Failure(errorMessage);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal(errorMessage, result.Error);
    }

    [Fact]
    public void ResultCanBeUsedInPatternMatching()
    {
        // Arrange
        var successResult = Result<int>.Success(42);
        var failureResult = Result<int>.Failure("Error");

        // Act & Assert
        var successValue = successResult.IsSuccess ? successResult.Value : 0;
        Assert.Equal(42, successValue);

        var failureValue = failureResult.IsSuccess ? failureResult.Value : -1;
        Assert.Equal(-1, failureValue);
    }
}
