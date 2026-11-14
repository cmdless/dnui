namespace Dnui;

/// <summary>A file-type filter: a display name and one or more patterns such as <c>*.png</c>.</summary>
/// <param name="Name">Name shown in the dialog's type list.</param>
/// <param name="Patterns">One or more glob patterns.</param>
public sealed record DnuiFileFilter(string Name, IReadOnlyList<string> Patterns);

/// <summary>Options for an open-file dialog.</summary>
public sealed record DnuiOpenFileOptions
{
    /// <summary>The dialog title.</summary>
    public string? Title { get; init; }

    /// <summary>Allow selecting more than one file.</summary>
    public bool AllowMultiple { get; init; }

    /// <summary>File-type filters, in display order.</summary>
    public IReadOnlyList<DnuiFileFilter> Filters { get; init; } = [];

    /// <summary>The initial folder. Ignored if it doesn't exist.</summary>
    public string? StartFolder { get; init; }
}

/// <summary>Options for an open-folder dialog.</summary>
public sealed record DnuiOpenFolderOptions
{
    /// <summary>The dialog title.</summary>
    public string? Title { get; init; }

    /// <summary>Allow selecting more than one folder.</summary>
    public bool AllowMultiple { get; init; }

    /// <summary>The initial folder. Ignored if it doesn't exist.</summary>
    public string? StartFolder { get; init; }
}

/// <summary>Options for a save-file dialog.</summary>
public sealed record DnuiSaveFileOptions
{
    /// <summary>The dialog title.</summary>
    public string? Title { get; init; }

    /// <summary>The prefilled file name.</summary>
    public string? SuggestedName { get; init; }

    /// <summary>Appended when the user types no extension (<c>png</c> or <c>.png</c>).</summary>
    public string? DefaultExtension { get; init; }

    /// <summary>File-type choices, in display order.</summary>
    public IReadOnlyList<DnuiFileFilter> Filters { get; init; } = [];

    /// <summary>The initial folder. Ignored if it doesn't exist.</summary>
    public string? StartFolder { get; init; }

    /// <summary>Ask before choosing an existing file. Default true.</summary>
    public bool OverwritePrompt { get; init; } = true;
}
