# Lavaland map closure

Static scan against target prototype IDs: zero missing.

The original report contained 19 IDs despite its stated count of 18. Nine unique entities were ported with their mechanics, assets, and locale. Ten map references use exact target-native equivalents:

```text
BorgBeaker -> Beaker
ClothingMaskSexyMime -> ClothingMaskBlushingMime
ClothingOuterHardsuitSyndicateDurathread -> ClothingOuterEVASuitSyndicate
LeftLegReptilian -> OrganReptilianLegLeft
PlasmaOre1Unprocessed -> PlasmaOre1
RightLegReptilian -> OrganReptilianLegRight
SyndicateSpawner -> LootSpawnerContrabandHigh
SyringeGun -> LauncherSyringe
ToySkeleton -> ToyFigurineSkeleton
VendingMachineSolsnack -> VendingMachineSnack
```

## Attribution

Source and commit attribution is recorded in `_Onyx_map_attribution.md`; no active map remains without recorded provenance.

## Validation

Shared, Server, Client, and IntegrationTests projects build. Static active-map references resolve with zero missing prototypes. The central outpost loads in its isolated integration test.
