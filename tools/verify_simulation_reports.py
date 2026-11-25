"""Independently check exported run logs, CSV hashes and summary accounting (no Unity)."""
import argparse
import csv
import hashlib
import json
import struct
from pathlib import Path


def impact_magnitude(impact):
    # ExperienceImpact.Magnitude is evaluated as C# float32; preserve its tie ordering.
    def f32(value):
        return struct.unpack("f", struct.pack("f", value))[0]
    return f32(f32(abs(impact["stress"]) + abs(impact["rebellion"])) + abs(impact["trust"]))


def verify(folder: Path):
    summary = json.loads((folder / "summary.json").read_text(encoding="utf-8-sig"))
    assert not summary["violations"], summary["violations"]
    assert summary["runs"] >= 1000
    assert summary["sceneLoads"] == 0
    assert summary["replayMatches"] == summary["runs"]
    frequencies = [0] * 14
    records = {}
    passive_love = costumes = 0
    with (folder / "runs.csv").open(encoding="utf-8-sig", newline="") as csv_file, (folder / "runs.jsonl").open("rb") as logs:
        rows = csv.DictReader(csv_file)
        total = 0
        for raw in logs:
            row = next(rows)
            run = json.loads(raw)
            digest = hashlib.sha256(raw.rstrip(b"\r\n")).hexdigest()
            assert digest == row["hash"], (row["policy"], row["seed"], "log hash mismatch")
            assert (run["policy"], run["seed"], run["ending"]) == (row["policy"], int(row["seed"]), int(row["ending"]))
            assert not run["violations"]
            state = run["state"]
            assert all(0 <= v <= 100 for v in state["statValues"] + [state["trust"]])
            entries = state["eventLog"]
            visits = [e for e in entries if e["eventType"] == "item_first"]
            ids = [(e["roomId"], e["experience"]["itemId"]) for e in visits]
            assert len(ids) == len(set(ids)), "duplicate first visit"
            room_entries = {e["roomId"]: e["timestamp"] for e in entries if e["eventType"] == "room_enter"}
            assert len(room_entries) == 4
            for e in entries:
                if e["eventType"] == "contact":
                    assert e["timestamp"] - room_entries[e["roomId"]] >= 90, "room observation cooldown"
            impacts = [e["impact"] for e in entries if e["eventType"] == "experience_impact"]
            top = sorted(impacts, key=lambda i: -impact_magnitude(i))[:3]
            assert run["decisiveExperiences"] == top, "ending card does not match actual impact log"
            snapshot = state["finalYoungAdultSnapshot"]
            assert snapshot["captured"]
            gambled = any(item == "youngadult_gambling" for _, item in ids)
            assert (run["ending"] == 1) == gambled == snapshot["gamblingBankruptcy"]
            frequencies[run["ending"] - 1] += 1
            passive_love += int(run["policy"] == "passive" and snapshot["love"] == 100)
            costumes += int(state["adolescenceHiddenCostume"])
            records[(run["policy"], run["seed"], run["ending"])] = digest
            total += 1
        assert next(rows, None) is None, "CSV contains extra rows"
    assert total == summary["runs"]
    assert frequencies == summary["endingCounts"] and all(frequencies)
    assert passive_love == summary["passiveLove100"] and passive_love > 0
    assert costumes == summary["hiddenCostumes"] and costumes > 0
    targeted = set()
    for witness in summary["witnesses"]:
        assert records[(witness["policy"], witness["seed"], witness["ending"])] == witness["hash"]
        if witness["policy"] == f'target_{witness["ending"]:02}':
            targeted.add(witness["ending"])
    assert targeted == set(range(1, 15)), targeted
    result = {"passed": True, "runs": total, "endingCounts": frequencies, "targetedEndings": len(targeted),
              "passiveLove100": passive_love, "hiddenCostumes": costumes,
              "checks": ["raw JSON SHA256 matches CSV", "bounds", "first-visit uniqueness", "90-second input cooldown",
                         "decisive experiences sorted from actual log", "gambling provenance and priority", "all targeted witnesses replayed", "summary accounting"]}
    (folder / "verification.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("folder", type=Path, nargs="?", default=Path("SimulationReports"))
    print(json.dumps(verify(parser.parse_args().folder), ensure_ascii=False, indent=2))
