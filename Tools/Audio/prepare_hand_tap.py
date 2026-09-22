#!/usr/bin/env python3
"""Reproduce the conservative dry hand-tap edit from the existing Walk2b source.

This is an edit of the project's recording, not a new or material-specific Foley
library. The original is left untouched. Keep the first impact; remove its ~112 ms
lead-in, the later secondary bump, and the nearly one-second noise/reverb tail.
Output: 48 kHz, mono PCM16, 93 ms, no gain boost, short click-prevention fades.

Run from any directory. --check compares exact bytes without writing anything.
"""

import argparse
import hashlib
import io
import math
from pathlib import Path
import struct
import wave


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Assets/RunawayChimps/Shared/Music/Walk2b.wav"
OUTPUT = ROOT / "Assets/RunawayChimps/Shared/Music/HandTap_Dry.wav"
SOURCE_SHA256 = "519960ca2d9dd061c4d807e428fcf2849cfcd95e133f6b4ad512bf269fa5709e"
START_SECONDS = 0.112
END_SECONDS = 0.205
FADE_IN_SECONDS = 0.001
FADE_OUT_SECONDS = 0.024


def render():
    data = SOURCE.read_bytes()
    if hashlib.sha256(data).hexdigest() != SOURCE_SHA256:
        raise ValueError("Walk2b.wav differs from the reviewed source; review the crop before regenerating.")
    with wave.open(io.BytesIO(data), "rb") as source:
        if source.getsampwidth() != 2 or source.getcomptype() != "NONE":
            raise ValueError("Expected uncompressed PCM16 source audio.")
        rate = source.getframerate()
        channels = source.getnchannels()
        source.setpos(round(START_SECONDS * rate))
        raw = source.readframes(round((END_SECONDS - START_SECONDS) * rate))
    values = struct.unpack("<" + "h" * (len(raw) // 2), raw)
    samples = [sum(values[index:index + channels]) / channels
               for index in range(0, len(values), channels)]
    dc = sum(samples) / len(samples)
    fade_in = round(FADE_IN_SECONDS * rate)
    fade_out = round(FADE_OUT_SECONDS * rate)
    processed = []
    for index, sample in enumerate(samples):
        gain = 1.0
        if index < fade_in:
            gain *= 0.5 - 0.5 * math.cos(math.pi * index / (fade_in - 1))
        remaining = len(samples) - 1 - index
        if remaining < fade_out:
            gain *= 0.5 - 0.5 * math.cos(math.pi * remaining / (fade_out - 1))
        processed.append(max(-32768, min(32767, round((sample - dc) * gain))))
    output = io.BytesIO()
    with wave.open(output, "wb") as target:
        target.setnchannels(1)
        target.setsampwidth(2)
        target.setframerate(rate)
        target.writeframes(struct.pack("<" + "h" * len(processed), *processed))
    return output.getvalue(), processed, rate


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Verify the checked-in edit without rewriting it.")
    args = parser.parse_args()
    output, samples, rate = render()
    if args.check:
        if not OUTPUT.exists() or OUTPUT.read_bytes() != output:
            raise SystemExit("FAIL: HandTap_Dry.wav does not match the reproducible edit.")
    else:
        OUTPUT.write_bytes(output)
    peak = max(abs(value) for value in samples) / 32768
    rms = math.sqrt(sum((value / 32768) ** 2 for value in samples) / len(samples))
    onset = next(index for index, value in enumerate(samples) if abs(value) > 0.01 * 32768) / rate
    if samples[0] != 0 or samples[-1] != 0 or peak >= 0.95 or onset > 0.010:
        raise SystemExit("FAIL: edited tap violates fade, headroom, or prompt-onset checks.")
    print(f"PASS: mono PCM16 tap, {len(samples) / rate:.3f}s, onset {onset * 1000:.1f}ms, "
          f"peak {peak:.4f}, RMS {rms:.4f}; source preserved.")
    print(f"SHA256: {hashlib.sha256(output).hexdigest()}")


if __name__ == "__main__":
    main()
