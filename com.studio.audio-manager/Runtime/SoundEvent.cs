using UnityEngine;

namespace Studio.Audio
{
    public enum ClipPlayMode
    {
        [Tooltip("Al azar, sin repetir el anterior.")] RandomNoRepeat,
        [Tooltip("Al azar, puede repetirse.")] Random,
        [Tooltip("En orden: 1, 2, 3, 1, 2, 3...")] Sequential
    }

    /// Un evento de sonido: qué suena y cómo cuando el juego avisa de algo.
    /// Quien hace el audio rellena "Clips" (o deja archivos con el nombre del evento en la carpeta Audio).
    [CreateAssetMenu(menuName = "Audio Manager/Sound Event", fileName = "SFX_")]
    public class SoundEvent : ScriptableObject
    {
        [Tooltip("Identificador interno. No se cambia: el juego lo usa para encontrar este sonido.")]
        public string eventId;

        [TextArea(2, 4)]
        [Tooltip("Cuándo suena este evento.")]
        public string description;

        public AudioBus bus = AudioBus.Sfx;

        [Header("Sonidos (arrastra aquí los archivos de audio)")]
        public AudioClip[] clips;
        public ClipPlayMode playMode = ClipPlayMode.RandomNoRepeat;

        [Header("Volumen y tono")]
        [Range(-40f, 12f)] public float volumeDb = 0f;
        [Tooltip("Cuánto varía el volumen al azar en cada reproducción (± dB).")]
        [Range(0f, 6f)] public float volumeVariationDb = 0f;
        [Range(-12f, 12f)] public float pitchSemitones = 0f;
        [Tooltip("Cuánto varía el tono al azar en cada reproducción (± semitonos).")]
        [Range(0f, 4f)] public float pitchVariationSemitones = 0f;
        [Range(-1f, 1f)] public float pan = 0f;

        [Header("Comportamiento")]
        [Tooltip("El sonido se repite hasta que el juego lo para.")]
        public bool loop;
        public float fadeInSeconds;
        [Tooltip("Fundido al pararlo (solo aplica cuando el juego lo para, p. ej. un bucle).")]
        public float fadeOutSeconds;
        public float delaySeconds;
        [Tooltip("Tiempo mínimo entre dos reproducciones de este evento.")]
        public float cooldownSeconds;
        [Tooltip("Cuántas copias de este evento pueden sonar a la vez (0 = sin límite). Si se supera, se corta la más antigua.")]
        public int maxInstances;
        [Tooltip("0 = el más importante, 255 = el menos. Si hay demasiados sonidos a la vez, se descartan los menos importantes.")]
        [Range(0, 255)] public int priority = 128;
        [Tooltip("Si no está marcado, no suena cuando el jugador está en otro programa.")]
        public bool playInBackground = true;

        [Header("Bajar la música mientras suena")]
        [Range(-24f, 0f)] public float duckMusicDb = 0f;
        public float duckSeconds = 1f;

        public bool HasClips
        {
            get
            {
                if (clips == null) return false;
                foreach (var clip in clips) if (clip != null) return true;
                return false;
            }
        }
    }
}
