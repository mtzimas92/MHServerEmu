# Mythic Rift Admin And Player Guide

This guide covers how to use, test, and troubleshoot the current Rift modes.

## Player Basics

Players should obtain one of these items from the Danger Room hub vendor or through admin grant commands:

- `Cosmic Rift Scenario`
- `Rift Gauntlet Scenario`
- `Boss Gauntlet Scenario`

Use the item from the Danger Room hub. Party members should also be in the Danger Room hub before launch if they want to enter the same run.

Useful player-safe commands:

```text
rift status
rift identity
rift progression
rift level
rift level cosmic [level|max]
rift level gauntlet [level|max]
rift level bossgauntlet [level|max]
rift abandon
rift recover
```

`rift recover` is the emergency button for stuck launcher or active-run state. It should be tried before relogging during playtests.

## Admin Grant Commands

Grant launchers:

```text
rift givebeacon [count]
rift giveendless [count]
```

Boss Gauntlet can be launched through the vendor item or fixed/admin launcher paths. Check current command output with:

```text
rift beacon
rift launchcandidates
rift entrypoints
```

Set or reset progression:

```text
rift setaccess [level] [cosmic|gauntlet|bossgauntlet]
rift resetprogress [cosmic|gauntlet|bossgauntlet]
rift progression [cosmic|gauntlet|bossgauntlet]
```

Set next launch level:

```text
rift level [cosmic|gauntlet|bossgauntlet] [level|max]
```

## Admin Diagnostics

Content and scaling:

```text
rift list
rift validatecontent
rift previewrandom [count] [level] [players] [minutes]
rift validaterandompool [level] [players] [minutes]
rift scale [level] [players]
rift identity
```

Modifier diagnostics:

```text
rift previewrandom [count] [level] [players] [minutes]
rift debug [contentId] [level] [players] [killQuota] [minutes]
rift run [runId]
```

These outputs include `regionAffixes` and `bossAffixes`. Cosmic Rift and Rift Gauntlet should show region affixes. Boss Gauntlet should show boss affixes that can reroll between waves.

Launcher diagnostics:

```text
rift beacon
rift beaconmode
rift diagbeacon [level] [minutes]
rift itemintent
rift vendorstock
```

Active run diagnostics:

```text
rift runs
rift run [runId]
rift status
rift perf
rift objectives
rift enter [runId]
```

Reward diagnostics:

```text
rift rewardconfig
rift rewardconfig reload
rift reward [runId]
rift rewardall [runId]
```

Debug run controls:

```text
rift create [level] [players] [killQuota] [minutes]
rift createfixed [contentId] [level] [players] [killQuota] [minutes]
rift createmix [contentId] [bossContentId] [level] [players] [killQuota] [minutes]
rift bind [runId]
rift start [runId]
rift kills [runId] [count]
rift success [runId]
rift fail [runId]
rift abort [runId]
rift remove [runId]
```

## Fixed Map Testing

Use fixed content to isolate problem regions:

```text
rift armbeaconfixed [contentId] [minutes]
```

Then use the relevant Rift launcher item. After the test:

```text
rift disarmbeacon
```

For command-only or experimental maps, record:

- `contentId`
- region prototype
- difficulty tier
- Rift level/wave
- party size
- whether native UI appeared
- whether Rift UI remained visible
- whether native bosses/events appeared
- whether the return portal worked

## Smoke Test Checklist

Basic solo Cosmic Rift:

1. Stand in Danger Room hub.
2. Run `rift resetprogress cosmic`.
3. Use `Cosmic Rift Scenario`.
4. Confirm Rift UI appears.
5. Confirm chat or `rift status`/`rift run [runId]` shows one or more `regionAffixes`.
6. Kill quota enemies.
7. Confirm final boss wave spawns only after quota.
8. Kill all required Rift bosses.
9. Confirm managed ground loot and return portal.
10. Confirm `rift progression cosmic` advanced by one level.

Rift Gauntlet wave 30:

1. Run `rift setaccess 30 gauntlet`.
2. Run `rift level gauntlet 30`.
3. Use `Rift Gauntlet Scenario`.
4. Confirm chat or `rift run [runId]` shows `regionAffixes`.
5. Confirm six randomized bosses, not six identical bosses.
6. Confirm completion occurs only after all bosses die.
7. Confirm managed ground loot appears once.
8. Confirm next Gauntlet level resets to wave 1.

Boss Gauntlet:

1. Use `Boss Gauntlet Scenario`.
2. Confirm all action stays in the boss arena/selected boss-gauntlet map.
3. Confirm sequential waves and short rest between waves.
4. Confirm boss count rises slowly and caps at 6.
5. Confirm each wave start message or `rift run [runId]` shows `bossAffixes`, and later wave configs can reroll.
6. Die or end the run.
7. Confirm recovery from death state.
8. Confirm cumulative rewards drop on the ground after the gauntlet ends.

Two-player launch:

1. Put both players in Danger Room hub.
2. Party them before launch.
3. Leader uses a launcher.
4. Confirm both players enter the same Rift.
5. Swap party leader mid-run.
6. Finish the Rift.
7. Confirm both admitted players receive intended rewards/progression.
8. Confirm each player can use the return portal independently.

Anti-carry progression:

1. Player A has high Cosmic Rift access.
2. Player B has level 1 Cosmic Rift access.
3. Player A launches a high-level Cosmic Rift with Player B admitted.
4. Clear it.
5. Confirm Player B does not inherit Player A's high max level and advances by at most one eligible level.

## Known High-Value Tests

Retest these after region/boss pool changes:

- Native UI suppression on raid/static-scenario regions.
- Queue bypass on raid-like regions.
- Party leader swap between repeated runs.
- Leaving party mid-run.
- Death during timer expiration.
- Return portal after completion.
- Boss spawns near players, not pets, turrets, minions, or the furthest-back player.
- Mini-boss/champion kill contribution.
- Native terminal bosses not counting as Rift bosses.
- Cosmic artifacts dropping from managed rewards even in non-cosmic-looking regions.
- Cube shards and XP orbs appearing as ground drops.

## Build And Test

Build:

```powershell
dotnet build src\MHServerEmu\MHServerEmu.csproj -p:Platform=x64
```

Focused tests:

```powershell
dotnet test src\MHServerEmu.Games.Tests\MHServerEmu.Games.Tests.csproj -p:Platform=x64 --filter MythicRift
```

## Troubleshooting

No launcher item text/icon:

- Confirm `PatchDataMythicRift.json` is loaded.
- Confirm the `AchievementStringMap_Z_MythicRiftScenario.json` file is deployed.
- Confirm the patch manager accepts `AssetId`.
- Run `rift beacon` and `rift launchcandidates`.

Item opens normal Danger Room behavior:

- Run `rift beaconmode`.
- Run `rift diagbeacon`.
- Confirm `Item.cs`, `Item.ItemActions.cs`, and `Agent.cs` hooks are present.
- Confirm the item prototype is one of the supported launcher or presentation prototypes.

Vendor does not show items:

- Open the Danger Room hub reward vendor.
- Run `rift vendorstock` while the vendor dialog target is active.
- Confirm `Player.Vendors.cs` and `VendorOption.cs` hooks are present.

Rewards missing:

- Run `rift rewardconfig reload`.
- Run `rift rewardconfig`.
- Confirm reward mode and level/wave filters match the test run.
- Confirm the loot table or item prototype resolves.

Stuck/dead after failure:

- Run `rift recover`.
- Capture `rift status`, `rift run [runId]`, and logs around the death/failure timestamp.

Native UI appears:

- Run `rift objectives`.
- Record the exact content id and region prototype.
- Treat that map as command-only until the native UI/metagame behavior is understood.
