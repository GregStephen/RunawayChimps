#!/usr/bin/env python3
"""Reproduce the reviewed hand-impact audio assets used by PR #62.

HandTap_Dry.wav is the conservative edit of the project's existing Walk2b
recording and remains available as a production-candidate reference.

HandImpact_DiagnosticTick.wav is intentionally synthetic and temporary. It is a
single ~20 ms damped tone with fixed timing and no material character so headset
testing can distinguish contact-trigger behavior from a misleading Foley sample.

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
DRY_OUTPUT = ROOT / "Assets/RunawayChimps/Shared/Music/HandTap_Dry.wav"
DIAGNOSTIC_OUTPUT = ROOT / "Assets/RunawayChimps/Shared/Music/HandImpact_DiagnosticTick.wav"
SOURCE_SHA256 = "519960ca2d9dd061c4d807e428fcf2849cfcd95e133f6b4ad512bf269fa5709e"
START_SECONDS = 0.112
END_SECONDS = 0.205
FADE_IN_SECONDS = 0.001
FADE_OUT_SECONDS = 0.024

DIAGNOSTIC_RATE = 48000
DIAGNOSTIC_SECONDS = 0.020
DIAGNOSTIC_FREQUENCY = 1800.0
DIAGNOSTIC_DECAY_SECONDS = 0.0038
DIAGNOSTIC_ATTACK_SECONDS = 0.0005
DIAGNOSTIC_FADE_OUT_SECONDS = 0.003
DIAGNOSTIC_AMPLITUDE = 0.52


def render_dry():
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
    return encode_pcm16(processed, rate), processed, rate


def render_diagnostic():
    rate = DIAGNOSTIC_RATE
    count = round(DIAGNOSTIC_SECONDS * rate)
    attack = round(DIAGNOSTIC_ATTACK_SECONDS * rate)
    fade_out = round(DIAGNOSTIC_FADE_OUT_SECONDS * rate)
    samples = []
    for index in range(count):
        time_seconds = index / rate
        gain = math.exp(-time_seconds / DIAGNOSTIC_DECAY_SECONDS)
        if index < attack:
            gain *= 0.5 - 0.5 * math.cos(math.pi * index / max(1, attack - 1))
        remaining = count - 1 - index
        if remaining < fade_out:
            gain *= 0.5 - 0.5 * math.cos(math.pi * remaining / max(1, fade_out - 1))
        sample = (
            DIAGNOSTIC_AMPLITUDE
            * math.sin(2.0 * math.pi * DIAGNOSTIC_FREQUENCY * time_seconds)
            * gain
        )
        samples.append(max(-32768, min(32767, round(sample * 32767))))
    samples[0] = 0
    samples[-1] = 0
    return encode_pcm16(samples, rate), samples, rate


def encode_pcm16(samples, rate):
    output = io.BytesIO()
    with wave.open(output, "wb") as target:
        target.setnchannels(1)
        target.setsampwidth(2)
        target.setframerate(rate)
        target.writeframes(struct.pack("<" + "h" * len(samples), *samples))
    return output.getvalue()


def validate(name, output, samples, rate, max_duration):
    peak = max(abs(value) for value in samples) / 32768
    rms = math.sqrt(sum((value / 32768) ** 2 for value in samples) / len(samples))
    onset = next(index for index, value in enumerate(samples) if abs(value) > 0.01 * 32768) / rate
    duration = len(samples) / rate
    if samples[0] != 0 or samples[-1] != 0 or peak >= 0.95 or onset > 0.010 or duration > max_duration:
        raise SystemExit(f"FAIL: {name} violates duration, fade, headroom, or prompt-onset checks.")
    print(
        f"PASS: {name}, mono PCM16, {duration:.3f}s, onset {onset * 1000:.1f}ms, "
        f"peak {peak:.4f}, RMS {rms:.4f}"
    )
    print(f"SHA256: {hashlib.sha256(output).hexdigest()}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Verify checked-in assets without rewriting them.")
    args = parser.parse_args()

    dry, dry_samples, dry_rate = render_dry()
    diagnostic, diagnostic_samples, diagnostic_rate = render_diagnostic()

    if args.check:
        expected = ((DRY_OUTPUT, dry), (DIAGNOSTIC_OUTPUT, diagnostic))
        for path, rendered in expected:
            if not path.exists() or path.read_bytes() != rendered:
                raise SystemExit(f"FAIL: {path.name} does not match the reproducible audio asset.")
    else:
        DRY_OUTPUT.write_bytes(dry)
        DIAGNOSTIC_OUTPUT.write_bytes(diagnostic)

    validate("dry tap candidate", dry, dry_samples, dry_rate, 0.100)
    validate("diagnostic tick", diagnostic, diagnostic_samples, diagnostic_rate, 0.025)


if __name__ == "__main__":
    main()
