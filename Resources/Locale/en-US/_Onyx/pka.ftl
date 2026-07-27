ent-WeaponProtoKineticShotgun = proto-kinetic shotgun
    .desc = Fires a spread of low-damage kinetic bolts that are half as effective for mining.
ent-WeaponProtoKineticRepeater = proto-kinetic repeater
    .desc = Fires a barrage of medium-damage kinetic bolts at a short range.
ent-WeaponProtoKineticPistol = proto-kinetic pistol
    .desc = Fires low-damage kinetic bolts, has a higher mod capacity.
ent-WeaponProtoKineticAcceleratorSpace = { ent-WeaponProtoKineticAccelerator }
    .suffix = Space upgrade
    .desc = { ent-WeaponProtoKineticAccelerator.desc }
ent-WeaponProtoKineticShotgunSpace = { ent-WeaponProtoKineticShotgun }
    .suffix = Space upgrade
    .desc = { ent-WeaponProtoKineticShotgun.desc }
ent-WeaponProtoKineticRepeaterSpace = { ent-WeaponProtoKineticRepeater }
    .suffix = Space upgrade
    .desc = { ent-WeaponProtoKineticRepeater.desc }
ent-WeaponProtoKineticPistolSpace = { ent-WeaponProtoKineticPistol }
    .suffix = Space upgrade
    .desc = { ent-WeaponProtoKineticPistol.desc }

ent-RapidBulletKinetic = rapid kinetic bolt
    .desc = { ent-BulletKinetic.desc }
ent-WeakBulletKinetic = { ent-RapidBulletKinetic }
    .desc = { ent-RapidBulletKinetic.desc }
ent-PelletKinetic = { ent-WeakBulletKinetic }
    .desc = { ent-WeakBulletKinetic.desc }
ent-PelletKineticSpread = { ent-PelletKinetic }
    .desc = { ent-PelletKinetic.desc }

ent-BasePKAUpgrade = PKA modkit
    .desc = A modkit for a proto-kinetic accelerator.
ent-PKAUpgradeDamage = PKA modkit (damage)
    .desc = { ent-BasePKAUpgrade.desc }
ent-PKAUpgradeRange = PKA modkit (range)
    .desc = { ent-BasePKAUpgrade.desc }
ent-PKAUpgradeFireRate = PKA modkit (fire rate)
    .desc = { ent-BasePKAUpgrade.desc }
ent-PKAUpgradePressure = PKA modkit (pressure)
    .desc = { ent-BaseSyndicateContraband.desc }
ent-PKAUpgradeSpace = PKA modkit (space)
    .desc = { ent-BasePKAUpgrade.desc }
ent-LavalandVampirismCrystal = a red crystal
    .desc = { ent-BasePKAUpgrade.desc }

ent-ProtoKineticWeaponLootSpawner = proto-kinetic weapon spawner
    .suffix = Lavaland
    .desc = { ent-MarkerBase.desc }
ent-ProtoKineticWeaponSpaceLootSpawner = space proto-kinetic weapon spawner
    .suffix = Lavaland, Space Upgrade
    .desc = { ent-MarkerBase.desc }
ent-ProtoKineticUpgradeLootSpawner = proto-kinetic upgrade spawner
    .suffix = Lavaland
    .desc = { ent-MarkerBase.desc }

multishot-component-examine = This weapon can be dual-wielded, causing it to miss { $chance }% of the time.
gun-upgrade-examine-text-pressure = This contains an illegal [color=orangered][bold]pressure[/bold][/color] upgrade.
gun-upgrade-examine-text-space = This has upgraded [color=#ff00bf][bold]space performance.[/bold][/color]
gun-upgrade-examine-text-vampirism = This contains a [color=crimson][bold]vampirism[/bold][/color] upgrade.
gun-upgrade-damage-name = [color=#ec9b2d][bold]damage[/bold][/color]
gun-upgrade-range-name = [color=#2decec][bold]range[/bold][/color]
gun-upgrade-reload-name = [color=#bbf134][bold]fire rate[/bold][/color]
gun-upgrade-vampirism-name = [color=crimson][bold]vampirism[/bold][/color]
gun-upgrade-pressure-name = [color=orangered][bold]pressure[/bold][/color]
gun-upgrade-space-name = [color=#ff00bf][bold]space[/bold][/color]

selectable-set-pka-accelerator-name = Proto-kinetic accelerator
selectable-set-pka-shotgun-name = Proto-kinetic shotgun
selectable-set-pka-repeater-name = Proto-kinetic repeater
selectable-set-pka-pistol-name = Proto-kinetic pistol
selectable-set-pka-standard-description = A standard pressure-tuned proto-kinetic weapon.
selectable-set-pka-space-description = A proto-kinetic weapon prefilled with a space modkit.
