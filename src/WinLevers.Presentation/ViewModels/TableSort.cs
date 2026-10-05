namespace WinLevers.Presentation.ViewModels;

/// <summary>A column of the app grid that can be sorted on.</summary>
public enum SortColumn
{
    /// <summary>The app's name.</summary>
    Name,

    /// <summary>Who published it.</summary>
    Publisher,

    /// <summary>Packaged or desktop.</summary>
    Type,

    /// <summary>Its state under the active lens.</summary>
    State,

    /// <summary>When it last used the lens's capability.</summary>
    LastUsed,
}

/// <summary>Which column the grid is ordered by, and which way.</summary>
/// <param name="Column">The column.</param>
/// <param name="Descending">Whether the order is reversed.</param>
public sealed record TableSort(SortColumn Column, bool Descending)
{
    /// <summary>By name, A to Z: the order a list of apps is expected in.</summary>
    public static TableSort Default { get; } = new(SortColumn.Name, false);

    /// <summary>The sort after a click on a header.</summary>
    /// <remarks>
    /// The same header again flips the direction. A new one starts the way it
    /// is most often wanted: text A to Z, and dates newest first, because "what
    /// used the microphone recently" is the question that column answers.
    /// </remarks>
    public TableSort Toggle(SortColumn column) =>
        column == Column
            ? this with { Descending = !Descending }
            : new TableSort(column, column == SortColumn.LastUsed);

    /// <summary>The arrow on a header, or nothing when it is not the sorted one.</summary>
    public string Mark(SortColumn column) =>
        column != Column ? string.Empty : Descending ? " ▼" : " ▲";

    /// <summary>The header labels, with the arrow on the sorted one.</summary>
    /// <param name="stateHeader">The state column's name: the lens's, or STATE on All apps.</param>
    public TableHeaders Headers(string stateHeader) => new(
        "APPLICATION" + Mark(SortColumn.Name),
        "PUBLISHER" + Mark(SortColumn.Publisher),
        "TYPE" + Mark(SortColumn.Type),
        stateHeader + Mark(SortColumn.State),
        "LAST USED" + Mark(SortColumn.LastUsed));
}

/// <summary>The five column headers of the grid, as drawn.</summary>
/// <param name="Name">Over the app's name.</param>
/// <param name="Publisher">Over the publisher.</param>
/// <param name="Type">Over packaged or desktop.</param>
/// <param name="State">Over the state under the active lens.</param>
/// <param name="LastUsed">Over the last-used date.</param>
public sealed record TableHeaders(string Name, string Publisher, string Type, string State, string LastUsed);
