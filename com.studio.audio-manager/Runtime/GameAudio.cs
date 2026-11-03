namespace Studio.Audio
{
    /// Puerta de entrada al audio para el resto del juego. Se llama siempre así:
    ///     GameAudio.Play(AudioIds.UI_Click);
    /// Si no hay AudioManager (p. ej. al probar la escena Main sin pasar por Boot), no hace nada.
    public static class GameAudio
    {
        public static AudioHandle Play(string id, float volume = 1f) =>
            AudioManager.Instance != null ? AudioManager.Instance.Play(id, volume) : AudioHandle.None;

        /// ¿Este evento ya tiene algún sonido?
        public static bool Has(string id) => AudioManager.Instance != null && AudioManager.Instance.HasSound(id);

        public static void Stop(AudioHandle handle, float fadeSeconds = -1f)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.Stop(handle, fadeSeconds);
        }

        public static void SetVolume(AudioHandle handle, float scale)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.SetVolume(handle, scale);
        }

        /// ¿Esta pista de música ya tiene audio?
        public static bool HasMusic(string id) => AudioManager.Instance != null && AudioManager.Instance.HasMusic(id);

        public static bool IsPlaying(AudioHandle handle) =>
            AudioManager.Instance != null && AudioManager.Instance.IsPlaying(handle);

        public static void Music(string trackId)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayMusic(trackId);
        }

        public static void StopMusic(float fadeSeconds = 1f)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.StopMusic(fadeSeconds);
        }
    }
}
