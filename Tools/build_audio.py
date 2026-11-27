"""
PlayStories audio pipeline.

Turns the raw source recordings in RawAssets/Audio into game-ready clips in
Assets/PlayStories/... and synthesises the small UI sound effects.

Requirements: Python 3.10+, numpy, ffmpeg on PATH. Placeholder narration
uses Windows' built-in speech synthesiser (PowerShell + System.Speech).

    python Tools/build_audio.py

Re-run any time; outputs are overwritten. Narration: own recordings in RawAssets/Audio/Narration/<lang>/<id>.wav win over
ElevenLabs clips (<id>.tts.mp3, made by Tools/generate_narration.py), which win over
the Windows robot-voice fallback.
"""
from __future__ import annotations

import os
import subprocess
import sys
import tempfile
import wave

import numpy as np

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
RAW = os.path.join(ROOT, "RawAssets", "Audio")
COMMONS = os.path.join(RAW, "Commons")
NARRATION_SRC = os.path.join(RAW, "Narration")
FARM_OUT = os.path.join(ROOT, "Assets", "PlayStories", "Books", "Farm", "Audio")
UI_OUT = os.path.join(ROOT, "Assets", "PlayStories", "Audio", "UI")
AMB_OUT = os.path.join(ROOT, "Assets", "PlayStories", "Audio", "Ambience")
SR = 44100


# ---------------------------------------------------------------- helpers
def load(path: str) -> np.ndarray:
    """Decode any audio file to mono float32 @ 44.1k via ffmpeg."""
    out = subprocess.run(
        ["ffmpeg", "-loglevel", "error", "-i", path, "-ac", "1", "-ar", str(SR), "-f", "f32le", "-"],
        check=True, capture_output=True).stdout
    return np.frombuffer(out, np.float32).copy()


def save(x: np.ndarray, path: str) -> None:
    """Write mono float audio as Ogg Vorbis (or wav if the extension says so)."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    x = np.clip(x, -1, 1).astype(np.float32)
    if path.endswith(".wav"):
        with wave.open(path, "wb") as w:
            w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
            w.writeframes((x * 32767).astype(np.int16).tobytes())
        return
    subprocess.run(
        ["ffmpeg", "-loglevel", "error", "-y", "-f", "f32le", "-ar", str(SR), "-ac", "1", "-i", "-",
         "-c:a", "libvorbis", "-q:a", "6", path], input=x.tobytes(), check=True)
    print("  wrote", os.path.relpath(path, ROOT))


def seg(x: np.ndarray, start: float, end: float | None = None) -> np.ndarray:
    a = int(start * SR); b = len(x) if end is None else int(end * SR)
    return x[a:b].copy()


def fade(x: np.ndarray, fin: float = 0.01, fout: float = 0.05) -> np.ndarray:
    x = x.copy(); ni = max(1, int(fin * SR)); no = max(1, int(fout * SR))
    x[:ni] *= np.linspace(0, 1, ni) ** 2
    x[-no:] *= np.linspace(1, 0, no) ** 2
    return x


def highpass(x: np.ndarray, hz: float = 80) -> np.ndarray:
    """One-pole high-pass to remove rumble / DC."""
    a = np.exp(-2 * np.pi * hz / SR); y = np.zeros_like(x); prev_x = prev_y = 0.0
    for i, v in enumerate(x):
        prev_y = a * (prev_y + v - prev_x); prev_x = v; y[i] = prev_y
    return y


def normalize(x: np.ndarray, peak_db: float = -1.0, rms_db: float | None = -16.0) -> np.ndarray:
    """Bring clips to a consistent loudness, limited by a peak ceiling."""
    x = x - np.mean(x)
    peak = np.abs(x).max() + 1e-9
    g_peak = 10 ** (peak_db / 20) / peak
    if rms_db is None:
        return x * g_peak
    # rms over the loud part only (ignore silence)
    frame = SR // 50
    frames = [x[i:i + frame] for i in range(0, len(x) - frame, frame)] or [x]
    rms = np.array([np.sqrt(np.mean(f ** 2)) for f in frames])
    loud = rms[rms > rms.max() * 0.25]
    g_rms = 10 ** (rms_db / 20) / (np.sqrt(np.mean(loud ** 2)) + 1e-9)
    g = min(g_peak, g_rms)
    y = x * g
    # gentle soft-clip in case rms gain pushed peaks
    return np.tanh(y * 1.2) / np.tanh(1.2) if np.abs(y).max() > 0.95 else y


def concat(*parts: np.ndarray, gap: float = 0.08) -> np.ndarray:
    g = np.zeros(int(gap * SR), np.float32)
    out = []
    for i, p in enumerate(parts):
        if i: out.append(g)
        out.append(p)
    return np.concatenate(out)


def loopify(x: np.ndarray, xfade: float = 2.5) -> np.ndarray:
    """Equal-power crossfade of the tail into the head so the clip loops seamlessly."""
    n = int(xfade * SR)
    t = np.linspace(0, np.pi / 2, n)
    head, body, tail = x[:n], x[n:-n], x[-n:]
    blended = tail * np.cos(t) + head * np.sin(t)
    return np.concatenate([body, blended])


def env_adsr(n: int, a: float, d: float, s: float, r: float) -> np.ndarray:
    A, D, R = int(a * SR), int(d * SR), int(r * SR)
    S = max(0, n - A - D - R)
    return np.concatenate([np.linspace(0, 1, A, endpoint=False), np.linspace(1, s, D, endpoint=False),
                           np.full(S, s), np.linspace(s, 0, R)])[:n]


# ---------------------------------------------------------------- animal sounds
def animal_sounds() -> None:
    print("Animal sounds")
    c = lambda n: os.path.join(COMMONS, n)
    out = os.path.join(FARM_OUT, "Sounds")

    cow = seg(load(c("Single_Cow_Moo.ogg")), 0.35, 3.15)
    save(normalize(fade(highpass(cow), 0.04, 0.35)), os.path.join(out, "cow_moo.ogg"))

    sheep = load(c("Mudchute_sheep_1.ogg"))
    save(normalize(fade(highpass(sheep), 0.01, 0.15)), os.path.join(out, "sheep_baa.ogg"))

    pigs = [fade(highpass(load(c(f"Mudchute_pig_{i}.ogg"))), 0.005, 0.04) for i in (1, 2, 3)]
    save(normalize(concat(pigs[0], pigs[1], pigs[2], gap=0.06)), os.path.join(out, "pig_oink.ogg"))

    hen = seg(load(c("Hen_announcing_shes_lain_an_egg.ogg")), 0.0, 2.1)
    save(normalize(fade(highpass(hen, 120), 0.01, 0.2)), os.path.join(out, "hen_cluck.ogg"))

    duck = seg(load(c("Pekin_duck_and_mallard.ogg")), 64.3, 65.9)
    save(normalize(fade(highpass(duck, 150), 0.02, 0.2)), os.path.join(out, "duck_quack.ogg"))

    horse = load(c("Wiehern.ogg"))
    save(normalize(fade(highpass(horse), 0.02, 0.3)), os.path.join(out, "horse_neigh.ogg"))


# ---------------------------------------------------------------- ambience
def ambience() -> None:
    print("Ambience")
    birds = highpass(load(os.path.join(COMMONS, "Gentle_breeze_and_birds_singing.ogg")), 60)
    birds = normalize(birds, -3, -24)
    save(loopify(birds, 3.0), os.path.join(AMB_OUT, "farm_birds_loop.ogg"))


# ---------------------------------------------------------------- UI sfx (synthesised)
rng = np.random.default_rng(7)


def sfx_pop() -> np.ndarray:
    n = int(0.12 * SR); t = np.arange(n) / SR
    f = 900 * np.exp(-t * 18) + 380
    ph = 2 * np.pi * np.cumsum(f) / SR
    return np.sin(ph) * np.exp(-t * 32) * 0.9


def sfx_tap_soft() -> np.ndarray:
    n = int(0.09 * SR); t = np.arange(n) / SR
    f = 620 * np.exp(-t * 10) + 520
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 45) * 0.7


def sfx_page_turn() -> np.ndarray:
    """Papery swoosh: band-limited noise sweeping up with a soft flutter."""
    n = int(0.55 * SR); t = np.arange(n) / SR
    noise = rng.standard_normal(n)
    # crude time-varying band-pass via two one-pole filters with moving cutoffs
    out = np.zeros(n); lp = hp_state = prev = 0.0
    for i in range(n):
        p = t[i] / t[-1]
        fc = 600 + 3200 * p
        a = np.exp(-2 * np.pi * fc / SR)
        lp = (1 - a) * noise[i] + a * lp
        b = np.exp(-2 * np.pi * 300 / SR)
        hp_state = b * (hp_state + lp - prev); prev = lp
        out[i] = hp_state
    shape = np.sin(np.pi * np.clip(t / t[-1], 0, 1)) ** 1.5
    flutter = 1 + 0.35 * np.sin(2 * np.pi * 22 * t)
    return out * shape * flutter * 4.0


def sfx_sparkle() -> np.ndarray:
    n = int(0.9 * SR); out = np.zeros(n)
    notes = [1568, 2093, 2637, 3136, 2349]
    for k, f in enumerate(notes):
        start = int(k * 0.07 * SR); m = n - start; t = np.arange(m) / SR
        tone = (np.sin(2 * np.pi * f * t) + 0.3 * np.sin(2 * np.pi * f * 2.01 * t)) * np.exp(-t * 7)
        out[start:] += tone * 0.25
    return out


def sfx_chime_open() -> np.ndarray:
    """Warm 'book opens' arpeggio."""
    n = int(1.3 * SR); out = np.zeros(n)
    for k, f in enumerate([523.25, 659.25, 783.99, 1046.5]):
        start = int(k * 0.09 * SR); m = n - start; t = np.arange(m) / SR
        tone = np.sin(2 * np.pi * f * t) + 0.25 * np.sin(2 * np.pi * 2 * f * t) + 0.1 * np.sin(2 * np.pi * 3 * f * t)
        out[start:] += tone * np.exp(-t * 3.2) * 0.22
    return out


def sfx_boing() -> np.ndarray:
    n = int(0.45 * SR); t = np.arange(n) / SR
    f = 180 + 140 * np.sin(2 * np.pi * 9 * t) * np.exp(-t * 6) + 120 * np.exp(-t * 8)
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * env_adsr(n, 0.005, 0.1, 0.6, 0.25) * 0.8


def sfx_hold_tick() -> np.ndarray:
    n = int(0.05 * SR); t = np.arange(n) / SR
    return np.sin(2 * np.pi * 1400 * t) * np.exp(-t * 90) * 0.5


def sfx_whoosh() -> np.ndarray:
    n = int(0.7 * SR); t = np.arange(n) / SR
    noise = rng.standard_normal(n); out = np.zeros(n); lp = 0.0
    for i in range(n):
        fc = 300 + 2500 * np.sin(np.pi * t[i] / t[-1]) ** 2
        a = np.exp(-2 * np.pi * fc / SR); lp = (1 - a) * noise[i] + a * lp; out[i] = lp
    return out * np.sin(np.pi * t / t[-1]) ** 2 * 3.0


def ui_sfx() -> None:
    print("UI sfx")
    for name, fn, rms in [("pop", sfx_pop, -14), ("tap_soft", sfx_tap_soft, -18), ("page_turn", sfx_page_turn, -18),
                          ("sparkle", sfx_sparkle, -20), ("chime_open", sfx_chime_open, -18),
                          ("boing", sfx_boing, -16), ("hold_tick", sfx_hold_tick, -22), ("whoosh", sfx_whoosh, -20)]:
        save(normalize(fade(fn().astype(np.float32), 0.002, 0.03), -2, rms), os.path.join(UI_OUT, f"{name}.ogg"))


# ---------------------------------------------------------------- narration
# Narration = the animal names only, per language. Source priority for each clip:
#   1. your own recording   RawAssets/Audio/Narration/<lang>/<id>.(wav|m4a|mp3|ogg)
#   2. ElevenLabs voice     RawAssets/Audio/Narration/<lang>/<id>.tts.mp3  (Tools/generate_narration.py)
#   3. Windows robot voice  (last-resort placeholder; Danish is read phonetically by an English voice)
NARRATION_IDS = ["cow", "sheep", "pig", "hen", "duck"]
ROBOT = {
    "da": {"cow": "Koh.", "sheep": "For.", "pig": "Greece.", "hen": "Hurr-neh.", "duck": "Ann."},
    "en": {"cow": "Cow.", "sheep": "Sheep.", "pig": "Pig.", "hen": "Chicken.", "duck": "Duck."},
}


def tts_placeholder(text: str, path: str) -> None:
    ps = f"""
Add-Type -AssemblyName System.Speech
$s = New-Object System.Speech.Synthesis.SpeechSynthesizer
try {{ $s.SelectVoice('Microsoft Hazel Desktop') }} catch {{}}
$s.Rate = -2
$s.SetOutputToWaveFile('{path}')
$s.Speak('{text}')
$s.Dispose()
"""
    subprocess.run(["powershell", "-NoProfile", "-Command", ps], check=True)


def find_source(lang: str, key: str) -> tuple[str | None, str]:
    folder = os.path.join(NARRATION_SRC, lang)
    for ext in (".wav", ".m4a", ".mp3", ".ogg", ".flac"):
        p = os.path.join(folder, key + ext)
        if os.path.exists(p):
            return p, "recorded"
    p = os.path.join(folder, key + ".tts.mp3")
    if os.path.exists(p):
        return p, "elevenlabs"
    return None, "robot"


def narration() -> None:
    print("Narration")
    with tempfile.TemporaryDirectory() as tmp:
        for lang in ROBOT:
            out_dir = os.path.join(FARM_OUT, "Narration", lang)
            for key in NARRATION_IDS:
                src, kind = find_source(lang, key)
                if src is None:
                    if sys.platform != "win32":
                        print("  (no source and not on Windows, skipped)", lang, key); continue
                    src = os.path.join(tmp, f"{lang}_{key}.wav")
                    tts_placeholder(ROBOT[lang][key], src)
                print(f"  {lang}/{key}: {kind}")
                x = load(src)
                # trim silence
                thr = np.abs(x).max() * 0.02
                idx = np.where(np.abs(x) > thr)[0]
                if len(idx):
                    x = x[max(0, idx[0] - int(0.02 * SR)): idx[-1] + int(0.12 * SR)]
                save(normalize(fade(highpass(x), 0.005, 0.08), -1, -15), os.path.join(out_dir, f"{key}.ogg"))


if __name__ == "__main__":
    animal_sounds()
    ambience()
    ui_sfx()
    narration()
    print("done")
