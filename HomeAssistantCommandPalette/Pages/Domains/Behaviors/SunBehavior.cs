namespace HomeAssistantCommandPalette.Pages.Domains.Behaviors;

/// <summary>
/// Entity-id override for <c>sun.sun</c>. Icon dispatch lives in
/// <see cref="IconPipeline.Rules.SunIconRule"/>; this class exists so
/// the registry has a behavior to bind for <c>sun.sun</c> primary /
/// context items (today: defaults).
/// </summary>
public sealed class SunBehavior : DomainBehavior
{
    public override string Domain => "sun";
}
