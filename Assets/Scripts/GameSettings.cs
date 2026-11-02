using UnityEngine;

namespace SumoGame
{
    /// <summary>Player options, saved between sessions.</summary>
    public static class GameSettings
    {
        static readonly int[] RoundChoices = { 1, 2, 3 };
        const int VolumeStep = 25;

        public static bool Fullscreen { get; private set; }
        public static bool VSync { get; private set; }
        public static bool CameraShake { get; private set; }
        public static int VersusRounds { get; private set; }
        public static int VolumePercent { get; private set; }

        public static int BestRun
        {
            get => PlayerPrefs.GetInt("BestRun", 0);
            set
            {
                PlayerPrefs.SetInt("BestRun", value);
                PlayerPrefs.Save();
            }
        }

        public static void Load()
        {
            Fullscreen = PlayerPrefs.GetInt("Fullscreen", 0) == 1;
            VSync = PlayerPrefs.GetInt("VSync", 1) == 1;
            CameraShake = PlayerPrefs.GetInt("CameraShake", 1) == 1;
            VersusRounds = PlayerPrefs.GetInt("VersusRoundsToWin", 2);
            VolumePercent = PlayerPrefs.GetInt("VolumePercent", 75);
            Apply();
        }

        public static void ToggleFullscreen()
        {
            Fullscreen = !Fullscreen;
            Save();
        }

        public static void ToggleVSync()
        {
            VSync = !VSync;
            Save();
        }

        public static void ToggleCameraShake()
        {
            CameraShake = !CameraShake;
            Save();
        }

        public static void CycleVolume(int direction)
        {
            VolumePercent = (VolumePercent + direction * VolumeStep + 125) % 125;
            Save();
        }

        public static void CycleVersusRounds(int direction)
        {
            var index = System.Array.IndexOf(RoundChoices, VersusRounds);
            var count = RoundChoices.Length;
            VersusRounds = RoundChoices[(index + direction + count) % count];
            Save();
        }

        static void Save()
        {
            PlayerPrefs.SetInt("Fullscreen", Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt("VSync", VSync ? 1 : 0);
            PlayerPrefs.SetInt("CameraShake", CameraShake ? 1 : 0);
            PlayerPrefs.SetInt("VersusRoundsToWin", VersusRounds);
            PlayerPrefs.SetInt("VolumePercent", VolumePercent);
            PlayerPrefs.Save();
            Apply();
        }

        static void Apply()
        {
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            AudioListener.volume = VolumePercent / 100f;
            if (Screen.fullScreen != Fullscreen)
                Screen.fullScreenMode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        }
    }
}
