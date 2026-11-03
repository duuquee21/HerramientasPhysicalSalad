using UnityEngine;

namespace Studio.Audio
{
    /// Una pista de música. Puede tener una intro (suena una vez) y un bucle.
    [CreateAssetMenu(menuName = "Audio Manager/Music Track", fileName = "MUS_")]
    public class MusicTrack : ScriptableObject
    {
        [Tooltip("Identificador interno. No se cambia.")]
        public string trackId;

        [TextArea(2, 4)] public string description;

        [Header("Música")]
        [Tooltip("Opcional: suena una vez antes del bucle.")]
        public AudioClip introClip;
        [Tooltip("El bucle principal (o la pista entera, si no se repite).")]
        public AudioClip loopClip;
        public bool loops = true;

        [Header("Mezcla")]
        [Range(-40f, 6f)] public float volumeDb = -6f;
        [Tooltip("Segundos de fundido cuando entra esta pista (y sale la anterior).")]
        public float crossfadeSeconds = 2f;
        [Tooltip("Si no está marcado, calla cuando el jugador está en otro programa.")]
        public bool playInBackground = false;

        public bool HasAudio => introClip != null || loopClip != null;
    }
}
