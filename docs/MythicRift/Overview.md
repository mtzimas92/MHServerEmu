# Mythic Rift Overview

Mythic Rift is a server-side endgame mode built around private Rift instances, controlled boss encounters, visible progression, and deliberate ground loot. The feature is meant to give players a repeatable progression path with power growth, chase rewards, and enough map/boss variety that it does not feel like one recycled terminal.

## Current Modes

| Player item | Internal mode | Main behavior |
| --- | --- | --- |
| `Cosmic Rift Scenario` | `Standard` | Infinite climb. Clear a kill quota, spawn the final Rift boss wave, complete before the timer expires. |
| `Rift Gauntlet Scenario` | `Endless` in code | 30-wave mode. Wave/scaling pattern loops every 30 levels, then progression resets to wave 1 after clearing wave 30. |
| `Boss Gauntlet Scenario` | `BossGauntlet` | Endless boss-only survival. Boss waves escalate slowly. Managed rewards drop when the gauntlet ends. |

The code still uses `Endless` for the 30-wave Rift Gauntlet mode because that was the original development name. Player-facing text should use `Rift Gauntlet Scenario`.

## Launcher Prototypes

| Mode | Technical base | Presentation shell |
| --- | --- | --- |
| Cosmic Rift | `Entity/Items/Consumables/Prototypes/DangerRoom/PortalToRandomMaxAffixDungeon.prototype` | `Entity/Items/Consumables/Prototypes/DangerRoom/DangerRoomScenarioCrateUniqueCableFight.prototype` |
| Rift Gauntlet | `Entity/Items/Consumables/Prototypes/DangerRoom/PortalToDangerRoomRandomThemeNoAffixesPurple.prototype` | `Entity/Items/Consumables/Prototypes/Test/TestHearthStone.prototype` |
| Boss Gauntlet | `Entity/Items/Consumables/Prototypes/DangerRoom/PortalToDangerRoomRandomThemeNoAffixesBlue.prototype` | `Entity/Items/Consumables/Prototypes/Test/TestStunKit.prototype` |

`PatchDataMythicRift.json` makes the launcher bases live and assigns presentation item display names, descriptions, and icons. The regular `PortalToRandomDungeon` item is deliberately not a Rift launcher.

## Player Flow

1. Player buys or is granted a Rift launcher item.
2. Player uses the item from the Danger Room hub.
3. Server intercepts the item or item-power activation.
4. Server creates a private Rift run and teleports admitted party members.
5. Rift UI shows level/wave, quota or boss count, and timer.
6. Rift-specific enemies and bosses advance the objective.
7. Rewards drop on the ground through managed reward logic.
8. Completion spawns a return portal to the Danger Room hub.

Only party members in the Danger Room hub at launch are admitted. Offline or out-of-region members are not added to the run, and should not receive rewards/progression for that run.

## Progression

Cosmic Rift progression is persistent per player and increases by at most one unlocked level per clear. A lower-level player completing a much higher player's Rift should not inherit the leader's full maximum level.

Rift Gauntlet progression is separate from Cosmic Rift progression. The 30-wave cycle normalizes levels into waves 1-30. Clearing wave 30 should send that mode back to wave 1.

Boss Gauntlet progression is separate from both other modes. It is wave-based and currently does not use the same reward/progression path as Cosmic Rift or Rift Gauntlet.

## Scaling

Scaling lives in `MythicRiftScaling.cs`.

Cosmic Rift:

- Piecewise climb curve inspired by Diablo 3 rifts.
- Health cap: `12x`.
- Damage cap: `2.4x`.
- Party health buckets: `1.0x`, `1.5x`, `2.0x`, `2.5x`, `3.0x` for 1-5 players.

Rift Gauntlet:

- Uses the 30-wave profile table.
- Party health multiplier is not added on top of the wave table.
- Damage uses the stronger of baseline wave damage or total boss pressure, capped at `2.4x`.
- Boss count ramps from 1 boss early to 6 bosses at wave 30.

Boss Gauntlet:

- Boss count increases by one every 6 waves, capped at 5.
- Health cap: `7x`.
- Damage cap: `2x`.
- Party health multiplier is capped lower for this mode.

Difficulty tiers are selected by launcher/run state:

- Base tier: `Difficulty/Tiers/Tier3Superheroic.prototype`
- High tier: `Difficulty/Tiers/Tier4Cosmic.prototype`
- Cosmic Rift enters high tier at level 70+.
- Rift Gauntlet enters high tier at wave 20+.
- Boss Gauntlet enters high tier at wave 50+.

## Rift Modifiers

Runs now roll native Danger Room enemy affixes from the existing `RegionAffixTable` system.

Cosmic Rift and Rift Gauntlet roll rift-wide region affixes before teleport, so the private region is created with those affixes and native population can inherit them. Rift-spawned bosses, milestone mini-bosses, and custom Rift population also receive the same enemy boosts so they do not feel disconnected from the run modifier.

Boss Gauntlet keeps the arena stable but rolls boss-scoped affixes per wave. The same boss can therefore fight differently across waves or runs without patching shared boss prototypes.

Current first-pass modifier counts:

- Cosmic Rift and Rift Gauntlet: 1 affix by default, 2 at level/wave 30, 3 at level/wave 70.
- Boss Gauntlet: 1 boss affix by default, 2 at wave 30, 3 at wave 70.

This pass intentionally uses enemy-boost affixes only. Player lockout affixes and leaderboards are still future work.

## Rift UX

Current in-run UX includes Rift-owned objective widgets, automatic short staging delays before boss waves, a modifier/status button, boss/party status indicators where client widgets are available, and optional hotspot-style environmental hazards from `Game/MythicRift/CosmicRiftHazards.json`.

Useful UX checks:

- `rift identity`: summarizes the three mode identities and current reward/hazard profiles.
- `rift modifiers`: shows the active affixes for the invoking player's run.
- `rift hazardconfig`: shows the resolved hazard tuning profile.

## Content Rules

The mode selects from registered `MythicRiftContentEntry` map and boss entries. SIP presence is not enough to make a region or boss safe; random eligibility should only include entries that have been tested for teleporting, UI behavior, native event suppression, boss AI, spawn bounds, and reward behavior.

Current design direction:

- Exclude maps with native UI or queue behavior unless the Rift UI can reliably suppress/replace it.
- Keep large patrol maps command-only until validated.
- Avoid native bosses that spawn as part of a terminal or metagame unless they are explicitly suppressed.
- Keep problematic bosses out of the random pool if their AI or assets fail in Rift contexts.

## Known Constraints

- Live multiplayer behavior still matters more than unit tests for party admission, party leader swaps, streaming/download errors, and return portals.
- Native raid/static-scenario regions can add UI or queue behavior outside normal terminal assumptions.
- Rewards are configurable, but item desirability still needs live economy testing.
- The 30-wave mode is currently named `Endless` internally; do not rename the enum casually unless save/progression compatibility is planned.
