"""Original deterministic synthesis, not recordings. No third-party audio assets."""
import hashlib
import json
import math
import random
import struct
import wave
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
RATE = 22050
TAU = 2 * math.pi


def synth(name, seconds=8):
    rng = random.Random(14)
    samples = []
    smooth_noise = 0
    for i in range(int(RATE * seconds)):
        t = i / RATE
        smooth_noise = smooth_noise * .985 + rng.uniform(-1, 1) * .015
        if name == "InfantLullaby":
            note = [261.63, 329.63, 392, 329.63, 293.66, 349.23, 440, 349.23][int(t) % 8]
            beat = t % 1
            value = .28 * math.sin(TAU * note * t) * math.sin(math.pi * beat) ** 2
        elif name == "ChildhoodPlay":
            beat = t % .5
            toy = math.exp(-beat * 18) * (math.sin(TAU * 880 * t) + .3 * math.sin(TAU * 1320 * t))
            chirp = math.sin(TAU * (1700 * t + 70 * math.sin(t * 8))) * max(0, math.sin(t * 3)) ** 20
            value = .22 * toy + .06 * chirp + .12 * smooth_noise
        elif name == "AdolescenceAmbience":
            breeze = 1 + .7 * math.sin(TAU * t / 8)
            bell = math.sin(TAU * 523.25 * t) * math.exp(-(t % 4) * 2.5)
            value = 1.5 * smooth_noise * breeze + .16 * bell
        elif name == "YoungAdultAmbience":
            tap = math.sin(TAU * 210 * t) * math.exp(-(t % .75) * 70)
            value = .25 * tap + .9 * smooth_noise + .025 * math.sin(TAU * 110 * t)
        elif name == "MiddleAgeAmbience":
            value = .12 * math.sin(TAU * 196 * t) * (.6 + .4 * math.sin(TAU * t / 8)) + .6 * smooth_noise
        else:  # Quiet pre-entry cue, 0.3 s, not an information-only sound.
            value = .18 * math.sin(TAU * 660 * t) * math.sin(math.pi * t / seconds) ** 2
        # Window both ends to zero so all loop boundaries are click-free.
        envelope = min(1, t / .08, (seconds - t) / .08)
        samples.append(max(-.8, min(.8, value * max(0, envelope))))
    return samples


def main():
    directory = ROOT / "Assets/_Project/Audio"
    directory.mkdir(parents=True, exist_ok=True)
    report = []
    for name in ["InfantLullaby", "ChildhoodPlay", "AdolescenceAmbience", "YoungAdultAmbience", "MiddleAgeAmbience", "ItemApproach"]:
        samples = synth(name, .3 if name == "ItemApproach" else 8)
        path = directory / (name + ".wav")
        with wave.open(str(path), "wb") as stream:
            stream.setparams((1, 2, RATE, 0, "NONE", "not compressed"))
            stream.writeframes(b"".join(struct.pack("<h", round(s * 32767)) for s in samples))
        report.append(dict(file=path.name, samples=len(samples), rate=RATE,
                           peak=round(max(abs(s) for s in samples), 6),
                           rms=round(math.sqrt(sum(s*s for s in samples) / len(samples)), 6),
                           sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
    assert len({r["sha256"] for r in report}) == 6
    assert all(0 < r["rms"] < .3 and r["peak"] < 1 for r in report)
    output = ROOT / "docs/reports/step14-audio.json"
    output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print("Generated 6 distinct original cues; no clipping, mono 22050 Hz.")


if __name__ == "__main__":
    main()
