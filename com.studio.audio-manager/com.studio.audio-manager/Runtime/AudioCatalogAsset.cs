using System;
using System.Collections.Generic;
using UnityEngine;

namespace Studio.Audio
{
    /// La lista de TODOS los sonidos y pistas de música de un proyecto. Es la única "fuente de verdad":
    /// de aquí salen los assets de cada evento, el archivo AudioIds.cs y la guía para el equipo de sonido.
    /// Se edita en el Inspector o en una hoja de cálculo (Exportar / Importar CSV).
    [CreateAssetMenu(menuName = "Audio Manager/Catalog", fileName = "AudioCatalog")]
    public class AudioCatalogAsset : ScriptableObject
    {
        [Serializable]
        public class EventDef
        {
            [Tooltip("Identificador del evento (sin espacios). Es el nombre que usa el código y el nombre de los archivos de audio.")]
            public string id;
            public AudioBus bus = AudioBus.Sfx;
            [TextArea(1, 3)] public string description;
            [Range(-40f, 12f)] public float volumeDb;
            [Tooltip("Tiempo mínimo entre dos reproducciones.")]
            public float cooldown;
            [Tooltip("Copias que pueden sonar a la vez (0 = sin límite).")]
            public int maxInstances;
            public bool loop;
            [Tooltip("Si suena cuando la ventana del juego no tiene el foco.")]
            public bool playInBackground = true;
            [Range(-24f, 0f)] public float duckMusicDb;
            public float duckSeconds = 1f;
            [Tooltip("0 = el más importante, 255 = el menos.")]
            [Range(0, 255)] public int priority = 128;
        }

        [Serializable]
        public class TrackDef
        {
            public string id;
            [TextArea(1, 3)] public string description;
        }

        public List<EventDef> events = new List<EventDef>();
        public List<TrackDef> tracks = new List<TrackDef>();

        [TextArea(3, 12)]
        [Tooltip("Texto extra que se añade al final de la guía para el equipo de sonido (reglas propias del proyecto).")]
        public string guideNotes;
    }
}
