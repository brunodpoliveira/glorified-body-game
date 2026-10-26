#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using GlorifiedBody.Audio;
using GlorifiedBody.Automation;
using GlorifiedBody.Cameras;
using GlorifiedBody.Core;
using GlorifiedBody.Enemy;
using GlorifiedBody.Player;
using GlorifiedBody.Subtitles;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The sound system (2026-10-26, Bruno: "yes, we should add the systems to be tested"), in the training room:
    /// the sliders reach every channel, the pause stops the world sounds and not the music, the ears stay on Lucy
    /// through a camera cut, the music goes from Ambient to Combat and back with the room's own track or the
    /// placeholder, a voice line plays its clip or shows its subtitle alone, the PA chimes first, her steps, dodge and
    /// powers make sound, the menus click. Batch: tools/unity.sh test playmode SoundSystemTests.
    /// </summary>
    public class SoundSystemTests
    {
        [DefaultExecutionOrder(-1000)]
        private sealed class PadDriver : MonoBehaviour
        {
            public VirtualPad pad;
            private void Update() => pad?.Commit();
        }

        private VirtualPad pad;
        private PadDriver driver;
        private GameObject lucy;
        private TankController tank;
        private PsychicStats stats;
        private PsychicPowers powers;
        private float sfx0, music0, voice0;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("Main_Menu");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            GameManager.Instance.LoadGameplay();
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "TrainingRoom" && GameManager.Instance.IsPlaying, 10f);
            yield return null;
            foreach (DummyShooter shooter in UnityEngine.Object.FindObjectsByType<DummyShooter>(FindObjectsSortMode.None)) shooter.enabled = false;
            lucy = GameObject.FindGameObjectWithTag("Player");
            tank = lucy.GetComponent<TankController>();
            stats = lucy.GetComponent<PsychicStats>();
            powers = lucy.GetComponent<PsychicPowers>();
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
            SettingsManager s = SettingsManager.Instance;
            sfx0 = s.SFXVolume;
            music0 = s.MusicVolume;
            voice0 = s.VoiceVolume;
            pad = new VirtualPad();
            driver = new GameObject("[PadDriver]").AddComponent<PadDriver>();
            driver.pad = pad;
            GameInput.SetBotSource(pad);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SettingsManager s = SettingsManager.Instance;
            if (s != null)
            {
                s.SFXVolume = sfx0;
                s.MusicVolume = music0;
                s.VoiceVolume = voice0;
            }
            if (pad != null)
            {
                pad.ReleaseAll();
                GameInput.ClearBotSource(pad);
            }
            if (driver != null) UnityEngine.Object.Destroy(driver.gameObject);
            MusicDirector.Hold(null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EverySoundEvent_HasClips_AndEveryMusicState_HasAPlaceholder()
        {
            yield return null;
            string[] keys = typeof(Sfx).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string) && f.Name != "Folder")
                .Select(f => (string)f.GetRawConstantValue()).ToArray();
            Assert.Greater(keys.Length, 30, "the events");
            string[] empty = keys.Where(k => !Sfx.Has(k)).ToArray();
            Assert.IsEmpty(empty, "events with no clip: " + string.Join(", ", empty));
            foreach (string state in new[] { "ambient", "tension", "combat", "menu" })
                Assert.IsNotNull(Resources.Load<AudioClip>(MusicDirector.Folder + MusicDirector.Placeholder + "/" + state), "placeholder " + state);
        }

        [UnityTest]
        public IEnumerator TheSliders_ReachEveryChannel()
        {
            SettingsManager s = SettingsManager.Instance;
            s.SFXVolume = 0.5f;
            AudioSource one = Sfx.Play(Sfx.Pickup, lucy.transform.position, 0.8f, 0f);
            Assert.IsNotNull(one);
            Assert.AreEqual(0.4f, one.volume, 0.001f, "a one-shot: 0.8 x the SFX slider");

            // A long source (a gun) follows a slider change.
            var host = new GameObject("BoundSource");
            AudioSource gun = host.AddComponent<AudioSource>();
            GameAudio.Bind(gun, SoundChannel.Sfx, 1f);
            Assert.AreEqual(0.5f, gun.volume, 0.001f);
            s.SFXVolume = 0.2f;
            Assert.AreEqual(0.2f, gun.volume, 0.001f, "the bound source took the new level");
            UnityEngine.Object.Destroy(host);

            s.VoiceVolume = 0.3f;
            Voice.Say(lucy, "test.voice_check", "A test line.", VoiceMode.Radio);
            Assert.IsTrue(Voice.LastHadClip, "the test line has a clip");
            Assert.AreEqual(0.3f, Voice.LastSource.volume, 0.001f, "the Voice slider");

            s.MusicVolume = 0.5f;
            MusicDirector music = MusicDirector.Instance;
            yield return new WaitForSecondsRealtime(MusicDirector.FadeSeconds + 1.5f);
            float expected = 0.5f * (music.CurrentIsPlaceholder ? 0.7f : 1f) * (music.VoiceDucking ? MusicDirector.VoiceDuckLevel : 1f);
            Assert.AreEqual(expected, music.CurrentVolume, 0.02f, "the Music slider");
            s.MusicVolume = 0f;
            yield return null;
            Assert.AreEqual(0f, music.CurrentVolume, 0.001f, "music off");
        }

        [UnityTest]
        public IEnumerator ThePause_StopsTheWorldSounds_ButNotTheMusic_AndTheMenusClick()
        {
            yield return new WaitForSecondsRealtime(0.5f);
            int opens = GameAudio.PlayedCount(Sfx.UiOpen);
            GameManager.Instance.Pause();
            yield return null;
            Assert.IsTrue(AudioListener.pause, "the pause stops the world sounds");
            Assert.AreEqual(opens + 1, GameAudio.PlayedCount(Sfx.UiOpen), "the pause opens with a sound");
            MusicDirector music = MusicDirector.Instance;
            AudioSource track = music.GetComponentsInChildren<AudioSource>().First(a => a.clip == music.CurrentClip);
            Assert.IsTrue(track.ignoreListenerPause && track.isPlaying, "the music plays on");

            GameManager.Instance.Resume();
            yield return null;
            Assert.IsFalse(AudioListener.pause, "the resume starts them again");

            // A button of its own: a menu button could restart the room.
            var buttonObject = new GameObject("TestButton", typeof(RectTransform), typeof(Image), typeof(Button));
            Button button = buttonObject.GetComponent<Button>();
            AudioHost.Instance.Ui.Hook();
            int confirms = GameAudio.PlayedCount(Sfx.UiConfirm);
            button.onClick.Invoke();
            Assert.AreEqual(confirms + 1, GameAudio.PlayedCount(Sfx.UiConfirm), "a button press clicks");
            UnityEngine.Object.Destroy(buttonObject);

            GameManager.Instance.OpenInventory();
            yield return null;
            Assert.IsTrue(AudioListener.pause, "the inventory stops them too");
            GameManager.Instance.CloseInventory();
            yield return null;
            Assert.IsFalse(AudioListener.pause);
        }

        [UnityTest]
        public IEnumerator TheEars_AreOnLucy_AndStayThereThroughACameraCut()
        {
            yield return new WaitForSecondsRealtime(0.6f);
            AudioListener[] on = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Where(l => l.isActiveAndEnabled).ToArray();
            Assert.AreEqual(1, on.Length, "one listener");
            Assert.AreEqual(AudioHost.Instance.Ears.Listener, on[0], "hers, not the camera's");
            Assert.Less(Vector3.Distance(on[0].transform.position, lucy.transform.position + Vector3.up * ListenerRig.HeadHeight), 0.05f, "at her head");

            FixedCameraDirector director = FixedCameraDirector.Instance;
            string before = director.CurrentName;
            tank.Teleport(new Vector3(3f, 1f, 3f), 180f);
            yield return WaitUntil(() => director.CurrentName != before, 1.5f);
            yield return null;
            Assert.AreNotEqual(before, director.CurrentName, "a cut");
            Assert.Less(Vector3.Distance(on[0].transform.position, lucy.transform.position + Vector3.up * ListenerRig.HeadHeight), 0.05f, "still at her head after the cut");
            Assert.AreEqual(Camera.main.transform.eulerAngles.y, on[0].transform.eulerAngles.y, 0.5f, "turned like the new camera");
        }

        [UnityTest]
        public IEnumerator TheMusic_IsTheRoomsAmbient_ThenCombatOnAnAim_ThenAmbientAgain()
        {
            MusicDirector music = MusicDirector.Instance;
            yield return WaitUntil(() => music.State == MusicState.Ambient && !music.Crossfading, 5f);
            Assert.AreEqual(MusicState.Ambient, music.State);
            Assert.AreEqual("TrainingRoom", music.RoomKey);
            Assert.IsFalse(music.CurrentIsPlaceholder, "the training room has its own ambient track");
            Assert.IsNotNull(music.RoomToneClip, "the room tone under it");

            DummyShooter shooter = GameObject.Find("Dummy_A").GetComponent<DummyShooter>();
            shooter.enabled = true;
            yield return WaitUntil(() => music.State == MusicState.Combat, 8f);
            Assert.AreEqual(MusicState.Combat, music.State, "an aim at her starts the combat music");
            Assert.IsTrue(music.CurrentIsPlaceholder, "no combat track for the room: the placeholder");
            Assert.AreEqual("combat", music.CurrentClip.name);

            foreach (DummyShooter s in UnityEngine.Object.FindObjectsByType<DummyShooter>(FindObjectsSortMode.None)) s.enabled = false;
            yield return WaitUntil(() => music.State == MusicState.Ambient, MusicDirector.CombatHoldSeconds + 8f);
            Assert.AreEqual(MusicState.Ambient, music.State, "the hold ends, the ambient comes back");

            MusicDirector.Hold(MusicState.Silent);
            yield return new WaitForSecondsRealtime(MusicDirector.FadeSeconds + 0.3f);
            Assert.IsNull(music.CurrentClip, "a cutscene holds the silence");
        }

        [UnityTest]
        public IEnumerator AVoiceLine_PlaysItsClip_OrShowsItsSubtitleAlone_AndThePaChimesFirst()
        {
            yield return null;
            GameObject dummy = GameObject.Find("Dummy_B");
            AudioSource source = Voice.Say(dummy, "test.voice_check", "A test line.");
            Assert.IsNotNull(source, "the clip plays");
            Assert.AreEqual(1f, source.spatialBlend, "a world line is 3D, at the speaker");
            Assert.IsTrue(SubtitleService.Lines.Any(l => l.Text == "A test line."), "with its subtitle");
            Assert.IsTrue(MusicDirector.Instance.VoiceDucking, "the music ducks under it");

            int said = Voice.Said;
            Bark.Say(dummy, "Nobody recorded this one.");
            string id = Voice.IdFor(dummy, "Nobody recorded this one.");
            Assert.AreEqual(said + 1, Voice.Said, "a bark goes through the voice player");
            Assert.IsFalse(Voice.LastHadClip);
            Assert.Contains(id, Voice.Missing.ToList(), "the line to record");
            Assert.IsTrue(SubtitleService.Lines.Any(l => l.Text == "Nobody recorded this one."), "the subtitle alone");

            int chimes = GameAudio.PlayedCount(Sfx.PaChime);
            Voice.PA("pa.test", "Code silver.");
            Assert.AreEqual(chimes + 1, GameAudio.PlayedCount(Sfx.PaChime), "the chime first");
            Assert.IsFalse(SubtitleService.Lines.Any(l => l.Text == "Code silver."), "the words after the chime");
            yield return new WaitForSeconds(Voice.PaDelay + 0.2f);
            Assert.IsTrue(SubtitleService.Lines.Any(l => l.Speaker == Voice.PaSpeaker && l.Text == "Code silver."), "PA: the words");
        }

        [UnityTest]
        public IEnumerator HerSteps_HerDodge_AndHerPowers_MakeSound()
        {
            stats.RegenPaused = true;
            stats.DebugSet(45f, 0f);
            Footsteps steps = lucy.GetComponent<Footsteps>();
            Assert.IsNotNull(steps, "her steps");
            Assert.AreEqual(Sfx.StepLucy, steps.Key, "bare feet");
            tank.Teleport(new Vector3(-3f, 1f, -2f), 0f);
            yield return new WaitForSeconds(0.3f);
            int n0 = steps.Steps;
            pad.SetHeld(InputManager.GameAction.Forward, true);
            yield return new WaitForSeconds(2f);
            pad.ReleaseAll();
            Assert.GreaterOrEqual(steps.Steps - n0, 2, "a step each stride");

            int dodges = GameAudio.PlayedCount(Sfx.Dodge);
            pad.Tap(InputManager.GameAction.Dodge);
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(dodges + 1, GameAudio.PlayedCount(Sfx.Dodge), "the dodge");

            tank.Teleport(new Vector3(0f, 1f, 0f), 0f);
            yield return new WaitForSeconds(0.6f);
            powers.Select(PowerKind.Blast);
            PowerSounds sounds = lucy.GetComponent<PowerSounds>();
            Assert.IsNotNull(sounds);
            int blasts = GameAudio.PlayedCount(Sfx.Blast);
            pad.SetHeld(InputManager.GameAction.Charge, true);
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(sounds.Humming, "the charge hums");
            float low = sounds.HumPitch;
            yield return new WaitForSeconds(0.6f);
            Assert.Greater(sounds.HumPitch, low, "the hum rises with the charge");
            pad.ReleaseAll();
            yield return new WaitForSeconds(0.2f);
            Assert.IsFalse(sounds.Humming, "the release ends the hum");
            Assert.AreEqual(blasts + 1, GameAudio.PlayedCount(Sfx.Blast), "mk_warspite's sound");
            Assert.Greater(GameAudio.PlayedCount(Sfx.FleshHit), 0, "the hit on the dummy");
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
