using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages;

/// <summary>
/// <see cref="ListItem"/> whose details pane is built on first read
/// instead of when the list is assembled.
/// </summary>
/// <remarks>
/// Building an entity's details can block on Home Assistant: a numeric
/// sensor's "Trend (24h)" row fetches history, a camera's hero image
/// fetches a snapshot. CmdPal only reads <c>Details</c> for the item the
/// user selected (ListItemViewModel.SlowInitializeProperties), so paying
/// that cost per row turned one render of All Entities into hundreds of
/// sequential HTTP calls. Deferring the build keeps <c>GetItems</c> pure
/// CPU and leaves at most one detail fetch in flight — the selected row's.
/// </remarks>
internal sealed partial class LazyDetailsListItem : ListItem
{
    private readonly object _gate = new();
    private Func<IDetails?> _factory;
    private IDetails? _details;
    private bool _built;

    public LazyDetailsListItem(ICommand command, Func<IDetails?> factory)
        : base(command)
    {
        _factory = factory;
    }

    /// <summary>
    /// The deferred build. Exposed so a refresh can re-arm another item
    /// with it without forcing either item to materialize.
    /// </summary>
    internal Func<IDetails?> Factory
    {
        get { lock (_gate) { return _factory; } }
    }

    public override IDetails? Details
    {
        get
        {
            // The factory runs under the lock: two threads landing on the
            // same row should share one fetch rather than race to issue
            // two. Contention is a non-issue in practice — only the
            // selected row is ever read.
            lock (_gate)
            {
                if (!_built)
                {
                    _details = _factory();
                    _built = true;
                }
                return _details;
            }
        }

        set
        {
            lock (_gate)
            {
                _factory = () => value;
                _details = value;
                _built = true;
            }
            OnPropertyChanged(nameof(Details));
        }
    }

    /// <summary>
    /// Swaps in a new deferred build and drops the materialized pane.
    /// The property-changed notification makes CmdPal re-read
    /// <see cref="Details"/> for a row it is currently showing.
    /// </summary>
    internal void ResetDetails(Func<IDetails?> factory)
    {
        lock (_gate)
        {
            _factory = factory;
            _details = null;
            _built = false;
        }
        OnPropertyChanged(nameof(Details));
    }
}
