"""
Generates the narrator voice (animal names) with ElevenLabs and verifies every clip with
ElevenLabs speech-to-text, so a mispronounced word is caught without listening to all of them.

    set ELEVENLABS_API_KEY=sk_...        (PowerShell: $env:ELEVENLABS_API_KEY="sk_...")
    python Tools/generate_narration.py [--force] [--only da|en]
    python Tools/build_audio.py

The key is read from the environment (or from Tools/.elevenlabs_key, which is git-ignored) and
is never written into the project.

Output: RawAssets/Audio/Narration/<lang>/<id>.tts.mp3
Your own recordings (<id>.wav/.m4a/.mp3/.ogg in the same folder) always win over these.
"""
from __future__ import annotations

import argparse
import json
import os
import sys
import urllib.request
import uuid

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
OUT = os.path.join(ROOT, "RawAssets", "Audio", "Narration")

# One narrator per language. Voices must exist in the ElevenLabs account (GET /v1/voices).
VOICES = {
    "da": {"voice_id": "xj6X4BCUsv9oxohm1E8o", "name": "Søren K",            # native Danish
           "context": "Se her, børn! Hvad er det for et dyr? Det er en", "stt": "dan"},
    "en": {"voice_id": "cgSgspJ2msm6clMCkdW9", "name": "Jessica",            # playful, warm
           "context": "Look, children! What animal is this? It's a", "stt": "eng"},
}

# id -> word per language. Keep in sync with the Character assets (PlayStoriesBuilder.cs).
WORDS = {
    "cow":   {"da": "Ko",   "en": "Cow"},
    "sheep": {"da": "Får",  "en": "Sheep"},
    "pig":   {"da": "Gris", "en": "Pig"},
    "hen":   {"da": "Høne", "en": "Chicken"},
    "duck":  {"da": "And",  "en": "Duck"},
}

# Words the transcriber may legitimately write differently (homophones).
ACCEPT = {("da", "Får"): {"får", "for"}}

MODEL = "eleven_multilingual_v2"
SETTINGS = {"stability": 0.5, "similarity_boost": 0.8, "style": 0.35, "use_speaker_boost": True}


def api_key() -> str:
    k = os.environ.get("ELEVENLABS_API_KEY")
    p = os.path.join(os.path.dirname(__file__), ".elevenlabs_key")
    if not k and os.path.exists(p):
        k = open(p, encoding="utf-8").read().strip()
    if not k:
        sys.exit("Set ELEVENLABS_API_KEY (or create Tools/.elevenlabs_key)")
    return k


def tts(key: str, voice: str, text: str, context: str) -> bytes:
    body = {"text": text, "model_id": MODEL, "previous_text": context, "voice_settings": SETTINGS}
    req = urllib.request.Request(
        f"https://api.elevenlabs.io/v1/text-to-speech/{voice}?output_format=mp3_44100_128",
        data=json.dumps(body).encode(), headers={"xi-api-key": key, "Content-Type": "application/json"})
    return urllib.request.urlopen(req, timeout=60).read()


def stt(key: str, audio: bytes, lang: str) -> str:
    b = uuid.uuid4().hex
    parts = []
    for n, v in (("model_id", "scribe_v1"), ("language_code", lang)):
        parts.append(f'--{b}\r\nContent-Disposition: form-data; name="{n}"\r\n\r\n{v}\r\n'.encode())
    parts.append(f'--{b}\r\nContent-Disposition: form-data; name="file"; filename="a.mp3"\r\n'
                 f'Content-Type: audio/mpeg\r\n\r\n'.encode() + audio + b"\r\n")
    parts.append(f"--{b}--\r\n".encode())
    req = urllib.request.Request("https://api.elevenlabs.io/v1/speech-to-text", data=b"".join(parts),
                                 headers={"xi-api-key": key, "Content-Type": f"multipart/form-data; boundary={b}"})
    return json.load(urllib.request.urlopen(req, timeout=60)).get("text", "")


def norm(s: str) -> str:
    return "".join(c for c in s.lower() if c.isalpha())


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--force", action="store_true", help="regenerate even if the clip exists")
    ap.add_argument("--only", choices=list(VOICES), help="only this language")
    args = ap.parse_args()
    key = api_key()
    ok = True
    for lang, v in VOICES.items():
        if args.only and lang != args.only:
            continue
        os.makedirs(os.path.join(OUT, lang), exist_ok=True)
        for wid, words in WORDS.items():
            word = words[lang]
            path = os.path.join(OUT, lang, f"{wid}.tts.mp3")
            if os.path.exists(path) and not args.force:
                print(f"  {lang}/{wid}: exists ({word})")
                continue
            accept = ACCEPT.get((lang, word), {norm(word)})
            heard = ""
            for attempt in range(5):  # retry if the check fails (generation is stochastic)
                audio = tts(key, v["voice_id"], word + "!", v["context"])
                heard = stt(key, audio, v["stt"])
                if norm(heard) in accept:
                    break
            open(path, "wb").write(audio)
            good = norm(heard) in accept
            ok &= good
            print(f"  {lang}/{wid}: '{word}' -> heard '{heard}' {'OK' if good else '<-- CHECK BY EAR'}")
    print("all verified" if ok else "some clips need a listen")


if __name__ == "__main__":
    main()
