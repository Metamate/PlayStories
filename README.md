# PlayStories

An interactive picture-book app for small children (Unity 6, URP 2D, uGUI). Landscape, phone and tablet.
Danish and English.

**Open `Assets/PlayStories/Scenes/Main.unity` and press Play.**

## What's in the prototype

| Screen | Features |
|---|---|
| **Welcome** | Wavy animated title, a storybook "hero" with the cow and sheep idling beside it, drifting clouds and a butterfly. Tap anywhere: the book bursts open into the library. |
| **Library** | Horizontally scrolling shelf of book cards (easy to swipe in landscape) and an age filter (All / 0–1 / 1–3 yrs). "Coming soon" books are greyed out with a lock and wobble when tapped. Tapping a real book makes its cover fly up and fill the screen. |
| **Book** | Cover page, one page per animal, then a "Hurra!" page with confetti and replay. On animal pages the narrator says the animal's name in the selected language, and the letters bounce karaoke-style. The speaker button repeats it. Tap the animal to hear its sound, see its reaction, a speech bubble ("Muh!") and hearts/notes/stars. Tapping the background sparkles, which rewards the littlest ones who tap everywhere. If the child goes quiet, the animal wiggles ("tap me") and the next arrow nudges. |
| **Page turn** | Real page curl: the page peels off like paper, with a shaded paper back and a cast shadow. Use the arrows, or drag the page with a finger (it follows the finger, and snaps back if released early). |
| **Parental gate** | The **settings (gear)** button must be held for 5 s while a ring fills. **Closing the app** is only possible from inside settings, and is not shown on iOS because Apple doesn't allow apps to quit. The **back arrow** inside a book needs a 1 s hold. Hold times are on the Skin asset. |
| **Child lock** | On by default (toggle in settings). **Android:** immersive full screen + screen pinning, so home/recents/back gestures can't leave the app. Android asks once to confirm pinning, and "Luk appen" unpins. **iOS:** edge swipes must be done twice and the home indicator is hidden. Apple doesn't allow apps to lock themselves, so for a full lock turn on **Guided Access** (Settings ▸ Accessibility ▸ Guided Access, then triple-click the side button in the app); the settings panel shows this tip. The screen never dims while the app is open. |
| **Settings** | Language (Dansk / English, switches live), child lock, auto-read names, background sounds, sound effects, idle hints, volume, close app. |
| **Scaling** | Canvas reference is 1920×1200 with *Expand*, so the whole layout is visible on any aspect ratio. Backgrounds always cover the screen (cropping, never letterboxing). Buttons stay inside the device safe area (notches, home bar). Verified at 19.5:9 (phone), 16:10 and 4:3 (iPad). Landscape only. |

## The animation system (recommendation)

Goal: every animal feels alive, while your wife draws **one picture per animal**.

**Tier 1 – procedural, from one drawing (implemented).**
`SoftRigDeformer` turns each drawing into a bendable 18×18 mesh. `CharacterActor` moves that mesh every frame in layers:
- **Idle:** breathing (the belly swells), a slow head drift, tail swish, random blinks and double-blinks.
- **Tap reaction**, one style per animal: *Nod* (cow), *Hop* (sheep), *Wiggle* (pig), *Peck* (hen), *Waddle* (duck), *Spin*.
- **Voice sync:** the body stretches with the loudness of its own sound, and the eyes squeeze shut on loud parts, so it looks like the animal is shouting "MUH!".
- **Entrance and hints:** drops in with a squashy landing, and wiggles when idle.

Head and tail motion use two **soft bones**: a circle on the drawing plus a pivot point. They are set per animal in the Character asset (`motion.head` / `motion.tail`, in 0–1 image coordinates). No cutting into pieces, no skeleton setup, and all motion is squash-and-stretch with overshoot and settle, so it reads as cartoon rather than "rubber".

**Tier 2 – tiny extra drawings, big payoff (blink implemented).**
A transparent "eyes closed" overlay at the same canvas size as the animal gives blinking and happy closed eyes. The pipeline generated these automatically for all five animals. If your wife draws them herself (about 5 minutes per animal), drop them in and they're used. The same mechanism works for other cheap swaps later, like an open mouth or a surprised face.

**Tier 3 – skeletal rigs, only when a book needs real articulation.**
For the 1–2 year books ("wag the dog's tail", "turn the wheels"), export a **layered PSD** (body, head, tail and legs on separate layers). Unity's PSD Importer and 2D Animation packages are already in the project, so the parts can be rigged with bones. Use this only for books where the child *manipulates* a part. The baby books don't need it.

I don't recommend frame-by-frame sprite animation, or AI-generated in-between frames. It multiplies the drawing work by 10–30× per animal, and generated frames drift off-model from her style. Tiers 1+2 give most of the life for almost none of the cost.

## Project layout

```
RawAssets/                  source files (your wife's drawings, downloaded sounds, font)
  Audio/Narration/da|en/    ← put recorded narration here (see below)
Tools/
  build_art.py              cuts out / cleans art, generates blink overlays + placeholder art
  build_audio.py            trims & normalises sounds, makes loops, UI sfx, narration placeholders
Assets/PlayStories/
  Scripts/Core              AppController (navigation), AudioManager, Localization, Settings, AutoTest
  Scripts/Animation         CharacterActor (procedural animation), SoftRigDeformer
  Scripts/PageTurn          PageTurner + PageCurlMesh (page-curl effect)
  Scripts/Screens           Welcome / Library / Book screens, Settings panel
  Scripts/UI                widgets (buttons, hold-gate, safe area, wavy text), FX (particles, bubbles)
  Scripts/Data              BookDefinition, CharacterDefinition, BookLibrary, UISkin (ScriptableObjects)
  Editor/PlayStoriesBuilder one-click: import settings, font, data assets, scene, build settings
  Data/                     Library, Skin, Books/*, Characters/*  ← edit content here
  Books/Farm/{Art,Audio}    content of the first book
```

All UI is built from code, and everything content-related lives in data assets. **Adding a book is a data task, not a code task.**

## Common tasks

**Rebuild everything after changing raw assets**
```
python Tools/build_art.py
python Tools/build_audio.py
Unity menu: PlayStories ▸ 2. Build Content
```
(Requires Python 3.10+ with numpy + Pillow, and ffmpeg.) Build Content overwrites the values it sets on the generated Character/Book assets. Once you hand-tune those assets in the Inspector, either stop re-running it or put your values into the tables in `PlayStoriesBuilder.cs`.

**Narration (animal names, Danish + English)**
Generated with ElevenLabs: *Søren K* (native Danish) and *Jessica* (English), using `eleven_multilingual_v2`. Each clip is checked automatically with ElevenLabs speech-to-text and regenerated if the word isn't heard correctly.
```
$env:ELEVENLABS_API_KEY="sk_..."         # or put the key in Tools/.elevenlabs_key (git-ignored)
python Tools/generate_narration.py        # --force to regenerate, --only da|en
python Tools/build_audio.py
```
Words and voices are in the tables at the top of `generate_narration.py`; keep them in sync with the Character assets. Your own recordings still win: `RawAssets/Audio/Narration/<da|en>/<cow|sheep|pig|hen|duck>.wav`.

**Replace placeholder animals**
Draw the animal on a transparent background, **facing left** like the cow, at least 1024 px tall. Save it as `RawAssets/Animals/pig.png` (or `hen`, `duck`) and re-run `build_art.py`. If proportions changed, adjust the soft bones (`motion.head/tail` centre + pivot) on the Character asset.

**Add an animal / a book**
- Create ▸ PlayStories ▸ Character: sprite, optional blink sprite, sound, sound word (da/en), reaction style, particles, soft bones.
- Create ▸ PlayStories ▸ Book: title (da/en), cover, background, ambience, pages (Cover → Character… → End), age range.
- Add the book to `Data/Library.asset`.

**Add a language**
Add a value to `Language` in `Localization.cs` (+ `Code`/`NativeName`), then fill the new entries on the Skin/Book/Character assets. Missing entries fall back to Danish.

**Visual QA on many screen sizes**
`PlayStories ▸ 3. Build Windows Test Player`, then:
`Builds/WinTest/PlayStories.exe -autotest <out-folder> -screen-width 1560 -screen-height 720 -screen-fullscreen 0`
It clicks through every screen, tests drag-turns and the language switch, saves screenshots and quits.

## Placeholders / still to do
- Pig, hen and duck art; book covers for the coming-soon books; close/gear icons (the mockup ones were cut out where possible).
- Give the narration a listen. All 10 clips passed the speech-to-text check, but "And" needed a few attempts.
- The duck clip was cut from a longer pond recording; listen and re-cut in `build_audio.py` if it's not quacky enough.
- On iOS the close button is hidden, because Apple doesn't allow apps to quit themselves.
- Not built for device yet: switch platform to iOS/Android in Build Profiles. Orientation is already set to landscape-only.
- Ideas: a "record your own voice" mode in parental settings; separate sung vs spoken narration (from the sketch notes); a library topic filter (topics are already on the books).

See `CREDITS.md` for sound and font licences.
