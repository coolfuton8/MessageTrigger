# MessageTrigger

A small VB.NET (WinForms, .NET 8) tool that watches a rectangle of your screen, repeatedly OCRs it, and
performs actions when configured phrases appear — e.g. flash the screen or play a sound when a game chat
line such as `[Event] The Alpha Giant Crocodile ...` shows up.

OCR uses the **built-in Windows OCR engine** (`Windows.Media.Ocr`), so there is nothing extra to install
beyond an OCR-capable language pack (any normal Windows 10/11 install has one for your display language).

## Usage

1. `dotnet run` (or build and start `MessageTrigger.exe`). Requires the .NET 8 SDK/runtime on Windows 10 1809+.
2. **Select region...** — the screens dim; drag a rectangle over the area to watch. Afterwards only a thin,
   click-through green frame is left around the area (the frame sits *outside* the captured pixels, so it
   never ends up in the OCR).
3. Add one or more **phrases**, and tick which actions each one triggers: Flash / Sound / Log / Run.
   Select a phrase row to set *its own* sound file and program/arguments.
4. Set the shared flash colour/duration and log file, plus the polling interval, then **Start watching**.

### Trigger behaviour

A phrase fires once when first seen. It will not fire again until it has been **absent from the region for
N consecutive polls** (N = "Re-arm after absent polls", default 10). Matching ignores case, whitespace and
punctuation, which makes it tolerant of typical OCR spacing noise.

### Actions

| Action | What it does |
|---|---|
| Flash | Tints every monitor with the chosen colour for the configured duration (click-through). |
| Sound | Plays the phrase's own `.wav`/`.mp3`/etc. file; system beep if blank. |
| Log | Appends `timestamp \| phrase \| matched line` to the log file. |
| Run | Starts the phrase's own program/script. `{phrase}` and `{time}` in the arguments are substituted. Raw OCR text is deliberately never passed to the command line. |

### Settings

Everything is saved to `config.json` next to the executable and loaded on startup (also saved when you
start watching, press **Save settings**, or close the window).

## Notes

- The capture area must be visible on screen (not covered by other windows). Games should run in
  windowed / borderless-windowed mode; exclusive fullscreen may capture black.
- Larger areas and higher upscale factors take longer to OCR; poll interval is the delay *between* polls.
