using System;
using Xunit;

namespace Keemya.Frontend.Tests;

public class ErrorReportTests
{
    [Fact]
    public void FormatError_PutsInnerCauseFirst_AndKeepsFullDetails()
    {
        var error = new InvalidOperationException("outer wrapper", new NotImplementedException("real cause"));

        string report = App.FormatError(error);

        Assert.StartsWith("NotImplementedException: real cause", report);
        Assert.Contains("outer wrapper", report);
    }
}
