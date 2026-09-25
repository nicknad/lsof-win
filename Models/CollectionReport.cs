namespace Lsof.Models;

internal enum CollectionDiagnosticLevel
{
    Information,
    Warning
}

internal sealed record CollectionDiagnostic(CollectionDiagnosticLevel Level, string Message);

internal sealed class CollectionReport
{
    private readonly List<CollectionDiagnostic> _diagnostics = new();

    public IReadOnlyList<CollectionDiagnostic> Diagnostics => _diagnostics;

    public bool HasWarnings => _diagnostics.Any(diagnostic => diagnostic.Level == CollectionDiagnosticLevel.Warning);

    public void AddInformation(string message)
    {
        _diagnostics.Add(new CollectionDiagnostic(CollectionDiagnosticLevel.Information, message));
    }

    public void AddWarning(string message)
    {
        _diagnostics.Add(new CollectionDiagnostic(CollectionDiagnosticLevel.Warning, message));
    }
}
