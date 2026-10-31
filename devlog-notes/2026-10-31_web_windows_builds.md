# 2026-10-31, night: the web build (itch.io) and the Windows build

Bruno:

```text
Now test the web build, since I intend to release the demo on itch io first. We'll also need to know if there'll be
any additional issues on windows not present on web and/or linux
```

## 1. The result

| Platform | Build | Demo bot (22 rooms) | Tutorial bot (26 steps) | Errors in the log |
|---|---|---|---|---|
| Linux (Vulkan/OpenGL) | 508 MB, 27 min (first) | 87 of 87 | (the editor runs it) | 0 |
| Web (Chrome, WebGL 2) | 90 MB, 12 min | 87 of 87 | 28 of 28 | 0 (after the fixes) |
| Windows (Direct3D 11, under Wine) | 488 MB, 41 min (first) | 87 of 87 | 28 of 28 | 0 from the game |

All three show the same picture (the same camera on each platform, checked side by side). The scan clip plays on all
three. The bots ran in the real players:

- Linux: `TheGlorifiedBody.x86_64 -batchmode -gbBot demo -gbRunDir <dir>`.
- Web: `node tools/web_bot.mjs builds/release_web demo <dir>` (new). It serves the build the way itch.io does (the
  gzip header), opens headless Chrome on the GTX 1050, passes the bot in the page URL (`?gbBot=demo`), and keeps the
  console and screenshots.
- Windows: `wine TheGlorifiedBody.exe -batchmode -gbBot demo -gbRunDir <dir>` (Wine 10, Direct3D 11 on the GTX 1050).

The bots now close a player when they finish (exit code 0 on a pass).

The last build of each (with the fix 10 below) is in `builds/`, with a zip for each upload:

- `builds/the_glorified_body_web.zip` (89 MB, `index.html` at the root, a 4 min build with a warm cache).
- `builds/the_glorified_body_windows.zip` (191 MB) and `builds/the_glorified_body_linux.zip` (200 MB), without the
  `BurstDebugInformation_DoNotShip` folder.

## 2. Web: what was wrong, and the fix

| # | Finding | Evidence | Fix |
|---|---|---|---|
| 1 | The web player cannot play an imported VideoClip (Unity manual, "Video playback in Web"). | Unity docs | `Core/VideoClips.Assign`: on the web a clip plays by URL from `StreamingAssets/Video/`. The web build copies the clips there for the build only. Log: `[Scan] the clip plays: frame 1 (.../scan_placeholder_video.webm)` |
| 2 | The saves stay in the browser only with `autoSyncPersistentDataPath`, off by default. | Unity issue UUM-60385 | Our page template (`Assets/WebGLTemplates/GlorifiedBody`, made by `PlayerBuild.EnsureWebTemplate`) turns it on. |
| 3 | The web player compiled the Invector code without its scripting defines (they existed for Standalone only). | `ProjectSettings.asset` | `PlayerBuild.SyncWebDefines`. Test `TheWebPlayer_GetsStandalonesDefines`. |
| 4 | After the build the editor stayed on WebGL, and no editor code compiled (46 errors in the vendor middleware). | test run `20261031-162552` | Every build switches the editor back to Linux at the end. |
| 5 | The first play of each sound was cut after 0.1 s: the browser decodes audio in the background, and `AudioClip.length` reads 0 until then. | "Trying to get length of sound which is not loaded yet" | `GameAudio.Length(clip, whenUnknown)` for every timer from a clip length (one-shots, voice, PA, tapes, sequences, cutscenes). |
| 6 | The gore driver logged an error on every hit (98 in one run). | "Not implemented icall: GC::GetAllocatedBytesForCurrentThread" | The web build skips that budget probe. |
| 7 | The page had a footer and a scrollbar under the 1280 x 720 canvas. | the screenshots | The canvas fills the page (the itch.io frame or its fullscreen). |
| 8 | The cursor lock: in a browser, Escape first releases the lock, and the pause key does not reach the game. | browser rule | No pointer lock on the web (no game code reads the mouse movement). |
| 9 | QUIT does nothing in a browser. | Unity docs | The web menu has no QUIT. |
| 10 | The debug line ("CAM ... LOCK ... WALK") showed in every release build, on every platform. | web screenshot | It shows only in dev (`GameBuild.Dev`). The How to Play screen lists the debug keys only in dev. Test `TheDebugStatusLine_ShowsOnlyInDev`. |
| 11 | The 2K look textures import uncompressed (16 MB each, 91 % of the Linux build). | build report | For the web only, `WebTexturePostprocessor` compresses them (DXT, 2048). The desktop builds keep them. At full size the gym shows no compression fault. |

Sizes on itch.io (limits: 500 MB in all, 200 MB for one file, 1,000 files): 90 MB in 21 files, the largest
`release_web.data.gz` 82 MB. The first load downloads 90 MB, and the browser keeps it for the next visit.

One remaining log line at load: "Trying to get length of sound which is not loaded yet" (4 times, 5 to 7 s after
the start, before any game timer). It does no harm.

Three more lines at load: "shader is not supported on this GPU" for `Hidden/CoreSRP/CoreCopy`,
`StencilDitherMaskSeed` and `HDRDebugView`. They are URP's own shaders for paths the web player does not use. The
picture is the same as on the desktop.

## 3. The crash of the session (16:36)

The second web build's IL2CPP step grew to 4.7 GB, and the machine ran out of memory. The kernel killed `il2cpp`, and
systemd's default `OOMPolicy=stop` then stopped the whole Devin app group it ran in. Two fixes:

- `tools/unity.sh build` runs itself in its own `systemd-run --user --scope` with a 9 GB limit (`GB_BUILD_MEM`). An
  overrun stops the build only.
- The web build uses IL2CPP "optimize size" code generation (the code file went from 12.7 MB to 10.6 MB).

The next web build took 12 min with no kill.

## 4. Windows: issues not on the web or Linux

The game shows none in the bot runs. Wine is not Windows, so here is what the runs cannot prove:

1. **SmartScreen:** an unsigned `.exe` downloaded from the web shows "Windows protected your PC". The player must
   press "More info", then "Run anyway". The itch.io app avoids it. A code signing certificate removes it (about $100
   to $400 a year). Some antivirus tools also flag new unsigned Unity games for a few days.
2. **Do not ship the folder** `The Glorified Body_BurstDebugInformation_DoNotShip` (Windows and Linux builds). Leave
   it out of the zip.
3. **Other GPUs:** the runs used one NVIDIA GPU. Intel and AMD drivers on Direct3D 11 are not tested. Our custom
   shaders (the dither, the stencil mark, the gore shaders) compiled for Direct3D 11 with no error.
4. **Video:** under Wine the hardware video decoder failed and Unity decoded the clip in software. Real Windows has
   the hardware decoder. The clips are VP8 webm: Windows plays them (Unity's own decoder).
5. **Saves** go to `%USERPROFILE%\AppData\LocalLow\brunodpoliveira\The Glorified Body\saves`. Our file names have no
   character that Windows refuses, and no number parse depends on the locale.
6. **Screen modes:** fullscreen, DPI scaling and Alt+Tab are not tested under Wine.

Windows builds only with Mono from this machine (IL2CPP for Windows needs Windows). Mono is fine for a demo.

## 5. Web: what the runs cannot prove (a manual check for Bruno, about 15 min)

Use itch.io's draft page (it stays private until you publish it):

1. Zip the contents of `builds/release_web/` (`index.html` at the root of the zip). Upload it, choose "This file
   will be played in the browser", viewport 1280 x 720, turn on the fullscreen button.
2. Press START. Expected: W1 loads, Stevens speaks (subtitles), there is sound.
3. Press Escape in the game. Expected: the pause menu. Then press the itch fullscreen button, and press Escape there.
   Expected: the browser leaves fullscreen. Report if the game pauses too, or does not pause.
4. Press Tab (the target switch) and F (scan) near a scan point. Expected: the game takes the key, the page does not
   move its focus.
5. Save at the W6 save machine, close the tab, open the page again, press CONTINUE. Expected: the save loads.
6. Try Firefox too. Safari: WebGL 2 works from Safari 15, and Safari's webm support is partial: report if the scan
   clip stays black.
7. A phone or tablet: not supported (the game needs a keyboard). Leave "Mobile Friendly" off on itch.io.

## 6. Proof

- Web: runs `20261031-174830-web-demo` (87 of 87, 0 errors) and `20261031-175045-web-tutorial` (28 of 28, 0 errors).
  The final build: `20261031-194231-web-demo` (87 of 87, 0 errors, no debug line, a 7 s load from the local server).
- Windows under Wine: `-wine-demo` (87 of 87) and `-wine-tutorial` (28 of 28) runs of 2026-10-31.
- Linux player: `20261031-154043-player-linux-demo` (87 of 87, 66 shots).
- Tests: `WebBuildTests` (3), `PlayerBuildTests` (2), `PlayerBuildFlagTests` (4), and 55 play mode tests of the
  changed systems (sound, tapes, sequences, screens, scans, gore). All pass.
