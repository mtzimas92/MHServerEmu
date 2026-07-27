# Mythic Rift Code Architecture

Most Rift logic is isolated under:

`src/MHServerEmu.Games/MythicRifts/`

Existing server files contain only the hooks needed to enter, run, reward, persist, and exit the mode.

## Core Components

| File | Responsibility |
| --- | --- |
| `MythicRiftManager.cs` | Runtime run registry, map/boss selection, party admission, kill/boss tracking, UI, timers, rewards, completion/failure cleanup, return portal, completion crafter spawn. |
| `MythicRiftLauncherService.cs` | Launcher item recognition, pending intent state, item-use conversion into Rift requests, tracked beacon charges, difficulty tier selection. |
| `MythicRiftEntryService.cs` | Logical entry points and candidate launcher item definitions. |
| `MythicRiftRewardTuning.cs` | Loads and resolves `CosmicRiftRewards.json`. |
| `MythicRiftScaling.cs` | Health, damage, party, wave, and boss-count scaling. |
| `MythicRiftRunState.cs` | Per-run mutable state and participant bookkeeping. |
| `MythicRiftRunConfig.cs` | Run configuration created at launch. |
| `MythicRiftUiController.cs` | Rift widget ownership and native widget cleanup. |
| `MythicRiftItemPresentation.cs` | Presentation item mapping for the three launcher modes. |
| `MythicRiftProgression.cs` | Cosmic one-level progression and 30-wave cycle progression helpers. |

Admin commands live in:

`src/MHServerEmu/Commands/Implementations/MythicRiftCommands.cs`

Automated tests live in:

`src/MHServerEmu.Games.Tests/MythicRifts/`

## Existing-Code Hooks

| File | Hook |
| --- | --- |
| `Game.cs` | Owns `MythicRiftManager`, `MythicRiftEntryService`, and `MythicRiftLauncherService`; ticks the manager. |
| `Player.cs` | Persists Cosmic and Rift Gauntlet highest unlocked levels. |
| `Archive.cs` | Adds archive version for separate Rift Gauntlet progression. |
| `Item.cs` and `Item.ItemActions.cs` | Intercepts launcher item use before normal item behavior falls through. |
| `Agent.cs` | Intercepts launcher item power activation. |
| `Player.Vendors.cs` | Injects Rift launcher items into the Danger Room vendor and isolates completion-crafter stock. |
| `Player.Crafting.cs` | Handles Rift completion-crafter upgrade attempts. |
| `VendorOption.cs` | Ensures Rift vendor/crafter stock exists before the dialog renders. |
| `Transition.cs` | Supports direct Rift return portals and blocks unsafe native transitions while a Rift is active. |
| `Teleporter.cs` | Allows Rift launches to bypass queue regions and preserve requested Rift difficulty tier. |
| `LootRollSettings.cs` | Supports forced item level for managed Rift rewards. |
| `PrototypePatchEntry.cs` | Accepts `AssetId` patch values used by launcher icon patches. |

Keep new feature logic inside `MythicRifts` whenever practical. Only add hooks outside that folder when the base server needs to notify or delegate to the Rift subsystem.

## Runtime Lifecycle

Launcher:

1. Vendor injection or admin command grants a launcher item.
2. `Item` / `Item.ItemActions` / `Agent` intercepts use.
3. `MythicRiftLauncherService.TryHandleItemUse()` resolves mode and entry point.
4. Launcher service creates a `MythicRiftEntryRequest`.
5. `MythicRiftEntryService` validates entry point, item, random/fixed content, timer, and level.

Run creation:

1. `MythicRiftManager` selects map/boss content.
2. Party members in the correct launch region are admitted.
3. A private region is requested.
4. Teleporter preserves Rift difficulty tier and skips queue-region behavior where needed.
5. Run state binds to the created region.

Active run:

1. Native objectives/UI are suppressed where possible.
2. Rift UI owns level/wave, timer, and quota/boss status.
3. Kills advance quota only when they belong to the Rift flow.
4. Milestone or final bosses spawn around admitted alive players, not pets/turrets/minions.
5. Rift boss native loot is suppressed.

Completion/failure:

1. Managed rewards resolve through `MythicRiftRewardTuning`.
2. Completion crafter attempts are granted on successful eligible runs.
3. Return portal is spawned on completion.
4. Failure returns or recovers players where possible, with special Boss Gauntlet death recovery.
5. Difficulty scaling and temporary state are restored.

## Vendor And Crafter Notes

The Danger Room hub reward vendor is the intended no-admin acquisition path. `Player.Vendors.cs` injects all three launcher items when the scoped vendor is opened.

Completion crafter behavior:

- Spawns after eligible Rift completion.
- Gives 3 attempts per run.
- Each attempt has a 30% upgrade chance.
- A success ends that run's attempts.
- Supports item level 69-74 Unique gear slots 1-5.
- Supports item level 63-74 Cosmic gear slots 1-5.
- Output item level increases by 1, capped at 75.

Do not replace the full stock vendor/crafting files from another branch. They are high-conflict files; port only the Rift-specific hooks that current main needs.

## Patch And Localization Files

Patch file:

`src/MHServerEmu.Games/Data/Game/Patches/PatchDataMythicRift.json`

Achievement string maps:

- `AchievementStringMap_MythicRift.json`
- `AchievementStringMap_MythicRiftLevelWidget.json`
- `AchievementStringMap_MythicRiftMessages.json`
- `AchievementStringMap_Z_MythicRiftScenario.json`

The `Z_` map keeps Rift launcher strings late in load order. Current strings should present:

- `Cosmic Rift Scenario`
- `Rift Gauntlet Scenario`
- `Boss Gauntlet Scenario`

## Build And Test

Use x64 when building this repo:

```powershell
dotnet build src\MHServerEmu\MHServerEmu.csproj -p:Platform=x64
```

Run the focused tests:

```powershell
dotnet test src\MHServerEmu.Games.Tests\MHServerEmu.Games.Tests.csproj -p:Platform=x64 --filter MythicRift
```

The unit tests cover scaling, progression helpers, content entry behavior, launcher selection, run state, reward tuning, and UI ownership. They do not prove live client UI, multiplayer zoning, streaming, native map behavior, or boss AI.
