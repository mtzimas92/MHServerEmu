#!/usr/bin/env python3
"""Build a simple damage dashboard from MHServerEmu power damage JSONL logs."""

from __future__ import annotations

import argparse
import csv
import glob
import html
import json
from collections import defaultdict
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


def parse_time(value: str) -> datetime:
    if not value:
        return datetime.now(timezone.utc)

    if value.endswith("Z"):
        value = value[:-1] + "+00:00"

    dt = datetime.fromisoformat(value)
    if dt.tzinfo is None:
        dt = dt.replace(tzinfo=timezone.utc)
    return dt.astimezone(timezone.utc)


def number(value: Any) -> float:
    try:
        return float(value)
    except (TypeError, ValueError):
        return 0.0


def pct_change(before: float, after: float) -> float:
    if before == 0.0:
        return 0.0 if after == 0.0 else 1.0
    return (after - before) / before


@dataclass
class PowerStats:
    player: str
    avatar: str
    power: str
    hits: int = 0
    crits: int = 0
    super_crits: int = 0
    blocked: int = 0
    resisted: int = 0
    over_time: int = 0
    proc: int = 0
    actual_damage: float = 0.0
    raw_server_damage: float = 0.0
    client_damage: float = 0.0
    physical: float = 0.0
    energy: float = 0.0
    mental: float = 0.0
    first: datetime | None = None
    last: datetime | None = None
    targets: dict[str, int] = field(default_factory=lambda: defaultdict(int))

    def add(self, event: dict[str, Any]) -> None:
        ts = parse_time(event.get("TimestampUtc", ""))
        self.first = ts if self.first is None or ts < self.first else self.first
        self.last = ts if self.last is None or ts > self.last else self.last

        self.hits += 1
        self.crits += int(bool(event.get("Critical")))
        self.super_crits += int(bool(event.get("SuperCritical")))
        self.blocked += int(bool(event.get("Blocked")))
        self.resisted += int(bool(event.get("Resisted")))
        self.over_time += int(bool(event.get("OverTime")))
        self.proc += int(bool(event.get("Proc")))

        self.actual_damage += number(event.get("ActualHealthDamage"))
        self.raw_server_damage += number(event.get("RawServerDamage"))
        self.client_damage += number(event.get("ClientDisplayDamage"))
        self.physical += number(event.get("RawPhysical"))
        self.energy += number(event.get("RawEnergy"))
        self.mental += number(event.get("RawMental"))

        target = event.get("TargetPrototype") or "unknown"
        self.targets[target] += 1

    @property
    def duration_seconds(self) -> float:
        if self.first is None or self.last is None:
            return 1.0
        return max((self.last - self.first).total_seconds(), 1.0)

    @property
    def actual_dps(self) -> float:
        return self.actual_damage / self.duration_seconds

    @property
    def raw_dps(self) -> float:
        return self.raw_server_damage / self.duration_seconds

    @property
    def crit_rate(self) -> float:
        return (self.crits + self.super_crits) / self.hits if self.hits else 0.0


@dataclass
class CompareStats:
    player: str
    avatar: str
    power: str
    before: PowerStats | None
    after: PowerStats | None

    @property
    def before_actual(self) -> float:
        return self.before.actual_damage if self.before else 0.0

    @property
    def after_actual(self) -> float:
        return self.after.actual_damage if self.after else 0.0

    @property
    def before_dps(self) -> float:
        return self.before.actual_dps if self.before else 0.0

    @property
    def after_dps(self) -> float:
        return self.after.actual_dps if self.after else 0.0

    @property
    def delta_actual(self) -> float:
        return self.after_actual - self.before_actual

    @property
    def delta_dps(self) -> float:
        return self.after_dps - self.before_dps

    @property
    def delta_actual_pct(self) -> float:
        return pct_change(self.before_actual, self.after_actual)

    @property
    def delta_dps_pct(self) -> float:
        return pct_change(self.before_dps, self.after_dps)


def expand_log_paths(patterns: list[str]) -> list[Path]:
    paths: list[Path] = []
    for pattern in patterns:
        matches = glob.glob(pattern)
        if matches:
            paths.extend(Path(match) for match in matches)
        else:
            paths.append(Path(pattern))

    missing = [path for path in paths if path.exists() is False]
    if missing:
        raise SystemExit("Missing log file(s): " + ", ".join(str(path) for path in missing))

    return paths


def load_events(paths: list[Path]) -> list[dict[str, Any]]:
    events: list[dict[str, Any]] = []
    for path in paths:
        with path.open("r", encoding="utf-8") as handle:
            for line_number, line in enumerate(handle, start=1):
                line = line.strip()
                if not line:
                    continue

                try:
                    event = json.loads(line)
                except json.JSONDecodeError as exc:
                    raise SystemExit(f"{path}:{line_number}: invalid JSON: {exc}") from exc

                if event.get("Schema") == "mh_power_damage_v1" and event.get("EventType") == "damage":
                    events.append(event)

    return events


def aggregate(events: list[dict[str, Any]]) -> list[PowerStats]:
    stats_by_key: dict[tuple[str, str, str], PowerStats] = {}
    for event in events:
        player = event.get("PlayerName") or "unknown"
        avatar = event.get("AvatarPrototype") or "unknown"
        power = event.get("PowerPrototype") or "unknown"
        key = (player, avatar, power)
        if key not in stats_by_key:
            stats_by_key[key] = PowerStats(player, avatar, power)
        stats_by_key[key].add(event)

    return sorted(stats_by_key.values(), key=lambda stat: stat.actual_damage, reverse=True)


def stats_key(row: PowerStats) -> tuple[str, str, str]:
    return (row.player, row.avatar, row.power)


def compare_aggregates(before_rows: list[PowerStats], after_rows: list[PowerStats]) -> list[CompareStats]:
    before_map = {stats_key(row): row for row in before_rows}
    after_map = {stats_key(row): row for row in after_rows}
    keys = set(before_map.keys()) | set(after_map.keys())

    rows = [
        CompareStats(key[0], key[1], key[2], before_map.get(key), after_map.get(key))
        for key in keys
    ]
    return sorted(rows, key=lambda row: abs(row.delta_actual), reverse=True)


def write_csv(path: Path, rows: list[PowerStats]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle)
        writer.writerow([
            "player",
            "avatar",
            "power",
            "hits",
            "actual_damage",
            "actual_dps",
            "raw_server_damage",
            "raw_dps",
            "client_display_damage",
            "crit_rate",
            "blocked_hits",
            "resisted_hits",
            "dot_hits",
            "proc_hits",
            "physical",
            "energy",
            "mental",
            "top_target",
        ])
        for row in rows:
            top_target = max(row.targets.items(), key=lambda item: item[1])[0] if row.targets else ""
            writer.writerow([
                row.player,
                row.avatar,
                row.power,
                row.hits,
                round(row.actual_damage, 2),
                round(row.actual_dps, 2),
                round(row.raw_server_damage, 2),
                round(row.raw_dps, 2),
                round(row.client_damage, 2),
                round(row.crit_rate, 4),
                row.blocked,
                row.resisted,
                row.over_time,
                row.proc,
                round(row.physical, 2),
                round(row.energy, 2),
                round(row.mental, 2),
                top_target,
            ])


def write_compare_csv(path: Path, rows: list[CompareStats]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle)
        writer.writerow([
            "player",
            "avatar",
            "power",
            "before_actual_damage",
            "after_actual_damage",
            "delta_actual_damage",
            "delta_actual_pct",
            "before_actual_dps",
            "after_actual_dps",
            "delta_actual_dps",
            "delta_actual_dps_pct",
            "before_hits",
            "after_hits",
            "before_crit_rate",
            "after_crit_rate",
        ])
        for row in rows:
            writer.writerow([
                row.player,
                row.avatar,
                row.power,
                round(row.before_actual, 2),
                round(row.after_actual, 2),
                round(row.delta_actual, 2),
                round(row.delta_actual_pct, 4),
                round(row.before_dps, 2),
                round(row.after_dps, 2),
                round(row.delta_dps, 2),
                round(row.delta_dps_pct, 4),
                row.before.hits if row.before else 0,
                row.after.hits if row.after else 0,
                round(row.before.crit_rate, 4) if row.before else 0.0,
                round(row.after.crit_rate, 4) if row.after else 0.0,
            ])


def write_html(path: Path, rows: list[PowerStats], top: int) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    shown = rows[:top]
    max_damage = max((row.actual_damage for row in shown), default=1.0)
    body_rows = []
    for row in shown:
        width = max(1.0, row.actual_damage / max_damage * 100.0)
        body_rows.append(
            "<tr>"
            f"<td>{html.escape(row.player)}</td>"
            f"<td>{html.escape(short_name(row.avatar))}</td>"
            f"<td>{html.escape(short_name(row.power))}</td>"
            f"<td>{row.hits}</td>"
            f"<td>{row.actual_damage:,.0f}</td>"
            f"<td>{row.actual_dps:,.0f}</td>"
            f"<td>{row.raw_server_damage:,.0f}</td>"
            f"<td>{row.client_damage:,.0f}</td>"
            f"<td>{row.crit_rate:.1%}</td>"
            f"<td><div class='bar'><span style='width:{width:.1f}%'></span></div></td>"
            "</tr>"
        )

    content = f"""<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>Power Damage Report</title>
<style>
body {{ font-family: Segoe UI, Arial, sans-serif; margin: 24px; background: #101316; color: #eceff1; }}
h1 {{ font-size: 22px; margin: 0 0 16px; }}
table {{ border-collapse: collapse; width: 100%; font-size: 13px; }}
th, td {{ border-bottom: 1px solid #2c343a; padding: 7px 8px; text-align: left; vertical-align: middle; }}
th {{ color: #9db3c3; font-weight: 600; position: sticky; top: 0; background: #101316; }}
.bar {{ width: 180px; height: 8px; background: #29333a; }}
.bar span {{ display: block; height: 8px; background: #58a6ff; }}
.muted {{ color: #9db3c3; margin-bottom: 18px; }}
</style>
</head>
<body>
<h1>Power Damage Report</h1>
<div class="muted">{len(rows)} powers aggregated. Showing top {len(shown)} by actual health damage.</div>
<table>
<thead><tr><th>Player</th><th>Avatar</th><th>Power</th><th>Hits</th><th>Actual Damage</th><th>Actual DPS</th><th>Raw Server</th><th>Client Display</th><th>Crit</th><th></th></tr></thead>
<tbody>
{''.join(body_rows)}
</tbody>
</table>
</body>
</html>
"""
    path.write_text(content, encoding="utf-8")


def write_compare_html(path: Path, rows: list[CompareStats], top: int) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    shown = rows[:top]
    body_rows = []
    for row in shown:
        delta_class = "up" if row.delta_actual >= 0 else "down"
        body_rows.append(
            "<tr>"
            f"<td>{html.escape(row.player)}</td>"
            f"<td>{html.escape(short_name(row.avatar))}</td>"
            f"<td>{html.escape(short_name(row.power))}</td>"
            f"<td>{row.before_actual:,.0f}</td>"
            f"<td>{row.after_actual:,.0f}</td>"
            f"<td class='{delta_class}'>{row.delta_actual:+,.0f}</td>"
            f"<td class='{delta_class}'>{row.delta_actual_pct:+.1%}</td>"
            f"<td>{row.before_dps:,.0f}</td>"
            f"<td>{row.after_dps:,.0f}</td>"
            f"<td class='{delta_class}'>{row.delta_dps:+,.0f}</td>"
            f"<td>{row.before.hits if row.before else 0}</td>"
            f"<td>{row.after.hits if row.after else 0}</td>"
            "</tr>"
        )

    content = f"""<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>Power Damage Compare</title>
<style>
body {{ font-family: Segoe UI, Arial, sans-serif; margin: 24px; background: #101316; color: #eceff1; }}
h1 {{ font-size: 22px; margin: 0 0 16px; }}
table {{ border-collapse: collapse; width: 100%; font-size: 13px; }}
th, td {{ border-bottom: 1px solid #2c343a; padding: 7px 8px; text-align: left; vertical-align: middle; }}
th {{ color: #9db3c3; font-weight: 600; position: sticky; top: 0; background: #101316; }}
.muted {{ color: #9db3c3; margin-bottom: 18px; }}
.up {{ color: #7ee787; }}
.down {{ color: #ff7b72; }}
</style>
</head>
<body>
<h1>Power Damage Compare</h1>
<div class="muted">{len(rows)} powers compared. Showing top {len(shown)} by absolute actual-damage delta.</div>
<table>
<thead><tr><th>Player</th><th>Avatar</th><th>Power</th><th>Before Damage</th><th>After Damage</th><th>Damage Delta</th><th>Damage %</th><th>Before DPS</th><th>After DPS</th><th>DPS Delta</th><th>Before Hits</th><th>After Hits</th></tr></thead>
<tbody>
{''.join(body_rows)}
</tbody>
</table>
</body>
</html>
"""
    path.write_text(content, encoding="utf-8")


def short_name(path: str) -> str:
    if not path:
        return ""
    name = path.rsplit("/", 1)[-1]
    return name.removesuffix(".prototype")


def print_summary(rows: list[PowerStats], top: int) -> None:
    print("player,avatar,power,hits,actual_damage,actual_dps,raw_server_damage,client_display_damage,crit_rate")
    for row in rows[:top]:
        print(
            f"{row.player},{short_name(row.avatar)},{short_name(row.power)},"
            f"{row.hits},{row.actual_damage:.0f},{row.actual_dps:.0f},"
            f"{row.raw_server_damage:.0f},{row.client_damage:.0f},{row.crit_rate:.2%}"
        )


def print_compare_summary(rows: list[CompareStats], top: int) -> None:
    print("player,avatar,power,before_damage,after_damage,delta_damage,delta_damage_pct,before_dps,after_dps,delta_dps")
    for row in rows[:top]:
        print(
            f"{row.player},{short_name(row.avatar)},{short_name(row.power)},"
            f"{row.before_actual:.0f},{row.after_actual:.0f},{row.delta_actual:+.0f},"
            f"{row.delta_actual_pct:+.2%},{row.before_dps:.0f},{row.after_dps:.0f},{row.delta_dps:+.0f}"
        )


def main() -> None:
    parser = argparse.ArgumentParser(description="Aggregate MHServerEmu power damage JSONL logs.")
    parser.add_argument("logs", nargs="*", help="PowerDamage_*.jsonl file(s)")
    parser.add_argument("--top", type=int, default=50, help="Number of powers to show in console/HTML")
    parser.add_argument("--csv", type=Path, help="Optional CSV output path")
    parser.add_argument("--html", type=Path, help="Optional HTML dashboard output path")
    parser.add_argument("--compare-before", nargs="+", help="Baseline PowerDamage JSONL file(s) or glob(s)")
    parser.add_argument("--compare-after", nargs="+", help="Candidate PowerDamage JSONL file(s) or glob(s)")
    args = parser.parse_args()

    if args.compare_before or args.compare_after:
        if not args.compare_before or not args.compare_after:
            raise SystemExit("--compare-before and --compare-after must be used together.")

        before_rows = aggregate(load_events(expand_log_paths(args.compare_before)))
        after_rows = aggregate(load_events(expand_log_paths(args.compare_after)))
        compare_rows = compare_aggregates(before_rows, after_rows)
        print_compare_summary(compare_rows, args.top)

        if args.csv:
            write_compare_csv(args.csv, compare_rows)
            print(f"\nCompare CSV written: {args.csv}")

        if args.html:
            write_compare_html(args.html, compare_rows, args.top)
            print(f"Compare HTML written: {args.html}")

        return

    if not args.logs:
        raise SystemExit("Provide log file(s), or use --compare-before and --compare-after.")

    events = load_events(expand_log_paths(args.logs))
    rows = aggregate(events)
    print_summary(rows, args.top)

    if args.csv:
        write_csv(args.csv, rows)
        print(f"\nCSV written: {args.csv}")

    if args.html:
        write_html(args.html, rows, args.top)
        print(f"HTML written: {args.html}")


if __name__ == "__main__":
    main()
