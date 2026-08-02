using System.Numerics;
using Content.Client.Hands.Systems;
// <Onyx-MartialArts>
using Content.Shared._Onyx.MartialArts;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Utility;
// </Onyx-MartialArts>
using Content.Shared.CCVar;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
// <Onyx-MartialArts>
using Robust.Shared.Maths;
// </Onyx-MartialArts>
using Direction = Robust.Shared.Maths.Direction;

namespace Content.Client.Hands
{
    public sealed partial class ShowHandItemOverlay : Overlay
    {
        [Dependency] private IConfigurationManager _cfg = default!;
        [Dependency] private IInputManager _inputManager = default!;
        [Dependency] private IClyde _clyde = default!;
        [Dependency] private IEntityManager _entMan = default!;
        // <Onyx-MartialArts>
        [Dependency] private IPlayerManager _player = default!;
        [Dependency] private IResourceCache _resourceCache = default!;
        // </Onyx-MartialArts>

        // <Onyx-MartialArts>
        private static readonly ResPath ComboAttackRsi = new("/Textures/_Onyx/Interface/Misc/intents.rsi");
        private readonly RSI _comboRsi;
        // </Onyx-MartialArts>

        private HandsSystem? _hands;
        private readonly IRenderTexture _renderBackbuffer;

        public override OverlaySpace Space => OverlaySpace.ScreenSpace;

        public Texture? IconOverride;
        public EntityUid? EntityOverride;

        public ShowHandItemOverlay()
        {
            IoCManager.InjectDependencies(this);
            _comboRsi = _resourceCache.GetResource<RSIResource>(ComboAttackRsi).RSI;

            _renderBackbuffer = _clyde.CreateRenderTarget(
                (64, 64),
                new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb, true),
                new TextureSampleParameters
                {
                    Filter = true
                }, nameof(ShowHandItemOverlay));
        }

        protected override void DisposeBehavior()
        {
            base.DisposeBehavior();

            _renderBackbuffer.Dispose();
        }

        protected override bool BeforeDraw(in OverlayDrawArgs args)
        {
            if (!_cfg.GetCVar(CCVars.HudHeldItemShow))
                return false;

            return base.BeforeDraw(in args);
        }

        protected override void Draw(in OverlayDrawArgs args)
        {
            var mousePos = _inputManager.MouseScreenPosition;

            // Offscreen
            if (mousePos.Window == WindowId.Invalid)
                return;

            var screen = args.ScreenHandle;
            var offset = _cfg.GetCVar(CCVars.HudHeldItemOffset);
            var offsetVec = new Vector2(offset, offset);

            if (IconOverride != null)
            {
                screen.DrawTexture(IconOverride, mousePos.Position - IconOverride.Size / 2 + offsetVec, Color.White.WithAlpha(0.75f));
                return;
            }

            _hands ??= _entMan.System<HandsSystem>();
            var handEntity = _hands.GetActiveHandEntity();

            // <Onyx-MartialArts>
            if (_player.LocalEntity is { } player)
            {
                var comboEvent = new GetPerformedAttackTypesEvent();
                _entMan.EventBus.RaiseLocalEvent(player, ref comboEvent);
                if (comboEvent.AttackTypes is { Count: > 0 } attacks)
                {
                    for (var i = 0; i < attacks.Count; i++)
                    {
                        if (!_comboRsi.TryGetState(attacks[i].ToString().ToLowerInvariant(), out var state))
                            continue;
                        var texture = state.Frame0;
                        var comboOffset = new Vector2(-offsetVec.X, (2f * i + 1f - attacks.Count) * texture.Size.Y / 1.8f);
                        screen.DrawTextureRect(texture,
                            UIBox2.FromDimensions(mousePos.Position - texture.Size / 2 + comboOffset, texture.Size),
                            Color.White.WithAlpha(0.75f));
                    }
                }
            }
            // </Onyx-MartialArts>

            if (handEntity == null || !_entMan.TryGetComponent(handEntity, out SpriteComponent? sprite))
                return;

            var halfSize = _renderBackbuffer.Size / 2;
            var uiScale = (args.ViewportControl as Control)?.UIScale ?? 1f;

            screen.RenderInRenderTarget(_renderBackbuffer, () =>
            {
                screen.DrawEntity(handEntity.Value, halfSize, new Vector2(1f, 1f) * uiScale, Angle.Zero, Angle.Zero, Direction.South, sprite);
            }, Color.Transparent);

            screen.DrawTexture(_renderBackbuffer.Texture, mousePos.Position - halfSize + offsetVec, Color.White.WithAlpha(0.75f));
        }
    }
}
