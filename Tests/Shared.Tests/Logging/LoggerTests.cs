using FluentAssertions;
using Shared.Logging;

namespace Shared.Tests.Logging;

public class LoggerTests : IDisposable
{
    private readonly TextWriter _originalOut;
    private readonly TextWriter _originalError;
    private readonly Logger.LogLevel _originalMinimumLevel;

    public LoggerTests()
    {
        _originalOut = Console.Out;
        _originalError = Console.Error;
        _originalMinimumLevel = Logger.MinimumLevel;
    }

    public void Dispose()
    {
        Console.SetOut(_originalOut);
        Console.SetError(_originalError);
        Logger.MinimumLevel = _originalMinimumLevel;
    }

    [Fact]
    public void Log_WhenLevelIsBelowMinimumLevel_DoesNotWrite()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        Console.SetOut(output);
        Console.SetError(error);
        Logger.MinimumLevel = Logger.LogLevel.Warning;

        Logger.Info("ignored");

        output.ToString().Should().BeEmpty();
        error.ToString().Should().BeEmpty();
    }

    [Fact]
    public void Info_WritesFormattedLineToStandardOutput()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        Console.SetOut(output);
        Console.SetError(error);
        Logger.MinimumLevel = Logger.LogLevel.Debug;

        Logger.Info("hello");

        output.ToString().Should().Contain("[Info] hello");
        error.ToString().Should().BeEmpty();
    }

    [Fact]
    public void Error_WritesFormattedLineToStandardError()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        Console.SetOut(output);
        Console.SetError(error);
        Logger.MinimumLevel = Logger.LogLevel.Debug;

        Logger.Error(new InvalidOperationException("boom"), "failed");

        output.ToString().Should().BeEmpty();
        error.ToString().Should().Contain("[Error] failed Exception: InvalidOperationException - boom");
    }
}
