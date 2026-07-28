# Mythic Rift Rewards

Rift rewards are controlled by:

`src/MHServerEmu.Games/Data/Game/MythicRift/CosmicRiftRewards.json`

The current profile is:

`worthwhile-v8-chest-payouts`

The reward system is intentionally server-side and configurable. The desired player experience is deliberate ground loot, clear progression value, and targetable chase rewards rather than random native terminal/event loot.

## Global Behavior

| Setting | Current intent |
| --- | --- |
| `enabled` | Enables managed Rift rewards. |
| `grantBossLootOnSuccess` | Allows boss loot on successful Cosmic Rift runs. |
| `grantBossLootOnFailure` | Disabled by default. |
| `grantBossLootInThirtyWaveMode` | Disabled so Rift Gauntlet uses managed rewards instead of every boss dumping native loot. |
| `suppressNativeRiftBossLoot` | Prevents duplicate native drops from Rift bosses. |
| `defaultDelivery` | `ground`; managed baseline loot should appear on the floor unless an entry opts into chests. |

Rift bosses should not produce both inventory-delivered and ground-delivered duplicates. Managed reward drops are meant to be visible and deliberate.

## Delivery Modes

Reward table entries and guaranteed items can set:

- `ground`: roll or spawn immediately onto the floor.
- `inventory`: give directly to the player.
- `chest`: group the player's chest-delivered rewards into one interactable Rift reward chest. The loot is not rolled until that player opens the chest, then it drops on the ground at the chest position.

The current profile uses `chest` for reward recipe table rolls and keeps guaranteed currency/XP items on `ground`. This makes chase loot feel like a deliberate payout while keeping cube shards and XP orbs reliable.

## Loot Table Aliases

`lootTableAliases` maps readable ids to real loot table prototypes. Use aliases in recipes when possible so later tuning is easier.

Current important aliases include:

- `cosmic-artifacts`: `Loot/Tables/Mob/Bosses/CosmicArtifactsTable.prototype`
- `cube-shards-100`: `Loot/Tables/CSGrant/CSGrantCrateCubeShards100Table.prototype`
- `hero-commendations-25`: `Loot/Tables/CSGrant/CSGrantCrateHeroCommendation25BoxTable.prototype`
- `protector-commendation`: `Loot/Tables/Achievements/ProtectorCommendation.prototype`
- `omega-file-box-100`: `Loot/Tables/CSGrant/CSGrantCrateOmegaFileBox100Table.prototype`
- `danger-room-blue-box`: `Loot/Tables/RandomGiftboxes/DailyGift/DangerRoomBlueBoxTable.prototype`
- `ultimate-upgrade-token`: `Loot/Tables/Achievements/ItemPowerLootTable/UltimateUpgradeLT.prototype`
- `offense-defense-ring`: `Loot/Tables/ItemType/RingOffenseOrDefense.prototype`
- `unique-single`: `Loot/Tables/Achievements/RandomChallengeUniqueSingle.prototype`
- `unique-double`: `Loot/Tables/Achievements/RandomChallengex2UniqueSingle.prototype`

Use only tables that resolve and produce useful items in live testing.

## Reward Recipes

`rewardRecipes` are optional grouped table rolls. They can be filtered by:

- `modes`
- `minRiftLevel`
- `maxRiftLevel`
- `successOnly`
- `checkpointOnly`
- `classicOnly`
- `contentIds`
- `bossSourceIds`

Each table entry can set:

- `lootTable`
- `chancePercent`
- `rolls`
- `itemLevel`
- `delivery`

Use `itemLevel: 69` for level 69 chase gear and rings when the table supports it.

## Guaranteed Items

`guaranteedItems` directly grants specific item prototypes and quantities. This is where cube shards and XP orbs are easiest to control.

The XP orb prototype currently used is:

`Entity/Items/Orbs/Items/ExperienceOrbSUPERMEGALargeNoMod.prototype`

Runtime id:

`12632728816580106976`

Cube shards are expected to be available every floor/run through guaranteed reward rules. If players report no cube shards, first run:

```text
rift rewardconfig reload
rift rewardconfig
```

Then confirm the guaranteed shard entries are enabled, scoped to the expected mode, and using `delivery: ground`.

## Cosmic Rift Reward Direction

Cosmic Rift is the infinite climb mode. Rewards should improve with Rift level but not become mandatory one-shot-survival content.

Suggested reward identity:

- Every clear: cube shards and XP orbs.
- Low tiers: small chance at cosmic artifacts, DR-style boxes, item level 69 uniques.
- Mid tiers: better chance at cosmic artifacts, commendation crates, item level 69 rings/uniques.
- High tiers: stronger chances at cosmic artifacts, Omega-related boxes, ultimate tokens, and other chase tables.
- Very high tiers: more reliable chase rolls, but avoid letting reward quality force unhealthy damage scaling.

Cosmic Rift should reward pushing, but it should still be reasonable to farm lower levels once unlocked.

## Rift Gauntlet Reward Direction

Rift Gauntlet is the 30-wave mode. It should feel faster and more boss-focused than Cosmic Rift.

Suggested reward identity:

- Every wave/clear: cube shards and XP orbs.
- Waves 20-24: stronger basic rewards and more useful boxes.
- Waves 25-30: high-value boss/chase windows.
- Wave 30: best reward bundle, then mode progression resets to wave 1.

Cosmic artifact rewards should be concentrated in waves 25-30 and should use controlled managed rewards, not native loot from every individual boss.

## Boss Gauntlet Reward Direction

Boss Gauntlet is survival-style. Rewards should be delayed until the run ends, because the point is to push as far as possible before failure.

Suggested reward identity:

- No meaningful loot during active waves.
- On death/failure/end, drop cumulative ground rewards based on completed waves.
- Start XP orb rewards at wave 10.
- Scale XP orbs from 10 to 40, and always give 40 after wave 40.
- Add better chase rolls as completed wave count increases.

Boss Gauntlet rewards should feel like a payout for the whole run, not a flood of loot every few seconds.

## Completion Crafter

Successful eligible Rift runs can spawn a completion crafter.

Current behavior:

- 3 attempts per run.
- 30% chance per attempt.
- A successful upgrade ends the attempts for that run.
- Upgrades by +1 item level.
- Cap is item level 75.
- Unique gear slots 1-5: item level 69-74.
- Cosmic gear slots 1-5: item level 63-74.

This gives long-term power progression without immediately turning every item into item level 75.

## Reloading Rewards

After editing the JSON on a running server:

```text
rift rewardconfig reload
rift rewardconfig
```

If a drop does not appear:

1. Confirm the JSON file is deployed to the server data path actually used by the running process.
2. Confirm `enabled: true` on the recipe/item.
3. Confirm mode filters match the run mode.
4. Confirm `minRiftLevel`, `maxRiftLevel`, `minWave`, and `maxWave` include the tested run.
5. Confirm `successOnly` and `checkpointOnly` match the outcome.
6. Confirm the prototype id or loot table resolves.
7. Confirm `delivery` is `ground` if testers are looking for a floor drop.

## Tuning Principles

- Prefer a few meaningful rewards over many random tables.
- Keep native boss/event loot suppressed in Rift contexts.
- Make cube shards reliable.
- Make cosmic artifacts deliberate and level-gated.
- Keep high-end chase rewards exciting but rare enough that 30 fast waves do not outclass all other game modes.
- Use live test feedback to tune quantities. JSON correctness does not prove the mode feels worthwhile.
