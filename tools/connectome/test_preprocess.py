import copy
import gzip
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import preprocess as p


class PreprocessingTests(unittest.TestCase):
    # Synthetic test fixture only; never shipped as visualization data.
    def setUp(self):
        self.nodes = [dict(root_id=str(i), super_class=s, **{"class": c}, side="left")
                      for i, (s, c) in enumerate([
                          ("sensory", "olfactory"), ("central", "ALPN"),
                          ("central", "Kenyon_Cell"), ("central", "MBON"), ("descending", "")])]
        self.edges = [dict(pre_root_id=str(i), post_root_id=str(i+1), neuropil="A", syn_count="9")
                      for i in range(4)]

    def test_regional_sum_precedes_threshold_and_preserves_counts(self):
        self.edges[0]["syn_count"] = "3"
        self.edges.append(dict(self.edges[0], neuropil="B", syn_count="2"))
        graph = p.extract(self.nodes, self.edges, 1)
        self.assertEqual(5, graph["edges"][0]["synapses"])
        self.assertEqual([3, 2], [r["synapses"] for r in graph["edges"][0]["regions"]])
        self.assertEqual(5, graph["paths"][0]["bottleneck"])

    def test_disconnected_class_members_do_not_create_a_path(self):
        self.nodes.append(dict(self.nodes[2], root_id="99"))
        self.edges[1]["post_root_id"] = "99"
        with self.assertRaisesRegex(ValueError, "complete"):
            p.extract(self.nodes, self.edges, 1)

    def test_ties_and_input_order_are_deterministic(self):
        self.nodes.append(dict(self.nodes[1], root_id="11"))
        self.edges.extend([dict(self.edges[0], post_root_id="11"), dict(self.edges[1], pre_root_id="11")])
        a = p.extract(self.nodes, self.edges, 1)
        b = p.extract(self.nodes[::-1], self.edges[::-1], 1)
        self.assertEqual(a, b)
        self.assertEqual("1", a["paths"][0]["ids"][1])

    def test_bottleneck_beats_larger_total_weight(self):
        self.nodes.append(dict(self.nodes[1], root_id="11"))
        self.edges[0]["syn_count"] = "100"
        self.edges[1]["syn_count"] = "5"
        self.edges.extend([dict(self.edges[0], post_root_id="11", syn_count="8"),
                           dict(self.edges[1], pre_root_id="11", syn_count="8")])
        self.assertEqual("11", p.extract(self.nodes, self.edges, 1)["paths"][0]["ids"][1])

    def test_motif_conserves_synapses_and_members(self):
        graph = p.extract(self.nodes, self.edges, 1)
        motif = p.motif(graph)
        self.assertEqual(sum(e["synapses"] for e in graph["edges"]), sum(e["synapses"] for e in motif["edges"]))
        self.assertEqual(5, sum(len(n["members"]) for n in motif["nodes"]))
        self.assertTrue(all(0 < e["visualWeight"] <= 1 for e in motif["edges"]))

    def test_downstream_bottleneck_resolves_tie_across_weaker_prefixes(self):
        self.nodes.append(dict(self.nodes[1], root_id="11"))
        self.edges[0]["syn_count"] = "8"
        self.edges[3]["syn_count"] = "5"
        self.edges.extend([dict(self.edges[0], post_root_id="11", syn_count="20"),
                           dict(self.edges[1], pre_root_id="11", syn_count="20")])
        # Both complete routes have bottleneck 5; lexicographic '1' must win.
        self.assertEqual("1", p.extract(self.nodes, self.edges, 1)["paths"][0]["ids"][1])

    def test_duplicate_ids_and_bad_counts_rejected(self):
        with self.assertRaisesRegex(ValueError, "duplicate"):
            p.extract(self.nodes + [self.nodes[0]], self.edges, 1)
        for value in ("-1", "2.5", "", "NaN"):
            edges = copy.deepcopy(self.edges)
            edges[0]["syn_count"] = value
            with self.subTest(value=value), self.assertRaises(ValueError):
                p.extract(self.nodes, edges, 1)

    def test_unpinned_input_rejected_without_network(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "classification.csv.gz"
            path.write_bytes(b"not pinned")
            with patch("urllib.request.urlretrieve") as network:
                with self.assertRaisesRegex(ValueError, "unpinned"):
                    p.checked_source(path.parent, path.name)
                network.assert_not_called()

    def test_missing_columns_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "bad.csv.gz"
            with gzip.open(path, "wt") as stream:
                stream.write("id,value\n1,2\n")
            with self.assertRaisesRegex(ValueError, "columns"):
                list(p.read_csv(path, ("root_id",)))


if __name__ == "__main__":
    unittest.main()
