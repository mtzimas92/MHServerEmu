# Mythic Rift and Omega Update

## Patch Notes

- Renamed **Cosmic Rift** to **Infinite Rift**.
- Renamed **Rift Gauntlet** to **Omega Training**.
- Rift difficulty, timers, party access, scaling, and boss counts are configurable in `CosmicRiftScaling.json`.
- Omega Training boss counts now come from its `modes.Endless.levels` breakpoints:
  - Levels 1-9: 1 boss
  - Levels 10-19: 2 bosses
  - Levels 20-24: 3 bosses
  - Levels 25-27: 4 bosses
  - Levels 28-29: 5 bosses
  - Level 30: 6 bosses
- Ambient Cosmic gear in Omega difficulty has a configurable 33.33% chance to become Omega gear.
- Added server-side loot filtering for individual Omega gear slots 1-5.
- Added filters for rings, medals, insignias, Team-Up gear, catalysts, and Uru-Forged items.
- Loot filters support global and per-character settings.
- Improved party reward-room transportation for all Rift modes.
- Infinite Rift is solo-only. Omega Training and Boss Gauntlet allow parties.
- Offensive Omega rings roll two offensive and one defensive category.
- Defensive Omega rings roll two defensive and one offensive category.
- Rift item-level upgrades now cap at item level 72.
- Added an Omega Raid Vendor near the Helicarrier crafter with placeholder stock.

## Loot Filter Commands

```text
!filter omega gear01 on
!filter omega gear05 on me
!filter omega all off
!filter set ring cosmic
!filter set teamup epic me
!filter uruforged on
!filter clear omega
!filter clearall
!filter list
!filter rarities
```

An optional final scope can be `global`, `me`, or a character name. Rarity filters remove matching items at or below the selected rarity.

## Test Plan

### 1. Loot Filtering

1. Enable filtering for one Omega slot, such as `gear01`.
2. Leave another slot, such as `gear02`, unfiltered.
3. Generate or farm Omega equipment.
4. Confirm Gear 1 items are removed while Gear 2 items still drop.
5. Repeat with `me` and verify that changing heroes uses the correct character-specific settings.
6. Test each additional category: ring, medal, insignia, Team-Up gear, catalyst, and Uru-Forged.

### 2. Omega-Difficulty Drops

1. Farm at least 30-60 Cosmic-eligible armor or ring drops in an Omega patrol or other Omega-difficulty region.
2. Confirm that approximately one third become Omega items.
3. Confirm the remaining items retain their normal Cosmic rarity.
4. Confirm guaranteed Rift Omega rewards remain guaranteed and are not reduced to 33.33%.

The conversion chance is configured through `omegaDifficultyPromotionChancePct` in `OmegaTierItems.json`.

### 3. Omega Training

Test levels 9, 10, 20, 25, 28, and 30.

| Level | Expected Bosses |
|---:|---:|
| 9 | 1 |
| 10 | 2 |
| 20 | 3 |
| 25 | 4 |
| 28 | 5 |
| 30 | 6 |

Also confirm:

- The timer is 10 minutes.
- Parties can launch and enter together.
- Level 5 uses `Difficulty/CosmicGate.prototype`.
- Old `thirtyWaveProfiles` health scaling is no longer applied.

### 4. Infinite Rift

1. Confirm the timer is 5 minutes.
2. Attempt entry while in a party and confirm the launch is rejected.
3. Confirm level 5 uses `Difficulty/CosmicGate.prototype`.
4. Complete a run and verify all eligible players reach the reward room where applicable.
5. Check levels 31 and above to verify the configurable 5% health growth.

### 5. Boss Gauntlet

1. Launch while grouped.
2. Test both successful completion and failure.
3. Confirm every admitted player reaches the reward room, not only the party leader.
4. Verify boss-count breakpoints against `CosmicRiftScaling.json`.
5. Test levels 45 and 46 carefully because they intentionally use extreme test scaling.

### 6. Omega Rings

1. Generate several offensive Omega rings.
2. Confirm each uses two offensive and one defensive affix category.
3. Generate several defensive Omega rings.
4. Confirm each uses two defensive and one offensive affix category.

### 7. Item-Level Upgrades

1. Upgrade an eligible item from item level 71.
2. Confirm the successful output is item level 72.
3. Attempt another upgrade and confirm an item already at level 72 is rejected.

### 8. Omega Raid Vendor

1. Enter the Helicarrier and locate the vendor beside the crafter.
2. Confirm six offers are present.
3. Confirm each unique placeholder costs 500 Champion's Commendations.
4. Confirm the item-level upgrade placeholder costs 100 commendations.
5. Purchase an offer and verify that one item is granted and the correct currency is removed.

The five named uniques and the item-level upgrade currently use placeholder item prototypes. Their final prototypes still need to be supplied and substituted before release.

## Main Configuration Files

- `src/MHServerEmu.Games/Data/Game/MythicRift/CosmicRiftScaling.json`
- `src/MHServerEmu.Games/Data/Game/MythicRift/CosmicRiftRewards.json`
- `src/MHServerEmu.Games/Data/Game/MythicRift/OmegaTierItems.json`
