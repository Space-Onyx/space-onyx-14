drone-active = A maintenance drone. It seems totally unconcerned with you.
drone-dormant = A dormant maintenance drone. Who knows when it will wake up?
drone-activated = The drone whirrs to life!
drone-too-close = Be aware of your proximity to { THE($being) }.
drone-cant-use-nearby = This act could cause harm to { THE($being) }. Your programming prevents it.
drone-cant-use = This act could cause harm to the station or its inhabitants. Your programming prevents it.
drone-med-battery = Be aware that you will cease to function permanently when your battery runs out.
drone-low-battery = Seek a charging station immediately. You are in existential danger.
alerts-drone-battery-name = Battery
alerts-drone-battery-desc = If your battery depletes, you will self-destruct.

ghost-role-information-drone-name = Maintenance Drone
ghost-role-information-drone-description = Maintain the station. Ignore other beings except drones. Use +/+d to talk in the Dronemind.
ghost-role-information-drone-rules = You are bound by these laws both in-game and out-of-character:

    1. You may not interfere with the affairs of any being except another drone, regardless of intent or circumstance.
    2. Your goal is to maintain or improve the station to the best of your ability.
    3. You may not take any action which causes damage or harm to the station or its inhabitants.

name-identifier-format-drone = DR-{ $number }
language-DroneTalk-name = Drone
language-DroneTalk-description = Incomprehensible to most non-drones!
chat-language-DroneTalk-name = Drone

ent-Drone = maintenance drone
    .desc = A small maintenance robot governed by strict laws.
ent-SpawnMobDrone = Drone Spawner
ent-ClothingBackpackSatchelDrone = drone satchel
    .desc = { ent-ClothingBackpackSatchel.desc }
ent-DroneSatchelUnremovable = { ent-ClothingBackpackSatchelDrone }
    .suffix = Unremovable
    .desc = { ent-ClothingBackpackSatchelDrone.desc }
ent-trayScannerUnremoveable = { ent-trayScanner }
    .suffix = Unremovable
    .desc = { ent-trayScanner.desc }
ent-OmnitoolUnremoveable = { ent-Omnitool }
    .suffix = Unremovable
    .desc = { ent-Omnitool.desc }
ent-WelderExperimentalUnremoveable = { ent-WelderExperimental }
    .suffix = Unremovable
    .desc = { ent-WelderExperimental.desc }
ent-RCDRechargingUnremoveable = { ent-RCDRecharging }
    .suffix = Unremovable
    .desc = { ent-RCDRecharging.desc }
ent-NetworkConfiguratorUnremoveable = { ent-NetworkConfigurator }
    .suffix = Unremovable
    .desc = { ent-NetworkConfigurator.desc }
ent-PinpointerStationUnremoveable = { ent-PinpointerStationOnyx }
    .desc = You are the station. Find yourself. Press E to activate.
    .suffix = Unremovable
ent-ActionDronePlayMidi = Play MIDI
    .desc = Contribute to the ambiance.
ent-ActionShowStationMap = Station Map Interface
    .desc = View a station map interface.
