# Omega Tier Items Session Notes - 2026-08-26

This note captures the important work from the Omega tier / Mythic Rift reward session so a future Codex session can recover the context quickly.

## Branch State

- Working repo: `C:\Users\mtzim\Desktop\CodexStuff\MHServerEmu-main`
- Target branch: `rifts-contained`
- Remote branch updated earlier in the session: `origin/rifts-contained`
- Previous Omega reward commit already pushed: `7cf59ef1b Add Omega tier rift reward drops`
- Before pulling parity into `MHServerEmu-main`, local dirty work was saved as:
  - `stash@{0}: On rifts-contained: codex backup before rifts-contained parity 2026-08-26`
- A backup branch was also created before parity reset:
  - `backup/rifts-contained-before-parity-20260826`

## Earlier Omega Reward Work

Omega item reward work was moved into the rifts-contained branch and pushed. The key intent:

- Rifts can award Omega tier items using `Entity/Items/Rarity/R6Omega.prototype`.
- Omega armor slots 1-5 should drop as armor, not only rings.
- Slot 2 and slot 4 Omega armor are biased toward preferred offensive affixes:
  - Critical Hit
  - Critical Damage
  - Brutal Damage
- Omega armor rolls like a cosmic-style item with an additional Omega affix, not with enchantments or challenge bonuses baked in.
- The Danger Room scenario vendor should sell three rift launcher items only, one per mode.
- Mythic Rift data files are copied by wildcard from `Data/Game/...` folders through the project file.

Important files for that work:

- `src/MHServerEmu.Games/OmegaTierItems/OmegaTierItemFactory.cs`
- `src/MHServerEmu.Games/OmegaTierItems/OmegaTierAffixLimits.cs`
- `src/MHServerEmu.Games/Data/Game/Patches/PatchData_OmegaTierItems.json`
- `src/MHServerEmu.Games/Data/Game/MythicRift/CosmicRiftRewards.json`
- `src/MHServerEmu.Games/MythicRifts/*`
- `src/MHServerEmu/MHServerEmu.csproj`

## Current UI Crafting / Enchanting Fix

Problem:

- Omega armor items were dropping, but could not be enchanted or crafted through the normal UI.
- Lordunborn handled similar items through commands, but the desired behavior here is normal UI crafting/enchanting.
- We do not want Omega drops to spawn with enchantments or challenge bonuses already attached.
- Normal crafting costs should remain unchanged.

Implementation approach:

- Do not add the normal enchant/challenge recipes to the Mythic Rift completion crafter.
- Instead, globally make normal crafting/enchant recipes treat `R6Omega` as valid wherever their input restrictions already accept `R5Cosmic`.
- Keep dropped Omega items clean.
- Add a narrow crafting-only affix limit exception so the UI can add:
  - one `Runeword` enchantment
  - one `Unique` challenge bonus
  to Omega armor slots 1-5.

Files changed:

- `src/MHServerEmu.Games/OmegaTierItems/OmegaTierAffixLimits.cs`
  - Resolves both `R5Cosmic` and `R6Omega`.
  - Walks all `CraftingRecipePrototype` inputs at startup.
  - Recursively handles nested `RestrictionSetInputPrototype` restrictions:
    - `RarityRestrictionPrototype`
    - `ConditionalRestrictionPrototype`
    - `RestrictionListPrototype`
  - Adds `R6Omega` to any recipe input rarity list that already includes `R5Cosmic`.
  - Logs how many recipe rarity restrictions were expanded.
  - Exposes `AllowOmegaCraftingAffixLimitOverride(...)` for crafting-only Omega affix limit checks.

- `src/MHServerEmu.Games/Loot/LootUtilities.cs`
  - Calls `OmegaTierAffixLimits.AllowOmegaCraftingAffixLimitOverride(...)` only when a positional affix add would exceed the normal limit.
  - This affects only `LootContext.Crafting`, `R6Omega`, armor slots 1-5, and positions `Runeword` or `Unique`.

Why this should not affect drops:

- The override requires `LootContext.Crafting`.
- Normal Omega drop generation still uses `LootContext.Drop`.
- The normal roll path still does not add runewords by default.
- We did not raise `MaxUniques` on the normal drop affix row, avoiding random challenge bonuses on dropped Omega armor.

## Verification

Build command run:

```powershell
dotnet build "C:\Users\mtzim\Desktop\CodexStuff\MHServerEmu-main\MHServerEmu.sln" -c Debug -p:OutDir="C:\Users\mtzim\Desktop\CodexStuff\MHServerEmu-main\artifacts\buildcheck\"
```

Result:

- Build succeeded.
- Only warning was the pre-existing unused variable warning in `PlayerCommands.cs`.

## Suggested Test Pass

Use the normal crafter/enchanter UI, not commands:

1. Obtain an Omega armor item in gear slot 1-5.
2. Try an enchant recipe that accepts the same slot on cosmic armor.
3. Try a challenge bonus recipe.
4. Confirm recipe costs are the normal costs.
5. Confirm newly dropped Omega armor still does not spawn with enchantments or challenge bonuses by default.
6. Watch startup logs for:
   - `Permitted R6Omega on ... crafting recipe input rarity restriction(s) across ... recipe(s)`

## Known Context From This Session

- The previous reward-room crafter logic is intentionally separate. It is for Mythic Rift completion upgrade recipes, not general enchanting/challenge crafting.
- If the UI still refuses Omega items after this change, the next likely blocker is client-side recipe filtering or recipe visibility, not server-side crafting validation.
- If server-side crafting succeeds but UI remains disabled, inspect the client recipe data / inventory filter sent to the client before adding more server-side mutation logic.
