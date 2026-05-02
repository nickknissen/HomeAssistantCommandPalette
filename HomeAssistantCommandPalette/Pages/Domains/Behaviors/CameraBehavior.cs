using System;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages.Domains.Behaviors;

public sealed class CameraBehavior : DomainBehavior
{
    public override string Domain => "camera";

    public override IconInfo BuildIcon(in DomainCtx ctx)
    {
        var unavailable = string.Equals(ctx.Entity.State, "unavailable", StringComparison.OrdinalIgnoreCase);
        return unavailable ? Icons.CameraUnavailable : Icons.Camera;
    }

    public override IconInfo? BuildHeroImage(in DomainCtx ctx)
    {
        // /api/camera_proxy/{entity_id} requires a Bearer header that
        // CmdPal can't add to a remote URL, so the client caches the
        // bytes to a temp file and we hand the file path to HeroImage.
        // Returns null on auth/timeout failure — the rest of the details
        // still render fine.
        var path = ctx.Client.GetCameraSnapshotPath(ctx.Entity.EntityId);
        return path is null ? null : new IconInfo(path);
    }
}
