namespace Game50033
{
    using UnityEngine;
    using UnityEngine.Audio;

    /// <summary>
    /// The player's saved volume levels (0-1 per channel), applied to the Crescendo mixer's exposed
    /// volume parameters and stored in PlayerPrefs so they survive between sessions. 1 keeps the mixer's
    /// authored level for that group; lower values turn it down from there.
    /// SFX covers both the Gameplay SFX and UI mixer groups.
    /// </summary>
    public static class VolumeSettings
    {
        public enum Channel { Master, Music, Sfx }

        // exposed parameter names on Assets/Gameplay/Audio/Crescendo.mixer
        private const string MasterParameter = "MasterVolume";
        private const string MusicParameter = "MusicVolume";
        private const string SfxParameter = "SFXVolume";
        private const string UiParameter = "UIVolume";

        private const float SilentDecibels = -80f;

        public static float Get(Channel channel) => Mathf.Clamp01(PlayerPrefs.GetFloat(Key(channel), 1f));

        public static void Set(AudioMixer mixer, Channel channel, float value)
        {
            value = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(Key(channel), value);
            Apply(mixer, channel, value);
        }

        /// <summary>Pushes every saved level into the mixer, e.g. when the game starts.</summary>
        public static void ApplyAll(AudioMixer mixer)
        {
            foreach (Channel channel in System.Enum.GetValues(typeof(Channel))) Apply(mixer, channel, Get(channel));
        }

        public static void Save() => PlayerPrefs.Save();

        private static void Apply(AudioMixer mixer, Channel channel, float value)
        {
            if (mixer == null) return;
            switch (channel)
            {
                case Channel.Master: SetRelative(mixer, MasterParameter, value); break;
                case Channel.Music: SetRelative(mixer, MusicParameter, value); break;
                case Channel.Sfx:
                    SetRelative(mixer, SfxParameter, value);
                    SetRelative(mixer, UiParameter, value);
                    break;
            }
        }

        // full volume keeps the mixer's authored level (the sound designer's mix); lower values reduce from it.
        // the authored level is read once per play session, before anything here has changed it.
        private static readonly System.Collections.Generic.Dictionary<string, float> AuthoredDecibels = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAuthoredLevels() => AuthoredDecibels.Clear();

        private static void SetRelative(AudioMixer mixer, string parameter, float value)
        {
            if (!AuthoredDecibels.TryGetValue(parameter, out var authored))
            {
                if (!mixer.GetFloat(parameter, out authored)) return;
                AuthoredDecibels[parameter] = authored;
            }
            // a linear slider maps to decibels so half the slider sounds about half as loud.
            var decibels = value <= 0.0001f ? SilentDecibels : authored + Mathf.Log10(value) * 20f;
            mixer.SetFloat(parameter, decibels);
        }

        private static string Key(Channel channel) => "Crescendo.Volume." + channel;
    }
}
