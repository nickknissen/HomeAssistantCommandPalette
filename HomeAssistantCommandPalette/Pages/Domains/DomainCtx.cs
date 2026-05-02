using System;
using HomeAssistantCommandPalette.Models;
using HomeAssistantCommandPalette.Services;

namespace HomeAssistantCommandPalette.Pages.Domains;

/// <summary>
/// Allocation-light bag of dependencies a <see cref="DomainBehavior"/>
/// needs to render a single entity. Threaded through every behavior
/// virtual by <c>in</c> reference so closures inside custom behaviors can
/// capture ctx fields without per-callsite locals.
/// </summary>
/// <remarks>
/// <see cref="OnSuccess"/> is page-owned: behaviors only ever forward it
/// into <see cref="Commands.CallServiceCommand"/>, never invoke it
/// directly. The page calls <c>RaiseItemsChanged</c> from there.
/// </remarks>
public readonly record struct DomainCtx(
    HaEntity Entity,
    IHaClient Client,
    HaSettings Settings,
    Action OnSuccess);
