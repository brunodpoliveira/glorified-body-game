# 2026-10-25, study: saves, checkpoints and subtitles

Bruno: "before start to building the actual game, two things we need to implement are saves and checkpoints;
save/load and restart from checkpoints. and also subtitles: both in-game and in cutscenes. we need to look to see
if there are either free or paid solutions for unity re this already".

This is a study. Nothing is built. The design file (`03_GAME_DESIGN.md`) has no rule for saves or subtitles yet.

## 1. What the project has now

| Part | State |
|---|---|
| `RoomDamageLedger` | The room state already. Each room keeps "committed" and "pending" records, keyed by the scene name and the builder's id. A restart after a death drops the pending damage. The save game can write the committed records. |
| `GameManager.RestartFromLatestCheckpoint` | A stub from step 1. It returns false, and the death screen reloads the scene. |
| `ItemInventory`, `DrugInventory`, `Health`, the MK and SHORT bars | Our own classes. No save code. |
| Invector `vSaveLoadInventory` | Saves Invector's `vItemManager` only. We do not use that inventory. |
| Invector `vCheckpointExample` | A trigger that moves the respawn point. No state. |
| Warspite `PlayerRespawnController` | It moves the player back to a checkpoint after a hazard. No state. |

## 2. The market (checked 2026-10-25)

The repo is public under GPL-3.0. A paid package can never go into git: every clone must import it by hand. So
the licence matters more than the price.

### Saves

| Package | Price | Public repo | Note |
|---|---|---|---|
| Easy Save 3 (Moodkie) | $59 | No (Asset Store EULA) | The best paid serializer: slots, references. It does not know our room state. Unity 6 support is not stated. |
| Pixel Crushers Save System | Only inside the Dialogue System ($95) | No | Room state savers and an Invector bridge. The publisher marks the inventory saver as work in progress. |
| Save Game Free (BayatGames) | Free | Yes (MIT) | A thin file wrapper. It saves little work. |
| Quick Save, Save Game Pro | Free / $20 | No | No licence file / not updated since 2022. |
| Newtonsoft JSON (`com.unity.nuget.newtonsoft-json`) | Free | Yes (a Unity package) | Dictionaries and polymorphism, which `JsonUtility` cannot do. The base for our own system. |

### Subtitles

| Package | Price | Public repo | Note |
|---|---|---|---|
| Dialogue System for Unity (Pixel Crushers) | $95 | No | Subtitles with the speaker, barks, Timeline clips, an Invector bridge. Too large for our needs. |
| Subtitler, Closed Captions Toolkit (Gasimo) | $24 | No | Unity 6000.3 and URP stated, a Timeline track. |
| Subtitler, free branch (Gasimo) | Free | Yes (MIT) | No more updates. Speaker, clip timing, SRT, a Timeline track. Its UI is UI Toolkit (we use uGUI and TMP). A reference, or a fork. |
| Unity Localization (`com.unity.localization`) | Free | Yes | String tables and audio per language. It shows no subtitles itself. |
| Timeline samples, "Text Track" (Unity) | Free | Yes | TextMeshPro text on a Timeline track. The start point for the cutscene track. |

Sources: the Asset Store pages, the publishers' manuals and the GitHub repos. Two facts are not verified: Unity 6
support for Easy Save, and the assembly setup of the Dialogue System.

## 3. My recommendation: build both on free Unity packages ($0)

### Saves and checkpoints, about 3 to 5 days

1. One `SaveData` object in Newtonsoft JSON: the bars, the inventory, the scene and the spawn point, the story
   flags, and one record for each visited room (the ledger's data, plus the dead enemies, the open doors and the
   items taken).
2. Each saved object gets a fixed id from the editor code that builds the room. A rebuild must keep the ids.
3. Slots are files in `Application.persistentDataPath`, with a version number in each file.
4. The checkpoint is an automatic save in memory at each room entry. A death restarts the room as she entered it.
   This matches the ledger rule of today.
5. PlayMode tests: save, load, and compare each part. A bot step: die, restart, check the room.

### Subtitles, about 2 to 3 days

1. A `SubtitleService` (TMP): the speaker, the line, the time from the clip length.
2. The barks, the voice lines and the death screen line go through it.
3. Options: subtitles on or off, and the text size, in `SettingsManager`.
4. Cutscenes: a Timeline track from Unity's Text Track sample.
5. Each line has a key from the first day, so the localization tables come later without a rewrite.

Buy the Dialogue System ($95) only if the design gets branching conversations.

## 4. Risks

1. Fixed ids: if a room rebuild makes new ids, old saves stop matching. The save version and a migration step
   are necessary.
2. Invector keeps its own state on each enemy. A dead enemy must load as dead, through our death path.
3. A paid package breaks the build of a fresh clone of the public repo.
4. Lines written as plain text now must move to string tables later. Keys from the first day stop that.

## 5. Questions for Bruno, and his answers (2026-10-25, night)

1. Where can she save? "only in save rooms".
2. The checkpoint? "only at specific save points". No automatic checkpoint at each room entry.
3. How many slots? "3 manual save slots, one for the auto checkpoints".
4. Subtitles? "on by default; move to the bottom of the screen. subtitles should be yellow with black outlines,
   and subtitle must always identify speaker".
5. Localization? "english only for now".

So the build is: save rooms with a save point (3 manual slots), checkpoint points placed by the room builder (one
automatic slot), a death restarts from the last checkpoint, and every spoken line goes to a yellow subtitle with a
black outline and the speaker's name at the bottom of the screen. English only, with keys.
