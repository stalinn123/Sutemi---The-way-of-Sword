using Kendo.Campaign;
using UnityEngine;

namespace Kendo.Game
{
    /// <summary>Music, ambience, sound effects and kiai voices with the player's volume settings.</summary>
    public sealed class AudioService : MonoBehaviour
    {
        private AudioSource music, ambience, ui;
        private AudioSource[] pool;
        private int next;
        private KendoGameConfig cfg;
        private PlayerSettings settings;
        private AudioClip pendingMusic;
        private float fade = 1f;

        public void Init(KendoGameConfig config, PlayerSettings s)
        {
            cfg = config;
            settings = s;
            music = gameObject.AddComponent<AudioSource>();
            music.loop = true;
            music.spatialBlend = 0f;
            ambience = gameObject.AddComponent<AudioSource>();
            ambience.loop = true;
            ambience.spatialBlend = 0f;
            ui = gameObject.AddComponent<AudioSource>();
            ui.spatialBlend = 0f;
            pool = new AudioSource[12];
            for (int i = 0; i < pool.Length; i++)
            {
                var go = new GameObject("Sfx" + i);
                go.transform.SetParent(transform, false);
                var a = go.AddComponent<AudioSource>();
                a.spatialBlend = 1f;
                a.minDistance = 1.5f;
                a.maxDistance = 30f;
                a.rolloffMode = AudioRolloffMode.Linear;
                pool[i] = a;
            }
            Apply(s);
        }

        public void Apply(PlayerSettings s)
        {
            settings = s;
            AudioListener.volume = s.masterVolume;
            if (music != null) music.volume = s.musicVolume * fade;
            if (ambience != null) ambience.volume = s.sfxVolume * 0.5f;
        }

        public void PlayMusic(AudioClip clip)
        {
            if (music == null || clip == null || music.clip == clip) return;
            pendingMusic = clip;
        }

        public void PlayAmbience(AudioClip clip)
        {
            if (ambience == null) return;
            if (clip == null) { ambience.Stop(); return; }
            if (ambience.clip == clip && ambience.isPlaying) return;
            ambience.clip = clip;
            ambience.Play();
        }

        private void Update()
        {
            if (music == null || settings == null) return;
            if (pendingMusic != null)
            {
                fade = Mathf.MoveTowards(fade, 0f, Time.unscaledDeltaTime * 1.5f);
                if (fade <= 0f || !music.isPlaying)
                {
                    music.clip = pendingMusic;
                    pendingMusic = null;
                    music.Play();
                }
            }
            else fade = Mathf.MoveTowards(fade, 1f, Time.unscaledDeltaTime);
            music.volume = settings.musicVolume * fade;
        }

        public void Sfx(AudioClip clip, Vector3 at, float volume = 1f, float pitch = 1f)
        {
            if (clip == null || pool == null || settings == null) return;
            var a = pool[next];
            next = (next + 1) % pool.Length;
            a.transform.position = at;
            a.pitch = pitch;
            a.PlayOneShot(clip, volume * settings.sfxVolume);
        }

        public void Voice(AudioClip clip, Vector3 at, float pitch = 1f)
        {
            if (clip == null || pool == null || settings == null) return;
            var a = pool[next];
            next = (next + 1) % pool.Length;
            a.transform.position = at;
            a.pitch = pitch;
            a.PlayOneShot(clip, settings.voiceVolume);
        }

        public void Ui(bool confirm)
        {
            if (ui == null || cfg == null || settings == null) return;
            var c = confirm ? cfg.uiConfirm : cfg.uiClick;
            if (c != null) ui.PlayOneShot(c, settings.sfxVolume * 0.6f);
        }

        public void Stinger(AudioClip clip)
        {
            if (ui == null || clip == null || settings == null) return;
            ui.PlayOneShot(clip, settings.sfxVolume);
        }
    }
}
