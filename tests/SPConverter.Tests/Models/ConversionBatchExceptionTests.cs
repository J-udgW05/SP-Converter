using FluentAssertions;
using SPConverter.Models;
using Xunit;

namespace SPConverter.Tests.Models;

public class ConversionBatchExceptionTests
{
    [Fact]
    public void Message_ShouldIncludeFirstFailureReasonForBatch()
    {
        var exception = new ConversionBatchException(new[]
        {
            new ConversionFailure("C:\\photos\\a.png", "Missing encoder"),
            new ConversionFailure("C:\\photos\\b.png", "Missing encoder")
        });

        exception.Message.Should().Contain("2 files");
        exception.Message.Should().Contain("Missing encoder");
    }
}
