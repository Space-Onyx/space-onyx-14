using Content.Shared.Gravity;
using Content.Shared._Onyx.ZLevels.Core.Components; // <Onyx-ZLevels>
using JetBrains.Annotations;
using Robust.Shared.Map.Components;

namespace Content.Server.Gravity
{
    [UsedImplicitly]
    public sealed class GravitySystem : SharedGravitySystem
    {
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<GravityComponent, ComponentInit>(OnGravityInit);
        }

        /// <summary>
        /// Iterates gravity components and checks if this entity can have gravity applied.
        /// </summary>
        public void RefreshGravity(EntityUid uid, GravityComponent? gravity = null)
        {
            if (!GravityQuery.Resolve(uid, ref gravity))
                return;

            if (gravity.Inherent && !TryComp<CEZLinkedGridComponent>(uid, out _)) // <Onyx-ZLevels-edited>
                return;

            // <Onyx-ZLevels-edited>
            var targets = GetGravityTargets(uid);
            var enabled = LinkedTargetsHaveInherentGravity(targets) || LinkedTargetsHaveActiveGravityGenerator(targets);
            foreach (var targetUid in targets)
            {
                if (!TryComp<GravityComponent>(targetUid, out var targetGravity) ||
                    targetGravity.Inherent ||
                    targetGravity.Enabled == enabled)
                    continue;

                targetGravity.Enabled = enabled;
                var ev = new GravityChangedEvent(targetUid, enabled);
                RaiseLocalEvent(targetUid, ref ev, true);
                Dirty(targetUid, targetGravity);

                if (enabled && HasComp<MapGridComponent>(targetUid))
                    StartGridShake(targetUid);
            }
            // </Onyx-ZLevels-edited>
        }

        private void OnGravityInit(EntityUid uid, GravityComponent component, ComponentInit args)
        {
            RefreshGravity(uid);
        }

        /// <summary>
        /// Enables gravity. Note that this is a fast-path for GravityGeneratorSystem.
        /// This means it does nothing if Inherent is set and it might be wiped away with a refresh
        ///  if you're not supposed to be doing whatever you're doing.
        /// </summary>
        public void EnableGravity(EntityUid uid, GravityComponent? gravity = null)
        {
            if (!GravityQuery.Resolve(uid, ref gravity))
                return;

            // <Onyx-ZLevels>
            if (TryComp<CEZLinkedGridComponent>(uid, out _))
            {
                RefreshGravity(uid, gravity);
                return;
            }
            // </Onyx-ZLevels>

            if (gravity.Enabled || gravity.Inherent)
                return;

            gravity.Enabled = true;
            var ev = new GravityChangedEvent(uid, true);
            RaiseLocalEvent(uid, ref ev, true);
            Dirty(uid, gravity);

            if (HasComp<MapGridComponent>(uid))
            {
                StartGridShake(uid);
            }
        }

        // <Onyx-ZLevels>
        private List<EntityUid> GetGravityTargets(EntityUid uid)
        {
            var targets = new List<EntityUid> { uid };
            if (!TryComp<CEZLinkedGridComponent>(uid, out var linked))
                return targets;

            foreach (var peerUid in linked.PeerGrids.Values)
            {
                if (!targets.Contains(peerUid))
                    targets.Add(peerUid);
            }
            return targets;
        }

        private bool LinkedTargetsHaveInherentGravity(List<EntityUid> targets)
        {
            foreach (var targetUid in targets)
            {
                if (TryComp<GravityComponent>(targetUid, out var gravity) && gravity.Inherent)
                    return true;
            }
            return false;
        }

        private bool LinkedTargetsHaveActiveGravityGenerator(List<EntityUid> targets)
        {
            var query = EntityQueryEnumerator<GravityGeneratorComponent, TransformComponent>();
            while (query.MoveNext(out _, out var gravity, out var xform))
            {
                if (gravity.GravityActive && targets.Contains(xform.ParentUid))
                    return true;
            }
            return false;
        }
        // </Onyx-ZLevels>
    }
}
