import unittest
from summarize_capture import summarize


class CaptureTests(unittest.TestCase):
    def test_missing_gpu_does_not_claim_cpu_bound_or_zero_gpu_cost(self):
        result = summarize([{"room": "infant", "cpu_ms": "8", "gpu_ms": "", "window_frames": "36", "over_budget_frames": "2"}])["rooms"]["infant"]
        self.assertIsNone(result["gpu_ms"])
        self.assertEqual("unknown", result["bound_hint"])
        self.assertEqual(36, result["frames"])
        self.assertEqual(2, result["over_budget_frames"])

    def test_groups_rooms_and_discards_nonfinite_counters(self):
        rooms = summarize([{"room": "infant", "cpu_ms": "3", "gpu_ms": "12"},
                           {"room": "child", "cpu_ms": "NaN", "gpu_ms": "inf"}])["rooms"]
        self.assertEqual("GPU", rooms["infant"]["bound_hint"])
        self.assertIsNone(rooms["child"]["cpu_ms"])
        self.assertIsNone(rooms["child"]["gpu_ms"])


if __name__ == "__main__":
    unittest.main()
