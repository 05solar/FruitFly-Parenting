"""Summarize real development-APK CSVs. Missing counters remain unknown, never zero."""
import argparse
import csv
import json
import math
from collections import defaultdict
from pathlib import Path


def summarize(rows):
    rooms = defaultdict(list)
    for row in rows:
        rooms[row.get("room") or "loading"].append(row)
    result = {}
    for room, samples in rooms.items():
        summary = {"samples": len(samples)}
        for field in ("cpu_ms", "gpu_ms", "draw_calls", "triangles", "memory_bytes", "gc_bytes", "worst_frame_ms", "refresh_hz"):
            values = []
            for sample in samples:
                try:
                    value = float(sample.get(field, ""))
                except (ValueError, TypeError):
                    continue
                if math.isfinite(value) and value >= 0:
                    values.append(value)
            values.sort()
            summary[field] = {"count": len(values), "p95_sample": values[math.ceil(.95 * len(values)) - 1], "max": values[-1]} if values else None
        summary["frames"] = sum(int(s.get("window_frames") or 0) for s in samples)
        summary["over_budget_frames"] = sum(int(s.get("over_budget_frames") or 0) for s in samples)
        cpu, gpu = summary["cpu_ms"], summary["gpu_ms"]
        summary["bound_hint"] = "unknown"
        if cpu and gpu:
            delta = cpu["p95_sample"] - gpu["p95_sample"]
            summary["bound_hint"] = "CPU" if delta > 1 else "GPU" if delta < -1 else "mixed"
        result[room] = summary
    return {"source": "device CSV (0.5s samples; frame budget counts cover frames between samples)",
            "caveat": "Bound hints need a Unity Profiler/OVR Metrics trace. P95 samples are not all-frame timing percentiles. Exclude loading/warm-up windows when comparing steady-state budgets.",
            "rooms": result}


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("csv", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    with args.csv.open(encoding="utf-8", newline="") as source:
        result = summarize(csv.DictReader(source))
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
