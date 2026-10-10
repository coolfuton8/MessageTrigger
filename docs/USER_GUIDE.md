# MessageTrigger User Guide

MessageTrigger watches a rectangle of your screen, repeatedly runs OCR on it, and performs actions
(flash, sound, log, run a program) when phrases you specify appear.

Contents: [1. Requirements](#1-requirements) · [2. Build](#2-build) · [3. Run](#3-run) ·
[4. Configure](#4-configure) · [5. How triggering works](#5-how-triggering-works) ·
[6. config.json reference](#6-configjson-reference) · [7. Troubleshooting](#7-troubleshooting)

---

## 1. Requirements

| Item | Requirement |
|---|---|
| OS | Windows 10 version 1809 (build 17763) or later, Windows 11 |
| To build | [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (newer SDKs also work) |
| To run a build | .NET 8 **Desktop** Runtime (included with the SDK) |
| OCR | At least one Windows OCR language pack. Any normal install has one for the display language. |

No third-party OCR engine or language data is needed: the app uses the OCR built into Windows.

## 2. Build

### Command line

```bash
git clone https://github.com/coolfuton8/MessageTrigger.git
cd MessageTrigger
dotnet build -c Release
```

The executable is produced at:

```
bin\Release\net8.0-windows10.0.19041.0\MessageTrigger.exe
```

### Create a folder you can copy elsewhere

```bash
dotnet publish -c Release -o publish
```

Copy the resulting `publish` folder anywhere; run `MessageTrigger.exe` from it. The target machine needs the
.NET 8 Desktop Runtime.

### Visual Studio

Open `MessageTrigger.vbproj` (File > Open > Project/Solution) in Visual Studio 2022 with the
".NET desktop development" workload, then press F5 (run) or Ctrl+Shift+B (build).

## 3. Run

```bash
dotnet run
```

or double-click `MessageTrigger.exe`.

The setup window opens. Settings are loaded from `config.json` in the same folder as the executable (created
on first save). Typical first-time flow:

1. **Select region...** and drag a rectangle over the area to watch.
2. Add a phrase (or several) in the Phrases grid and tick the actions you want.
3. Click **Start watching**.
4. Click **Stop watching** to stop, or just close the window.

## 4. Configure

### 4.1 Capture region

| Control | Description |
|---|---|
| **Select region...** | The window minimizes and every monitor dims. Drag with the left mouse button to choose the area. Esc or right-click cancels. A drag smaller than 8x8 pixels is ignored. |
| **Hide frame / Show frame** | The area is outlined by a thin, click-through green frame. The frame is drawn just *outside* the captured pixels, so it never appears in the OCR input. Hiding it does not affect capture. |

The region is saved immediately after you select it. If your windows move, re-select the region.

### 4.2 Polling / OCR

| Setting | Range / default | Description |
|---|---|---|
| **Poll interval (ms)** | 100–60000 / 1000 | Delay between the end of one OCR pass and the start of the next. |
| **Re-arm after absent polls** | 1–1000 / 10 | How many consecutive polls a phrase must be missing before it can trigger again. |
| **Upscale (x)** | 1–4 / 2 | Enlarges the capture before OCR. Higher values read small text better but are slower. Reduced automatically if the result would exceed the OCR size limit. |

### 4.3 Phrases

Each row in the grid is one phrase. Type in the blank bottom row to add one; select a row and press
**Delete** to remove it.

| Column | Description |
|---|---|
| **Phrase** | Text to look for. Matching ignores case, whitespace and punctuation (see section 5). |
| **Flash** | Flash the screen when this phrase is found. |
| **Sound** | Play this phrase's sound when found. |
| **Log** | Append an entry to the log file when found. |
| **Run** | Launch this phrase's program when found. |

Select a row to edit that phrase's **own** settings in the *Settings for the selected phrase* box
(it is disabled for the blank "new" row, and while watching is running):

| Setting | Description |
|---|---|
| **Sound file** | `.wav` plays via the Windows sound player; `.mp3`, `.wma`, `.m4a` etc. play via the media player. Leave blank (or point at a missing file) to play the system beep. **Test** plays it. |
| **Run program** | Program or script to launch. **Test** launches it with the phrase `(test)`. |
| **Arguments** | Command-line arguments. `{phrase}` is replaced with the phrase text you configured, and `{time}` with the current time as `yyyy-MM-dd_HH-mm-ss`. |
| **Flash colour** | Colour used for this phrase's flash. **Test flash** previews it. |

Tips for **Run program**:

- The program is started with the Windows shell, so `.exe`, `.bat` and `.cmd` files work directly. Its working
  directory is the folder the program lives in.
- `.ps1` files normally open in an editor when launched directly. To *run* a PowerShell script, set
  *Run program* to `powershell.exe` and *Arguments* to
  `-NoProfile -File "C:\scripts\alert.ps1" "{phrase}"`.
- The text OCR'd from the screen is deliberately **never** passed to the command line - only the phrase you
  configured. This prevents on-screen text from injecting commands.

### 4.4 Flash length and log file (shared by all phrases)

| Setting | Range / default | Description |
|---|---|---|
| **Flash length (ms)** | 200–10000 / 1500 | Total time of the flash effect. The first half is 3 strobe pulses; the second half is a gradual fade-out. The flash covers every monitor and is click-through. |
| **Log file** | path | File that Log actions append to. Missing folders are created. |

Log lines look like:

```
2026-10-10 13:04:17 | Alpha Giant Crocodile | [Event] The Alpha Giant Crocodile slips back into the deep jungle.
```

(`timestamp | configured phrase | OCR line that contained it`)

### 4.5 Buttons and status

| Control | Description |
|---|---|
| **Start / Stop watching** | Starts or stops the polling loop. Starting requires a region and at least one phrase. While watching, the settings are locked. |
| **Save settings** | Writes `config.json` now. Settings are also saved when you press Start, after selecting a region, and when you close the window while not watching. |
| **Status line** | Shows the latest event: triggers, errors, saves. |
| **Last OCR result** | The raw text from the most recent pass - useful for choosing phrases and checking how well the region reads. |

## 5. How triggering works

1. Every poll, the region is captured, upscaled, and OCR'd.
2. The OCR text and each phrase are normalized: lower-cased, with everything except letters and digits
   removed. `[Event] A strange energy...` and OCR output such as `I-vent] A strange energyfelt` are compared in
   that form, so odd spacing or stray punctuation from OCR doesn't break a match. (Letters the OCR actually
   misreads still won't match - pick distinctive parts of a message.)
3. A phrase that is found while **armed** triggers its actions once and becomes **disarmed**.
4. A disarmed phrase becomes armed again only after it has been **absent for the configured number of
   consecutive polls** (default 10). If it reappears before that, the absent count restarts at zero.

All phrases are tracked independently. Each time you press Start, every phrase starts armed.

## 6. config.json reference

Stored next to `MessageTrigger.exe`. It is plain JSON and safe to edit while the app is closed. If the file is
missing or invalid, defaults are used.

```json
{
  "HasRegion": true,
  "RegionX": 5, "RegionY": 62, "RegionWidth": 475, "RegionHeight": 340,
  "PollIntervalMs": 1000,
  "RearmAfterAbsentPolls": 10,
  "UpscaleFactor": 2,
  "FlashDurationMs": 1500,
  "LogFile": "C:\\Logs\\messagetrigger.log",
  "Phrases": [
    {
      "Phrase": "Alpha Giant Crocodile",
      "Flash": true,  "FlashColor": "#00A0FF",
      "Sound": true,  "SoundFile": "C:\\Windows\\Media\\tada.wav",
      "Log": true,
      "Run": false,   "CommandPath": "", "CommandArgs": ""
    }
  ]
}
```

| Key | Meaning |
|---|---|
| `HasRegion`, `RegionX/Y/Width/Height` | Capture rectangle in screen pixels (virtual-desktop coordinates). |
| `PollIntervalMs` | See 4.2. Clamped to 100–60000. |
| `RearmAfterAbsentPolls` | See 4.2. Clamped to 1–1000. |
| `UpscaleFactor` | See 4.2. Clamped to 1–4. |
| `FlashDurationMs` | See 4.4. Clamped to 200–10000. |
| `LogFile` | See 4.4. |
| `Phrases[].Phrase` | Text to match. |
| `Phrases[].Flash / Sound / Log / Run` | Which actions fire for this phrase. |
| `Phrases[].FlashColor` | HTML colour (`#RRGGBB` or a name). Default `#FF0000`. |
| `Phrases[].SoundFile` | Sound path, blank for system beep. |
| `Phrases[].CommandPath / CommandArgs` | Program and arguments for the Run action. |

To start fresh, close the app and delete `config.json`.

## 7. Troubleshooting

| Symptom | Likely cause / fix |
|---|---|
| Error: "Windows OCR is not available: no OCR language pack..." | Add a language under *Settings > Time & language > Language & region* (the language's OCR capability must be installed), then restart the app. |
| OCR text is empty or garbage | Make sure the region is not covered by another window. Try a larger **Upscale**, a tighter region around just the text, or a larger in-game text size. The *Last OCR result* box shows exactly what was read. |
| Capture is black | The game is in exclusive fullscreen mode. Switch it to windowed or borderless-windowed. |
| Phrase never triggers | Check the phrase against the *Last OCR result* text; OCR may misread a character. Use a shorter or more distinctive part of the message. Also make sure the phrase has at least one action ticked. |
| Phrase triggers only once | Working as designed: it must be absent for the configured number of polls before it can trigger again. Lower **Re-arm after absent polls** if you want it re-armed sooner. |
| Phrase triggers repeatedly | Normally prevented by the re-arm rule. If the message flickers in and out of the region (e.g. chat scrolling it off and on) it can re-arm; raise **Re-arm after absent polls**. |
| Region is in the wrong place after changing monitor layout or resolution | Click **Select region...** again. |
| Run action does nothing | Use the **Test** button next to *Arguments* and read the status line. For PowerShell scripts use `powershell.exe -File ...` (see 4.3). |
| Sound action only beeps | The sound file path is blank or the file does not exist. |
| Settings not saved | `config.json` lives next to the exe, so that folder must be writable. If you installed under *Program Files*, copy the app to a user folder. |
