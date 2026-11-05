# 2026-11-03: the graphics options

Bruno:

```text
in the graphics we should allow the usual knobs and schanges that most games allow: anti-aliasing, qulaity textured,
resolution, graphical options on/off that sort of thing
```

## The Display/Graphics tab

| Section | Row | Values | What it changes |
|---|---|---|---|
| DISPLAY | Resolution | the screen's list | as before |
| | Display Mode | Fullscreen, Borderless, Windowed | the window. It was a Fullscreen on/off, and off becomes Windowed. |
| | VSync | Off, every, every 2nd | as before |
| | Frame Rate Limit | No limit, 30, 60, 120, 144 | `Application.targetFrameRate` (VSync wins when on) |
| | Brightness | -1.0 to +1.0 (exposure stops) | a global post-processing volume, on only away from 0 |
| GRAPHICS | Quality Preset | Low, Medium, High, Ultra, Custom | sets the six rows under it. A change to one row makes it Custom. |
| | Render Scale | 50, 67, 75, 85, 100 % | the URP render scale |
| | Anti-Aliasing | Off, FXAA, SMAA, MSAA 2x, 4x, 8x | MSAA on the URP asset, FXAA or SMAA on the camera |
| | Texture Quality | Low, Medium, High | the texture size: a quarter, a half, the full 2K |
| | Texture Filtering | Trilinear, Anisotropic 4x, 8x, 16x | the anisotropic level of every texture |
| | Shadows | Off, Low, Medium, High, Ultra | the sun map 512 to 4096, the spot atlas 1024 to 8192. Off holds the shadow distance at 0. |
| | Ambient Occlusion | on, off | the SSAO of the stock renderer |
| GAME | Gore, Subtitles, Subtitle Size | as before | |

The presets (PROPOSED):

| Preset | Scale | Anti-aliasing | Textures | Filtering | Shadows | AO |
|---|---|---|---|---|---|---|
| Low | 67 % | Off | Medium | Trilinear | Low | off |
| Medium | 85 % | FXAA | High | 4x | Medium | off |
| High (the default) | 100 % | SMAA | High | 8x | High (2048 and 4096, as before) | on |
| Ultra | 100 % | MSAA 4x | High | 16x | Ultra | on |

- No soft shadows: the shadows stay hard (ART_LESSONS 1.3).
- Gone: the LOOK option (the Anti-Aliasing row replaces it) and "Rain Effects" (a Warspite row that did nothing here).
- The game draws with the stock renderer in every room. `GB_Full` lost its dither this morning, so it had no reason
  to stay the default.
- The code: `Rendering/GraphicsOptions.cs` (the store, the presets, the apply). `LookQualityApplier` keeps its name
  (every room scene holds it) and applies the options at the start of a room and at each change. The URP asset is a
  shared file: the first change keeps its own values, and the end of play puts them back.
- A batch run (the tests, the bots) uses its own keys (`batch_gfx_...`), so the choices of a player never change a test.
- Fixed on the way: the title "OPTIONS" and the tab "Display/Graphics" wrapped onto two lines at 1366 x 768. The
  brightness and the mouse sensitivity values showed a comma ("3,0") on this machine's locale.
- The Controls tab lost the three Warspite rows (Wallrun Mode, Slide Mode, Catch/Throw Mode). No code of this game read
  them.
- Mouse Sensitivity now does something (Bruno: "keep it and have it actually do something"): a left or right move of the
  mouse turns her, on top of A and D, Mouse Sensitivity degrees for each unit of "Mouse X". A new row, Mouse Turn (on by
  default), turns it off for pure tank controls. During a charge she still turns to her target. Test:
  `TankControlTests.TheMouse_TurnsHer_BySensitivity_AndTheOptionTurnsItOff`.

## Proof

| Check | Result |
|---|---|
| `GraphicsOptionsTests` (4: the High default, each row changes the frame, the presets and Custom and the saved choices, the rows on the screen) | 4 of 4 |
| With the look room, system seam and tutorial tests | 44 of 44 |
| Edit mode | 61 of 61 |
| Bot `play TrainingRoom Main_Menu structural 300` (new shots of the tab) | pass |
| The whole PlayMode suite, run `20261103-025417-test-playmode-399784` | 345 of 345 |

Shots: `~/MEGA/glorified-body-renders/game/options/2026-11-03/` (`options_display.png` the top of the tab,
`options_display_lower.png` the rest).

## The empty dropdown lists (2026-11-05)

Bruno, with two screenshots: "THe dropdowns are not showing the options". Confirmed: a render of an open list showed the
panel and 0 bright pixels, though the row objects stood there with the right words. The list's viewport had a `Mask` on an
image with `Color.clear`. A mask writes its shape where its image has alpha, so it wrote nothing and clipped every row. The
image is opaque now, and `showMaskGraphic = false` keeps it invisible. Every dropdown of the Options screen shares this
builder, so Resolution, VSync, Gore and Subtitle Size were empty too. The code came with the Warspite port.

Test: `GraphicsOptionsTests.AnOpenDropdown_ShowsItsRows` (0 bright pixels before the fix, 792 after). It waits 0.6 s for the
list's fade: a first version rendered mid-fade and counted the buttons behind the list.

## The list scroll and the resolution order (2026-11-05)

Bruno: "the options on the dropdown scroll very slowly plus the resolution is not properly organized; it should be from
lowest to highest res".

- Scroll: the list never set `scrollSensitivity`, so it had Unity's default of 1. It is 30 now (the height of a row), and
  the list stops at its ends (`Clamped`).
- Order: `Screen.resolutions` on this machine sorts by height ("1024 x 576, 800 x 600, 1024 x 600, 960 x 640"). The list
  now comes from `SettingsManager.Resolutions()`: by width, then by height, one row for each size at its best refresh
  rate. It runs from 640 x 480 to 1920 x 1080.
- The save holds the width and the height (`settings_resolutionWidth`, `settings_resolutionHeight`). The old key held a
  place in the raw list, so an old choice resets to the desktop size once.

Test: `GraphicsOptionsTests.TheLists_ScrollARowANotch_AndTheResolutionsRunFromLowToHigh`. The options and menu tests pass 12
of 12 (run `20261105-012319-test-playmode-144303`). Nobody measured the feel of the scroll: a batch run has no mouse wheel.

## Open for Bruno

1. The presets and their numbers are PROPOSED.
2. A gamma test picture next to the brightness row (as in many horror games)?
