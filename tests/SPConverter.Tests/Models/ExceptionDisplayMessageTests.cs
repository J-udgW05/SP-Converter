using System;
using FluentAssertions;
using SPConverter.Models;
using Xunit;

namespace SPConverter.Tests.Models;

public class ExceptionDisplayMessageTests
{
    [Fact]
    public void From_ShouldReturnDeepestInnerExceptionMessage()
    {
        var exception = new TypeInitializationException(
            "NativeMagickSettings",
            new InvalidOperationException("Magick native library is missing"));

        string message = ExceptionDisplayMessage.From(exception);

        message.Should().Be("Magick native library is missing");
    }
}
