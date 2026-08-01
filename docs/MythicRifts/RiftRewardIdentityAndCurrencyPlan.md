# Rift Reward Identity and Currency Plan

This is a design/example file only. It does not change live Rift rewards by itself.

## Currency Audit

The SIP currency reference audit found one clean, item-level candidate for a Rift-only currency:

| Candidate | Prototype | Runtime id | SIP refs | Notes |
| --- | --- | ---: | ---: | --- |
| Rift Sigil candidate | `Entity/Items/CurrencyItems/VacuumingCurrencies/TestCurrencyItem.prototype` | `2470498557584809493` | 0 | Best isolated drop item. Needs patchdata/localization/icon because it is test/non-live data. |
| Currency backend | `Entity/Items/CurrencyItems/CurrencyPrototypes/TestCurrency.prototype` | `11495560312682585144` | 2 | Referenced by the test currency item and `UI/Currency/TestCurrencyUI.prototype`. |
| Safer live fallback | `Entity/Items/CurrencyItems/RaidCurrency/GenoshaRaidToken.prototype` | `2852929430040615658` | 1 | Already Live, but one old Achievers loot crate references it. Current Rift crafter code already uses `GenoshaRaidCurrency`. |
| Existing crafter currency | `Entity/Items/CurrencyItems/CurrencyPrototypes/GenoshaRaidCurrency.prototype` | `12206141738751172349` | 3 | Used by token, globals, and UI. Useful if we keep the crafter on this economy. |

Other zero-reference currency prototypes exist, including `MarvelousDustCurrency.prototype` and `RunestoneCurrency.prototype`, but the cleanest actual droppable item candidate is `TestCurrencyItem.prototype`.

Recommended name: **Rift Sigil**.

Recommended approach:

1. Use `TestCurrencyItem.prototype` as the Rift Sigil drop if we want a self-contained Rift economy.
2. Patch `DesignState` to `Live`.
3. Patch display name, tooltip, and icon.
4. Add it to `CosmicRiftRewards.json` as a guaranteed ground item.
5. Add a Rift vendor that spends the matching currency backend on deterministic cache choices.

Fallback approach:

Use `GenoshaRaidToken.prototype` as Rift Sigil because it is already Live and the Rift crafter already points at `GenoshaRaidCurrency`. This is less isolated, but likely requires fewer client-facing fixes.

## Mode Identity

The three Rift modes should not all feel like "run content, get cube shards." Each should answer a different player need.

### Cosmic Rift

Identity: personal climb, progression test, steady upgrade economy.

This mode should reward pushing higher personal levels without becoming the best shortcut for everything. The payout should be reliable but not explosive.

Primary rewards:

- Rift Sigils every clear, scaling by level band.
- Cube shards as choke-point relief.
- I69 unique/artifact/ring chances at mid and high levels.
- Crafter attempts or crafter currency.
- Low-to-medium cosmic artifact chances at higher tiers.

Avoid:

- Too many guaranteed cosmic artifacts.
- Massive boss loot duplication.
- Rewards that make low levels better than normal endgame farms.

### Rift Gauntlet

Identity: 30-wave boss farm with a finish line.

This mode should be faster, punchier, and more predictable. It should reset after wave 30 and should be the place players go when they want a contained "boss gauntlet night" with a known endpoint.

Primary rewards:

- Cube shards every wave.
- Small Rift Sigil payouts at waves 5/10/15/20.
- Waves 25-30 as the targeted cosmic artifact window.
- Wave 30 as the big payout.
- Boss loot tables tuned higher only in waves 25-30.

Avoid:

- Carrying wave 31+ reward tier upward.
- Giving the same reward identity as Cosmic Rift.
- Letting every run become ten guaranteed cosmic artifacts.

### Boss Gauntlet

Identity: endless risk/reward boss survival.

This mode should not feel rewarding at wave 5. It should feel like banking risk. The player gets no loot until death/failure/end, then the floor payout should be visually satisfying.

Primary rewards:

- Ground delivery for most visible rewards.
- Cumulative Rift Sigils.
- Cumulative XP orbs from wave 10 onward.
- Big milestone jackpots at 20/25/30/40/50.
- Better odds than other modes at very high waves, because the player risked losing time to push.

Avoid:

- Only cube shards and XP orbs.
- Chest-only jackpots that players do not notice.
- Constant reward spam during the run.

## Example Patchdata For Rift Sigil

This assumes `TestCurrencyItem.prototype` becomes the Rift-only currency item.

```json
[
  {
    "Enabled": true,
    "Prototype": "Entity/Items/CurrencyItems/VacuumingCurrencies/TestCurrencyItem.prototype",
    "Path": "DesignState",
    "Description": "Enable the unused test currency item as the Rift Sigil reward currency.",
    "ValueType": "Enum",
    "Value": "Live"
  },
  {
    "Enabled": true,
    "Prototype": "Entity/Items/CurrencyItems/VacuumingCurrencies/TestCurrencyItem.prototype",
    "Path": "DisplayName",
    "Description": "Rename the unused test currency item to Rift Sigil.",
    "ValueType": "LocaleStringId",
    "Value": 18000000000000080100
  },
  {
    "Enabled": true,
    "Prototype": "Entity/Items/CurrencyItems/VacuumingCurrencies/TestCurrencyItem.prototype",
    "Path": "TooltipDescription",
    "Description": "Describe Rift Sigils as the deterministic Rift reward currency.",
    "ValueType": "LocaleStringId",
    "Value": 18000000000000080101
  }
]
```

Suggested achievement strings:

```json
{
  "18000000000000080100": {
    "en_us": "Rift Sigil"
  },
  "18000000000000080101": {
    "en_us": "Earned from Rift modes. Spend at the Rift vendor for targeted upgrade caches and progression rewards."
  }
}
```

## Example Reward JSON Fragments

These are not a full replacement file. They show the intended structure using fields that `CosmicRiftRewards.json` already supports.

### Cosmic Rift Sigil Payouts

```json
{
  "guaranteedItems": [
    {
      "id": "rift-sigil-cosmic-levels-1-19",
      "enabled": true,
      "itemPrototypeRuntimeId": 2470498557584809493,
      "quantity": 2,
      "minRiftLevel": 1,
      "maxRiftLevel": 19,
      "minWave": 1,
      "maxWave": 0,
      "successOnly": true,
      "checkpointOnly": false,
      "classicOnly": false,
      "modes": [ "standard" ],
      "contentIds": [],
      "bossSourceIds": [],
      "delivery": "ground"
    },
    {
      "id": "rift-sigil-cosmic-levels-20-49",
      "enabled": true,
      "itemPrototypeRuntimeId": 2470498557584809493,
      "quantity": 5,
      "minRiftLevel": 20,
      "maxRiftLevel": 49,
      "minWave": 1,
      "maxWave": 0,
      "successOnly": true,
      "checkpointOnly": false,
      "classicOnly": false,
      "modes": [ "standard" ],
      "contentIds": [],
      "bossSourceIds": [],
      "delivery": "ground"
    },
    {
      "id": "rift-sigil-cosmic-levels-70-plus",
      "enabled": true,
      "itemPrototypeRuntimeId": 2470498557584809493,
      "quantity": 12,
      "minRiftLevel": 70,
      "maxRiftLevel": 0,
      "minWave": 1,
      "maxWave": 0,
      "successOnly": true,
      "checkpointOnly": false,
      "classicOnly": false,
      "modes": [ "standard" ],
      "contentIds": [],
      "bossSourceIds": [],
      "delivery": "ground"
    }
  ]
}
```

### Rift Gauntlet Milestones

```json
{
  "guaranteedItems": [
    {
      "id": "rift-sigil-gauntlet-wave-5",
      "enabled": true,
      "itemPrototypeRuntimeId": 2470498557584809493,
      "quantity": 2,
      "minRiftLevel": 1,
      "maxRiftLevel": 0,
      "minWave": 5,
      "maxWave": 5,
      "successOnly": true,
      "checkpointOnly": false,
      "classicOnly": false,
      "modes": [ "rift-gauntlet" ],
      "contentIds": [],
      "bossSourceIds": [],
      "delivery": "ground"
    },
    {
      "id": "rift-sigil-gauntlet-wave-20",
      "enabled": true,
      "itemPrototypeRuntimeId": 2470498557584809493,
      "quantity": 8,
      "minRiftLevel": 1,
      "maxRiftLevel": 0,
      "minWave": 20,
      "maxWave": 20,
      "successOnly": true,
      "checkpointOnly": false,
      "classicOnly": false,
      "modes": [ "rift-gauntlet" ],
      "contentIds": [],
      "bossSourceIds": [],
      "delivery": "ground"
    },
    {
      "id": "rift-sigil-gauntlet-wave-30-final",
      "enabled": true,
      "itemPrototypeRuntimeId": 2470498557584809493,
      "quantity": 15,
      "minRiftLevel": 1,
      "maxRiftLevel": 0,
      "minWave": 30,
      "maxWave": 30,
      "successOnly": true,
      "checkpointOnly": false,
      "classicOnly": false,
      "modes": [ "rift-gauntlet" ],
      "contentIds": [],
      "bossSourceIds": [],
      "delivery": "ground"
    }
  ],
  "rewardRecipes": [
    {
      "id": "rift-gauntlet-waves-25-30-cosmic-artifact-window",
      "enabled": true,
      "minRiftLevel": 1,
      "maxRiftLevel": 0,
      "minWave": 25,
      "maxWave": 30,
      "successOnly": true,
      "checkpointOnly": false,
      "classicOnly": false,
      "modes": [ "rift-gauntlet" ],
      "contentIds": [],
      "bossSourceIds": [],
      "tables": [
        {
          "id": "cosmic-artifacts-high-window",
          "lootTable": "cosmic-artifacts",
          "chancePercent": 18,
          "rolls": 1,
          "delivery": "ground"
        }
      ]
    },
    {
      "id": "rift-gauntlet-wave-30-cosmic-artifact-finish",
      "enabled": true,
      "minRiftLevel": 1,
      "maxRiftLevel": 0,
      "minWave": 30,
      "maxWave": 30,
      "successOnly": true,
      "checkpointOnly": false,
      "classicOnly": false,
      "modes": [ "rift-gauntlet" ],
      "contentIds": [],
      "bossSourceIds": [],
      "tables": [
        {
          "id": "cosmic-artifacts-final-roll",
          "lootTable": "cosmic-artifacts",
          "chancePercent": 100,
          "rolls": 1,
          "delivery": "ground"
        },
        {
          "id": "mysterious-blue-omega-box",
          "lootTable": "mysterious-blue-omega-box",
          "chancePercent": 100,
          "rolls": 1,
          "delivery": "chest"
        }
      ]
    }
  ]
}
```

### Boss Gauntlet Deferred Payout

For Boss Gauntlet, the higher-value rewards should mostly use `delivery: "ground"` because the payout happens only when the gauntlet ends.

```json
{
  "guaranteedItems": [
    {
      "id": "rift-sigil-boss-gauntlet-cumulative",
      "enabled": true,
      "itemPrototypeRuntimeId": 2470498557584809493,
      "quantity": 1,
      "quantityMatchesRewardWave": true,
      "cumulativeWaveQuantity": false,
      "quantityCap": 75,
      "minRiftLevel": 1,
      "maxRiftLevel": 0,
      "minWave": 1,
      "maxWave": 0,
      "successOnly": false,
      "checkpointOnly": false,
      "classicOnly": false,
      "modes": [ "boss-gauntlet" ],
      "contentIds": [],
      "bossSourceIds": [],
      "delivery": "ground"
    }
  ],
  "rewardRecipes": [
    {
      "id": "boss-gauntlet-wave-20-jackpot",
      "enabled": true,
      "minRiftLevel": 20,
      "maxRiftLevel": 24,
      "minWave": 20,
      "maxWave": 24,
      "successOnly": false,
      "checkpointOnly": false,
      "classicOnly": false,
      "modes": [ "boss-gauntlet" ],
      "contentIds": [],
      "bossSourceIds": [],
      "tables": [
        {
          "id": "cosmic-artifacts-wave-20",
          "lootTable": "cosmic-artifacts",
          "chancePercent": 45,
          "rolls": 1,
          "delivery": "ground"
        },
        {
          "id": "omega-file-box-100",
          "lootTable": "omega-file-box-100",
          "chancePercent": 100,
          "rolls": 1,
          "delivery": "ground"
        }
      ]
    },
    {
      "id": "boss-gauntlet-wave-30-jackpot",
      "enabled": true,
      "minRiftLevel": 30,
      "maxRiftLevel": 39,
      "minWave": 30,
      "maxWave": 39,
      "successOnly": false,
      "checkpointOnly": false,
      "classicOnly": false,
      "modes": [ "boss-gauntlet" ],
      "contentIds": [],
      "bossSourceIds": [],
      "tables": [
        {
          "id": "cosmic-artifacts-wave-30",
          "lootTable": "cosmic-artifacts",
          "chancePercent": 100,
          "rolls": 1,
          "delivery": "ground"
        },
        {
          "id": "offense-defense-ring-i69",
          "lootTable": "offense-defense-ring",
          "chancePercent": 65,
          "rolls": 1,
          "delivery": "ground",
          "itemLevel": 69
        },
        {
          "id": "ultimate-upgrade-token",
          "lootTable": "ultimate-upgrade-token",
          "chancePercent": 8,
          "rolls": 1,
          "delivery": "ground"
        }
      ]
    },
    {
      "id": "boss-gauntlet-wave-40-plus-jackpot",
      "enabled": true,
      "minRiftLevel": 40,
      "maxRiftLevel": 0,
      "minWave": 40,
      "maxWave": 0,
      "successOnly": false,
      "checkpointOnly": false,
      "classicOnly": false,
      "modes": [ "boss-gauntlet" ],
      "contentIds": [],
      "bossSourceIds": [],
      "tables": [
        {
          "id": "cosmic-artifacts-wave-40-plus",
          "lootTable": "cosmic-artifacts",
          "chancePercent": 100,
          "rolls": 2,
          "delivery": "ground"
        },
        {
          "id": "offense-defense-ring-i69",
          "lootTable": "offense-defense-ring",
          "chancePercent": 100,
          "rolls": 1,
          "delivery": "ground",
          "itemLevel": 69
        },
        {
          "id": "ultimate-upgrade-token",
          "lootTable": "ultimate-upgrade-token",
          "chancePercent": 15,
          "rolls": 1,
          "delivery": "ground"
        }
      ]
    }
  ]
}
```

## Vendor Example

Rift Sigils should make bad luck tolerable, not replace the drops. Suggested vendor stock:

| Cost | Offer |
| ---: | --- |
| 20 | Cube Shard Relief Cache |
| 35 | Mysterious Unique Box |
| 50 | I69 Unique Cache |
| 65 | Mysterious Artifact Box |
| 90 | I69 Artifact Cache |
| 125 | I69 Ring Cache |
| 150 | Cosmic Artifact Cache |
| 200 | Ultimate Upgrade Token or rare chase cache |

This gives every run a reason to matter without making Rifts the only correct activity.

## Important Follow-Ups

The current reward JSON can express most of this, but three improvements would make the system much easier to tune:

1. Add daily or weekly reward locks to Rift reward recipes, similar to how the Dino event avoids unlimited guaranteed payout abuse.
2. Add a reward preview command that resolves the JSON for `mode + level/wave` and prints the exact tables/items that would roll.
3. Add vendor stock for real cache targets instead of selling only scenario items.

