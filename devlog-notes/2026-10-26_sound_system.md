# 2026-10-26, night: the sound system

Bruno's answer to the audio sweep (`2026-10-26_sweeps/audio.md`): build the systems "to be tested", with
placeholders. Everything below is new code in `Assets/_Game/Scripts/Audio/` (namespace `GlorifiedBody.Audio`). No
scene changed: one object, `[GameAudio]`, starts with the game and stays through every load (`AudioHost`).

## What the game does now

| Part | Behaviour |
|---|---|
| Channels | Every sound plays on a channel: SFX, Music, Voice or UI (`GameAudio`). The Options menu has a slider for each, plus Master. A long source (an enemy gun, a loop) follows a slider change at once (`ChannelSource`). Before, the SFX slider reached only the HUD beeps, and the Music slider nothing. |
| New option | Options > Audio > Voice Volume (`SettingsManager.VoiceVolume`). |
| One 3D curve | Full level within 1.5 m, logarithmic to 40 m, for every world sound. Before, the breaks used Unity's 500 m curve. |
| Pause | The pause, the inventory and the death screen pause the world sounds. The music plays on, lower (0.4). The menus click. |
| The ears | The listener is on Lucy's head and turns with the active camera (`ListenerRig`). Before, it was on the fixed camera, so a cut moved the sound and could swap the sides. The main menu keeps the camera's listener. |
| Music | `MusicDirector`: Combat while an enemy aims at her or for 8 s after a hit from her or on her. Tension while an armed enemy hunts her. Else Ambient. Menu on the main menu, silence on the death screen and for a cutscene (`MusicDirector.Hold`). A 2 s crossfade at each change. |
| Tracks | `Resources/Audio/Music/<room key>/<state>.ogg`. The room key comes from a `RoomMusic` component (a sector name shared by its rooms), or else the scene name. A gap plays `_placeholder/<state>.ogg` and logs `music_placeholder` once. The music goes on through a door when the next room has the same track. |
| Room tone | A quiet hum and vent loop under the music in every gameplay room (`<room key>/room_tone`, else the placeholder). |
| Voice | `Voice.Say(speaker, id, words)`: it plays `Resources/Voice/<id>.ogg` on the Voice channel, and the subtitle stays for the clip. With no clip, the subtitle shows alone and the id goes to the run log (`voice_missing`). A line ducks the music. `Voice.PA` plays a chime, then the line in 2D with "PA:" as the speaker. Every bark goes through it. A bark's id is its speaker and first six words, for example `nurse.come_on_breathe_for_me_im`. |
| Tutorial presenter | Each step plays `Resources/Voice/presenter.<step>` when it exists. The prompt panel keeps the words. |
| Scan clips | A scan video plays its own sound on the Voice channel. Before, the code muted it. |
| Death screen | The sting plays, the world sounds stop, and the death line plays on the Voice channel. |

The events with a sound now (one folder each in `Resources/Audio/Sfx/`):

| Group | Events |
|---|---|
| Lucy | steps (bare feet), dodge, the charge hum (its pitch rises with the charge), mk_warspite, mk_erebus (quiet for a part charge), mk_orzabal's lift, throw, slam, catch, drug, a refusal ("TOO HEAVY", "NOT ENOUGH MK"), her hurt |
| Figures | boots, a flesh hit, a body that falls, the crackle of a fire on a body |
| Weapons | the grenade's bounce, the rocket in flight, the gas hiss (the guns and explosions were already there) |
| World | pickup, a locked door, a door that opens, a log open and close, the scan, a save, a checkpoint |
| UI | a move of the selection, a button press, the pause and the inventory open and close, the death sting, the PA chime |

## Placeholders

All of it is a placeholder until the sound source is chosen (RFab gap 14):

- `tools/audio_placeholders.py` makes the music (ambient 40 s, tension 33 s at 72 bpm with a heartbeat, combat 21 s
  at 140 bpm, menu 38 s), the room tone, the hum, the chime, the sting and the hiss in numpy.
- It copies Kenney's CC0 packs ("Impact Sounds", "Interface Sounds", "RPG Audio", "Sci-fi Sounds") for the rest. The
  licences are in `Resources/Audio/Sfx/License_*.txt`. All of it is safe for the public repo. Size: 4.5 MB.
- Spectrograms of the four tracks: `~/glorified-body-renders/game/audio/2026-10-26/music_spectrograms.png`.

To replace a sound, put a file in its folder. A file named like a placeholder replaces it. To give a room or a
sector its music, add `Resources/Audio/Music/<key>/ambient.ogg` (and `tension`, `combat`, `room_tone`). To voice a
line, add `Resources/Voice/<id>.ogg`. The ids still to record are in each run's telemetry (`voice_missing`).

## Proof

- `SoundSystemTests` (7, the training room):
  - Every event has clips, and every music state has a placeholder.
  - The sliders reach a one-shot, a bound source, a voice line and the music.
  - The pause stops the world sounds but not the music. A button clicks. The inventory stops the world sounds too.
  - The ears are on Lucy, with one listener, and stay at her head through a camera cut, turned like the new camera.
  - The music is the training room's own ambient, then Combat (the placeholder) when a dummy aims, then Ambient
    again after the hold. A hold gives silence.
  - A voice line plays its clip in 3D with its subtitle and ducks the music. A bark with no clip shows the subtitle
    alone and lists its id. The PA chimes, then shows "PA:" after 1.2 s.
  - Her steps, the dodge, the charge hum (its pitch rises), the blast and the flesh hit play.
- `EnemyAiTests.HuntingSoldiers_TurnTheMusicToTension_AndTheirBootsSound` (the AI room): alerted soldiers with
  their guns off give Tension, and their boots step.
- The full run: EditMode 49/49, PlayMode 203/205, the six bots CLEAN. The two failures came from the new sources:
  - `BreakableTests.ABulletOnTheBarricade_PlaysOneWoodHit` counted the music and the room tone. They are 2D sources
    at the origin, near the barricade. The test counts world sounds (3D) only now.
  - `ShotSoundTests.AGasCloud_PlaysOneSound` heard the pop and the new hiss at once. That broke Bruno's rule of one
    sound for one event (2026-10-25). The hiss starts 1 s after the pop now.
  - After the two fixes: 18/18 (the barricade, every `ShotSoundTests`, every `SoundSystemTests`).

## For later

- The real sounds and music (RFab Music Studio, or another source: RFab gap 14).
- The barks in `barks.csv` that the code does not say yet (spots, aims, fires, hit, reloads).
- An enemy's hurt and death cries are voice lines: they wait for the voice work.
