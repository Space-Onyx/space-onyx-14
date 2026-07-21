// SPDX-FileCopyrightText: 2025 Aidenkrz <aiden@djkraz.com>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Rouden <149893554+Roudenn@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Roudenn <romabond091@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Client.UserInterface.Systems;
using Content.Shared._Onyx.Fishing.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Utility;

namespace Content.Client._Onyx.Fishing.Overlays;

public sealed class FishingOverlay : Overlay
{
    private const float StartYFraction = 0.09375f;
    private const float EndYFraction = 0.90625f;
    private const float BarWidthFraction = 0.2f;

    private readonly IEntityManager _entityManager;
    private readonly IPlayerManager _player;
    private readonly SharedTransformSystem _transform;
    private readonly ProgressColorSystem _progressColor;
    private readonly SpriteSystem _sprite;
    private readonly Texture _barTexture;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public FishingOverlay(IEntityManager entityManager, IPlayerManager player)
    {
        _entityManager = entityManager;
        _player = player;
        _transform = entityManager.System<SharedTransformSystem>();
        _progressColor = entityManager.System<ProgressColorSystem>();
        _sprite = entityManager.System<SpriteSystem>();
        var sprite = new SpriteSpecifier.Rsi(new("/Textures/_Onyx/Interface/Misc/fish_bar.rsi"), "icon");
        _barTexture = _sprite.Frame0(sprite);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var localEntity = _player.LocalSession?.AttachedEntity;
        if (localEntity == null ||
            !_entityManager.TryGetComponent(localEntity, out ActiveFisherComponent? comp) ||
            !_entityManager.TryGetComponent(localEntity, out SpriteComponent? sprite) ||
            !_entityManager.TryGetComponent(localEntity, out TransformComponent? xform) ||
            xform.MapID != args.MapId ||
            comp.TotalProgress is null or < 0)
            return;

        var worldPosition = _transform.GetWorldPosition(xform);
        if (!args.WorldAABB.Enlarged(5f).Contains(worldPosition))
            return;

        var handle = args.WorldHandle;
        var rotation = args.Viewport.Eye?.Rotation ?? Angle.Zero;
        var textureSize = new Vector2(_barTexture.Width, _barTexture.Height) / EyeManager.PixelsPerMeter;
        var barWidth = textureSize.X * BarWidthFraction;
        var rotationMatrix = Matrix3Helpers.CreateRotation(-rotation);
        handle.SetTransform(Matrix3x2.Multiply(rotationMatrix, Matrix3Helpers.CreateTranslation(worldPosition)));
        var position = new Vector2(_sprite.GetLocalBounds((localEntity.Value, sprite)).Width / 2f, -textureSize.Y / 2f);
        handle.DrawTextureRect(_barTexture, new Box2(position, position + textureSize));

        var progress = Math.Clamp(comp.TotalProgress.Value, 0f, 1f);
        var startY = textureSize.Y * StartYFraction;
        var progressY = (textureSize.Y * EndYFraction - startY) * progress + startY;
        var box = new Box2(
            new Vector2((textureSize.X - barWidth) / 2f, startY),
            new Vector2((textureSize.X + barWidth) / 2f, progressY)).Translated(position);
        handle.DrawRect(box, _progressColor.GetProgressColor(progress));

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }
}
