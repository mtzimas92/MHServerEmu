# Power Damage Metrics

Server-side power damage profiling is controlled by the `damageprofile` admin command. It writes JSON Lines files to:

```text
Logs/PowerDamage/PowerDamage_<timestamp>_<label>.jsonl
```

## Commands

```text
!damageprofile start <label>
!damageprofile mark <label>
!damageprofile status
!damageprofile stop
```

The logger records only hostile damage caused by player-owned avatars, including pets and summons through the ultimate owner. It is off by default.

## Damage Fields

Each damage event includes:

- `ActualHealthDamage`: real server health removed after final health clamp. This is best for true encounter DPS.
- `RawServerDamage`: final server-calculated damage before overkill clamp, split into `RawPhysical`, `RawEnergy`, and `RawMental`.
- `ClientDisplayDamage`: the damage number sent to the client, split into `ClientPhysical`, `ClientEnergy`, and `ClientMental`.

The client display value can differ from actual server damage because `PowerPayload.CalculateResultDamageLevelScaling()` adjusts numbers shown to the client for difficulty and level scaling presentation.

## Report Script

Generate a console summary:

```bash
python tools/power-damage-report.py Logs/PowerDamage/PowerDamage_*.jsonl
```

Generate CSV and HTML:

```bash
python tools/power-damage-report.py Logs/PowerDamage/PowerDamage_20260731_203000_test.jsonl --csv damage.csv --html damage.html
```

The report groups by player, avatar, and power, then shows hits, actual damage, actual DPS, raw server damage, client display damage, and crit rate.
