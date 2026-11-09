using System.Collections.Generic;
using UnityEngine;

namespace SumoGame
{
    /// <summary>Fight sounds synthesized at start-up, the recorded drum that opens each match, and the music.</summary>
    public class SumoAudio : MonoBehaviour
    {
        const int SampleRate = 44100;
        const float ClapPitch = 1900f;
        const float MusicVolume = 0.6f;

        readonly Dictionary<SumoImpact, AudioClip> clips = new Dictionary<SumoImpact, AudioClip>();
        AudioClip clap;
        AudioClip matchStart;
        AudioClip menuMusic;
        AudioClip[] fightMusic;
        AudioClip chosenFight;
        AudioSource source;
        AudioSource music;

        void Awake() => Setup();

        /// <summary>Also runs on first use, because the game may ask for music before this Awake.</summary>
        void Setup()
        {
            if (source != null) return;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;

            foreach (SumoImpact impact in System.Enum.GetValues(typeof(SumoImpact)))
                clips[impact] = ToClip(impact.ToString(), Synthesize(impact));
            clap = ToClip("Clap", Tone(0.09f, ClapPitch, 0.5f, 0.6f, 90f));
            matchStart = Resources.Load<AudioClip>("Audio/MatchStartDrum");

            music = gameObject.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.spatialBlend = 0f;
            music.loop = true;
            music.volume = MusicVolume;
            menuMusic = Resources.Load<AudioClip>("Audio/Music/Pink");
            fightMusic = new[] { Resources.Load<AudioClip>("Audio/Music/VioletTactics"), Resources.Load<AudioClip>("Audio/Music/PetalDance") };
        }

        public void Play(SumoImpact impact) => source.PlayOneShot(clips[impact]);

        public void PlayClap() => source.PlayOneShot(clap);

        public void PlayMatchStart()
        {
            Setup();
            source.PlayOneShot(matchStart);
        }

        public void PlayMenuMusic()
        {
            Setup();
            if (music.isPlaying && music.clip == menuMusic) return;
            PlayMusic(menuMusic);
        }

        /// <summary>Picks one of the fight tracks at random for the coming match. It plays once the fight starts.</summary>
        public void ChooseFightMusic()
        {
            Setup();
            chosenFight = fightMusic[Random.Range(0, fightMusic.Length)];
        }

        public void PlayFightMusic()
        {
            Setup();
            PlayMusic(chosenFight != null ? chosenFight : fightMusic[0]);
        }

        public void StopMusic()
        {
            Setup();
            music.Stop();
        }

        public void PauseMusic(bool paused)
        {
            Setup();
            if (paused) music.Pause();
            else music.UnPause();
        }

        public AudioClip CurrentMusic => music != null && music.isPlaying ? music.clip : null;

        void PlayMusic(AudioClip clip)
        {
            music.clip = clip;
            music.Play();
        }

        static AudioClip ToClip(string name, float[] samples)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public static float[] Synthesize(SumoImpact impact)
        {
            switch (impact)
            {
                case SumoImpact.Strike: return Tone(0.12f, 620f, 0.25f, 0.85f, 38f);
                case SumoImpact.Grab: return Tone(0.2f, 120f, 0.8f, 0.35f, 18f);
                case SumoImpact.Shove: return Tone(0.16f, 90f, 0.7f, 0.2f, 22f);
                case SumoImpact.Drive: return Tone(0.45f, 70f, 0.9f, 0.3f, 7f);
                case SumoImpact.Whiff: return Tone(0.28f, 240f, 0.05f, 0.5f, 9f);
                case SumoImpact.Ducked: return Tone(0.22f, 320f, 0.05f, 0.45f, 12f);
                case SumoImpact.Clash: return Tone(0.16f, 180f, 0.6f, 0.5f, 26f);
                default: return Tone(0.6f, 55f, 0.95f, 0.45f, 5f);
            }
        }

        /// <summary>A decaying mix of a falling tone and noise, faded to silence at the end.</summary>
        static float[] Tone(float seconds, float pitch, float toneLevel, float noiseLevel, float decay)
        {
            var random = new System.Random(pitch.GetHashCode());
            var samples = new float[(int)(seconds * SampleRate)];
            var phase = 0f;

            for (var i = 0; i < samples.Length; i++)
            {
                var t = i / (float)SampleRate;
                var progress = i / (float)samples.Length;
                phase += 2f * Mathf.PI * pitch * (1f - progress * 0.6f) / SampleRate;

                var noise = (float)random.NextDouble() * 2f - 1f;
                var envelope = Mathf.Exp(-t * decay) * (1f - progress);
                samples[i] = Mathf.Clamp((Mathf.Sin(phase) * toneLevel + noise * noiseLevel) * envelope, -1f, 1f);
            }
            return samples;
        }
    }
}
