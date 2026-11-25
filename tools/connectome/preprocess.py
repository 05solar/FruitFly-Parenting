"""Pinned FAFB v783 -> tiny directed paths. No Unity or third-party dependency."""
import argparse
import csv
import gzip
import hashlib
import json
import math
import urllib.request
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
BASE = "https://storage.googleapis.com/flywire-data/codex/data/fafb/783/"
PINS = {
    "classification.csv.gz": "e946b552f4056dfc977707be0674609832c3f64332a22d69dc0d9615e7aae663",
    "connections.csv.gz": "d49dd692e59e153aa3c83f5257bfc0eff51247b86d7bb183386c6d1622c70fc9",
}
LABELS = ("ORN", "ALPN", "KC", "MBON", "Descending")
ATTRIBUTION = (
    "Derived from FlyWire FAFB v783 connectivity and classification data. "
    "Dorkenwald, S. et al., Neuronal wiring diagram of an adult brain, Nature 634, 124-138 (2024), "
    "https://doi.org/10.1038/s41586-024-07558-y. "
    "Schlegel, P. et al., Whole-brain annotation and multi-connectome cell typing of Drosophila, "
    "Nature 634, 139-152 (2024), https://doi.org/10.1038/s41586-024-07686-5. "
    "Licensed under CC BY-NC 4.0, https://creativecommons.org/licenses/by-nc/4.0/. "
    "Changes: deterministic pathway selection, regional summation, class aggregation, "
    "logarithmic visual scaling and authored layout. No endorsement by the authors is implied."
)


def digest(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def checked_source(raw_dir, name, download=False):
    path = raw_dir / name
    if not path.exists() and download:
        raw_dir.mkdir(parents=True, exist_ok=True)
        temporary = path.with_suffix(".partial")
        try:
            urllib.request.urlretrieve(BASE + name, temporary)
            if digest(temporary) != PINS[name]:
                raise ValueError("Source checksum changed: " + name)
            temporary.replace(path)
        finally:
            temporary.unlink(missing_ok=True)
    if not path.exists() or digest(path) != PINS[name]:
        raise ValueError("Missing or unpinned source: " + str(path))
    return path


def read_csv(path, required):
    with gzip.open(path, "rt", encoding="utf-8-sig", newline="") as stream:
        reader = csv.DictReader(stream)
        if not set(required).issubset(reader.fieldnames or []):
            raise ValueError("Missing CSV columns: " + str(path))
        yield from reader


def node_stage(row):
    if row["super_class"] == "sensory" and row["class"] == "olfactory":
        return 0
    return {"ALPN": 1, "Kenyon_Cell": 2, "MBON": 3}.get(
        row["class"], 4 if row["super_class"] == "descending" else -1)


def extract(classifications, connections, path_count=6):
    nodes, seen = {}, set()
    for row in classifications:
        key = row["root_id"]
        if not key.isdecimal() or key in seen:
            raise ValueError("Invalid or duplicate root ID: " + key)
        seen.add(key)
        stage = node_stage(row)
        if stage >= 0:
            nodes[key] = dict(id=key, label=LABELS[stage], stage=stage,
                              side=row["side"], members=[key])
    regions = defaultdict(lambda: defaultdict(int))
    for row in connections:
        value = row["syn_count"]
        if not value.isdecimal():
            raise ValueError("Non-integer or negative synapse count: " + value)
        a, b = row["pre_root_id"], row["post_root_id"]
        if a in nodes and b in nodes and nodes[b]["stage"] == nodes[a]["stage"] + 1:
            regions[a, b][row["neuropil"]] += int(value)
    weights = {pair: sum(r.values()) for pair, r in regions.items() if sum(r.values()) >= 5}
    # DAG dynamic programming; compare complete ID paths to settle ties reproducibly.
    best = {key: (math.inf, (key,)) for key, node in nodes.items() if node["stage"] == 0}
    for stage in range(1, 5):
        for (a, b), weight in sorted(weights.items()):
            if nodes[b]["stage"] != stage or a not in best:
                continue
            score, path = best[a]
            candidate = min(score, weight), path + (b,)
            if (b not in best or candidate[0] > best[b][0]
                    or candidate[0] == best[b][0] and candidate[1] < best[b][1]):
                best[b] = candidate
    # A downstream bottleneck can make previously unequal paths tie. Reconstruct
    # lexicographically first paths at each final threshold, rather than retaining
    # only the locally strongest prefix when resolving these global ties.
    endpoints = {k: v[0] for k, v in best.items() if nodes[k]["stage"] == 4}
    canonical = {}
    for threshold in sorted(set(endpoints.values())):
        paths = {k: (k,) for k, n in nodes.items() if n["stage"] == 0}
        for stage in range(1, 5):
            for (a, b), weight in sorted(weights.items()):
                if nodes[b]["stage"] != stage or weight < threshold or a not in paths:
                    continue
                candidate = paths[a] + (b,)
                if b not in paths or candidate < paths[b]:
                    paths[b] = candidate
        for endpoint, score in endpoints.items():
            if score == threshold:
                canonical[endpoint] = (score, paths[endpoint])
    ranked = sorted(canonical.values(), key=lambda v: (-v[0], v[1]))
    if len(ranked) < path_count:
        raise ValueError("Not enough complete ORN -> descending paths")
    paths = [dict(bottleneck=score, ids=list(path)) for score, path in ranked[:path_count]]
    ids = {key for path in paths for key in path["ids"]}
    pairs = {pair for path in paths for pair in zip(path["ids"], path["ids"][1:])}
    edges = [make_edge(a, b, regions[a, b]) for a, b in sorted(pairs)]
    scale_edges(edges)
    return dict(nodes=sorted((nodes[key] for key in ids), key=lambda n: (n["stage"], n["id"])),
                edges=edges, paths=paths)


def make_edge(a, b, regions):
    return dict(source=a, target=b, synapses=sum(regions.values()),
                regions=[dict(name=k, synapses=v) for k, v in sorted(regions.items())])


def scale_edges(edges):
    maximum = max(e["synapses"] for e in edges)
    for edge in edges:
        edge["visualWeight"] = round(math.log1p(edge["synapses"]) / math.log1p(maximum), 6)


def motif(graph):
    nodes = [dict(id=str(stage), label=LABELS[stage], stage=stage, side="aggregate",
                  members=sorted(n["id"] for n in graph["nodes"] if n["stage"] == stage))
             for stage in range(5)]
    stages = {n["id"]: str(n["stage"]) for n in graph["nodes"]}
    regions = defaultdict(lambda: defaultdict(int))
    for edge in graph["edges"]:
        pair = stages[edge["source"]], stages[edge["target"]]
        for r in edge["regions"]:
            regions[pair][r["name"]] += r["synapses"]
    edges = [make_edge(a, b, r) for (a, b), r in sorted(regions.items())]
    scale_edges(edges)
    return dict(nodes=nodes, edges=edges, paths=[])


def write_json(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes((json.dumps(data, ensure_ascii=False, indent=2) + "\n").encode("utf-8"))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--raw-dir", type=Path, default=ROOT / "Logs/ConnectomeRaw")
    parser.add_argument("--output-dir", type=Path, default=ROOT / "Assets/_Project/Experimental/Connectome/Data")
    parser.add_argument("--report", type=Path, default=ROOT / "docs/reports/step13-data.json")
    parser.add_argument("--download", action="store_true")
    args = parser.parse_args()
    # Check both complete files BEFORE any output is overwritten.
    files = {name: checked_source(args.raw_dir, name, args.download) for name in PINS}
    graph = extract(read_csv(files["classification.csv.gz"], ("root_id", "super_class", "class", "side")),
                    read_csv(files["connections.csv.gz"], ("pre_root_id", "post_root_id", "neuropil", "syn_count")))
    metadata = dict(schemaVersion=1, dataset="FlyWire FAFB v783", license="CC BY-NC 4.0",
                    attribution=ATTRIBUTION, sources=[dict(name=n, url=BASE+n, sha256=h) for n, h in PINS.items()])
    outputs = []
    for name, content, mode in [("connectome-v783.json", graph, "neurons"),
                                ("memory-motif-v783.json", motif(graph), "motif")]:
        path = args.output_dir / name
        write_json(path, dict(**metadata, mode=mode, **content))
        outputs.append(dict(file=name, sha256=digest(path), bytes=path.stat().st_size,
                            nodes=len(content["nodes"]), edges=len(content["edges"])))
    write_json(args.report, dict(dataset=metadata["dataset"], sources=metadata["sources"], outputs=outputs,
                                 paths=graph["paths"]))
    print(json.dumps(outputs, indent=2))


if __name__ == "__main__":
    main()
