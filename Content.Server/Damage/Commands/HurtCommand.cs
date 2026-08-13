using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Administration;
using Content.Shared._Onyx.Targeting; // <Onyx-PartDamageCommand>
using Content.Shared._Onyx.Wounds; // <Onyx-PartDamageCommand>
using Content.Shared.Administration;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server.Damage.Commands
{
    [AdminCommand(AdminFlags.Fun)]
    sealed partial class DamageCommand : IConsoleCommand
    {
        [Dependency] private IEntityManager _entManager = default!;
        [Dependency] private IPrototypeManager _prototypeManager = default!;

        private TargetResolverSystem TargetResolver => _entManager.System<TargetResolverSystem>(); // <Onyx-PartDamageCommand>

        public string Command => "damage";
        public string Description => Loc.GetString("damage-command-description");
        public string Help => Loc.GetString("damage-command-help", ("command", Command));

        public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
        {
            if (args.Length == 1)
            {
                var types = _prototypeManager.EnumeratePrototypes<DamageTypePrototype>()
                    .Select(p => new CompletionOption(p.ID));

                var groups = _prototypeManager.EnumeratePrototypes<DamageGroupPrototype>()
                    .Select(p => new CompletionOption(p.ID));

                return CompletionResult.FromHintOptions(types.Concat(groups).OrderBy(p => p.Value),
                    Loc.GetString("damage-command-arg-type"));
            }

            if (args.Length == 2)
            {
                return CompletionResult.FromHint(Loc.GetString("damage-command-arg-quantity"));
            }

            if (args.Length == 3)
            {
                // if type.Name is good enough for cvars, <bool> doesn't need localizing.
                return CompletionResult.FromHint("<bool>");
            }

            if (args.Length == 4)
            {
                return CompletionResult.FromHint(Loc.GetString("damage-command-arg-target"));
            }

            // <Onyx-PartDamageCommand>
            if (args.Length == 5)
            {
                var options = SharedTargetingSystem.SelectableParts.AsEnumerable();
                if (_entManager.TryParseNetEntity(args[3], out var target) && _entManager.EntityExists(target))
                    options = options.Where(part => TargetResolver.TryResolveExact(target.Value, part, out _));

                return CompletionResult.FromHintOptions(
                    options.Select(part => new CompletionOption(part.ToString())),
                    Loc.GetString("damage-command-arg-body-part"));
            }
            // </Onyx-PartDamageCommand>

            return CompletionResult.Empty;
        }

        private delegate void Damage(EntityUid entity, bool ignoreResistances);

        private bool TryParseDamageArgs(
            IConsoleShell shell,
            EntityUid target,
            string[] args,
            [NotNullWhen(true)] out Damage? func)
        {
            if (!float.TryParse(args[1], out var amount))
            {
                shell.WriteLine(Loc.GetString("damage-command-error-quantity", ("arg", args[1])));
                func = null;
                return false;
            }

            if (_prototypeManager.TryIndex<DamageGroupPrototype>(args[0], out var damageGroup))
            {
                func = (entity, ignoreResistances) =>
                {
                    var damage = new DamageSpecifier(damageGroup, amount);
                    _entManager.System<DamageableSystem>().TryChangeDamage(entity, damage, ignoreResistances);
                };

                return true;
            }
            // Fall back to DamageType

            if (_prototypeManager.TryIndex<DamageTypePrototype>(args[0], out var damageType))
            {
                func = (entity, ignoreResistances) =>
                {
                    var damage = new DamageSpecifier(damageType, amount);
                    _entManager.System<DamageableSystem>().TryChangeDamage(entity, damage, ignoreResistances);
                };
                return true;

            }

            shell.WriteLine(Loc.GetString("damage-command-error-type", ("arg", args[0])));
            func = null;
            return false;
        }

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            if (args.Length < 2 || args.Length > 5) // <Onyx-PartDamageCommand-edited>
            {
                shell.WriteLine(Loc.GetString("damage-command-error-args"));
                return;
            }

            EntityUid? target;

            if (args.Length >= 4) // <Onyx-PartDamageCommand-edited>
            {
                if (!_entManager.TryParseNetEntity(args[3], out target) || !_entManager.EntityExists(target))
                {
                    shell.WriteLine(Loc.GetString("damage-command-error-euid", ("arg", args[3])));
                    return;
                }
            }
            else if (shell.Player?.AttachedEntity is { Valid: true } playerEntity)
            {
                target = playerEntity;
            }
            else
            {
                shell.WriteLine(Loc.GetString("damage-command-error-player"));
                return;
            }

            bool ignoreResistances;
            if (args.Length >= 3) // <Onyx-PartDamageCommand-edited>
            {
                if (!bool.TryParse(args[2], out ignoreResistances))
                {
                    shell.WriteLine(Loc.GetString("damage-command-error-bool", ("arg", args[2])));
                    return;
                }
            }
            else
            {
                ignoreResistances = false;
            }

            // <Onyx-PartDamageCommand-edited>
            if (args.Length == 5)
            {
                if (!Enum.TryParse<TargetBodyPart>(args[4], ignoreCase: true, out var requestedPart) ||
                    !SharedTargetingSystem.IsSelectable(requestedPart))
                {
                    shell.WriteLine(Loc.GetString("damage-command-error-body-part", ("arg", args[4])));
                    return;
                }

                if (!TargetResolver.TryResolveExact(target.Value, requestedPart, out var part))
                {
                    shell.WriteLine(Loc.GetString("damage-command-error-missing-body-part",
                        ("target", target.Value),
                        ("part", requestedPart)));
                    return;
                }

                if (!TryParseDamageSpecifier(args[0], args[1], shell, out var damage))
                    return;

                if (!_entManager.System<WoundDamageRoutingSystem>()
                        .TryApplyPartDamage(target.Value, part, damage, ignoreResistances: ignoreResistances))
                    shell.WriteLine(Loc.GetString("damage-command-error-part-damage", ("target", target.Value)));
                return;
            }

            if (!TryParseDamageArgs(shell, target.Value, args, out var damageFunc))
                return;

            damageFunc(target.Value, ignoreResistances);
            // </Onyx-PartDamageCommand-edited>
        }

        // <Onyx-PartDamageCommand>
        private bool TryParseDamageSpecifier(
            string type,
            string quantity,
            IConsoleShell shell,
            [NotNullWhen(true)] out DamageSpecifier? damage)
        {
            if (!float.TryParse(quantity, out var amount))
            {
                shell.WriteLine(Loc.GetString("damage-command-error-quantity", ("arg", quantity)));
                damage = null;
                return false;
            }

            if (_prototypeManager.TryIndex<DamageGroupPrototype>(type, out var damageGroup))
            {
                damage = new DamageSpecifier(damageGroup, amount);
                return true;
            }

            if (_prototypeManager.TryIndex<DamageTypePrototype>(type, out var damageType))
            {
                damage = new DamageSpecifier(damageType, amount);
                return true;
            }

            shell.WriteLine(Loc.GetString("damage-command-error-type", ("arg", type)));
            damage = null;
            return false;
        }
        // </Onyx-PartDamageCommand>
    }
}
