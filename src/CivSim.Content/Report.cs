using System.Collections.Generic;

namespace CivSim.Content;

public enum Severity { Error, Warning }

/// <summary>One finding from loading or validating content. <see cref="Code"/> is the check id (V1 to V18).</summary>
public sealed record Issue(Severity Severity, string Code, string Location, string Message)
{
    public override string ToString() => $"{(Severity == Severity.Error ? "error" : "warning")} {Code} {Location}: {Message}";
}

public sealed class Report
{
    private readonly List<Issue> _issues = new();

    public IReadOnlyList<Issue> Issues => _issues;
    public int ErrorCount { get; private set; }
    public int WarningCount { get; private set; }
    public bool HasErrors => ErrorCount > 0;

    public void Error(string code, string location, string message)
    {
        _issues.Add(new Issue(Severity.Error, code, location, message));
        ErrorCount++;
    }

    public void Warning(string code, string location, string message)
    {
        _issues.Add(new Issue(Severity.Warning, code, location, message));
        WarningCount++;
    }

    public int Count(string code, Severity severity)
    {
        int n = 0;
        foreach (Issue i in _issues) if (i.Code == code && i.Severity == severity) n++;
        return n;
    }
}
