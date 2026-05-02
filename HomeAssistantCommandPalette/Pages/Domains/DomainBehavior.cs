using System.Collections.Generic;
using HomeAssistantCommandPalette.Commands;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages.Domains;

/// <summary>
/// Per-domain rendering contract: icon, details rows, primary command,
/// context menu items, and optional hero image. Each behavior owns the
/// full rendering for one HA domain so the page-level <c>CreateItem</c>
/// reduces to a registry lookup plus a fixed assembly step.
/// </summary>
/// <remarks>
/// New cross-cutting axes (footer line, secondary command, …) become new
/// virtuals on this base with safe defaults so non-overriding domains pay
/// nothing.
/// </remarks>
internal abstract class DomainBehavior
{
    /// <summary>The HA domain this behavior handles (e.g. <c>"light"</c>).</summary>
    public abstract string Domain { get; }

    /// <summary>
    /// The Enter action for an entity row. Defaults to opening the entity
    /// in the HA dashboard — the right behavior for read-only domains
    /// that don't expose a primary service.
    /// </summary>
    public virtual ICommand BuildPrimary(in DomainCtx ctx)
        => new OpenDashboardCommand(ctx.Settings, ctx.Entity.EntityId);

    /// <summary>
    /// Append per-domain detail rows after the standard <c>State</c> row
    /// and before the page-level common rows (Area / Last changed /
    /// Attribution / Entity ID).
    /// </summary>
    public virtual void AddDetailRows(in DomainCtx ctx, List<IDetailsElement> rows) { }

    /// <summary>
    /// Append per-domain context-menu items. The page always appends an
    /// "Open in dashboard" item after this returns.
    /// </summary>
    public virtual void AddContextItems(in DomainCtx ctx, List<IContextItem> items) { }

    /// <summary>
    /// State-tinted icon for the row. Default returns the generic shape
    /// glyph; concrete behaviors override with their domain icons.
    /// </summary>
    public virtual IconInfo BuildIcon(in DomainCtx ctx) => Icons.Shape;

    /// <summary>
    /// Hero image for the details pane (e.g. camera snapshot). Returns
    /// <c>null</c> for domains without one — the overwhelming majority.
    /// </summary>
    public virtual IconInfo? BuildHeroImage(in DomainCtx ctx) => null;
}
