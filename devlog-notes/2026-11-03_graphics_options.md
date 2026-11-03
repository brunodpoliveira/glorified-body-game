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

## Open for Bruno

1. The presets and their numbers are PROPOSED.
2. A gamma test picture next to the brightness row (as in many horror games)?
