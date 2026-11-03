using System.Collections.Generic;
using UnityEngine;

namespace Studio.Audio
{
    /// La biblioteca con todos los eventos de sonido y todas las pistas de música.
    /// La rellena sola la herramienta Tools > Audio Manager > 1. Crear o completar los sonidos del catálogo.
    [CreateAssetMenu(menuName = "Audio Manager/Library", fileName = "AudioLibrary")]
    public class AudioLibrary : ScriptableObject
    {
        public SoundEvent[] events;
        public MusicTrack[] tracks;

        Dictionary<string, SoundEvent> eventById;
        Dictionary<string, MusicTrack> trackById;

        public bool TryGetEvent(string id, out SoundEvent soundEvent)
        {
            Build();
            return eventById.TryGetValue(id, out soundEvent);
        }

        public bool TryGetTrack(string id, out MusicTrack track)
        {
            Build();
            return trackById.TryGetValue(id, out track);
        }

        void OnValidate()
        {
            eventById = null;
            trackById = null;
        }

        void Build()
        {
            if (eventById != null) return;

            eventById = new Dictionary<string, SoundEvent>();
            trackById = new Dictionary<string, MusicTrack>();

            if (events != null)
            {
                foreach (var e in events)
                {
                    if (e == null || string.IsNullOrEmpty(e.eventId)) continue;
                    if (!eventById.TryAdd(e.eventId, e))
                        Debug.LogError($"[Audio] Evento duplicado: '{e.eventId}'.", e);
                }
            }

            if (tracks != null)
            {
                foreach (var t in tracks)
                {
                    if (t == null || string.IsNullOrEmpty(t.trackId)) continue;
                    if (!trackById.TryAdd(t.trackId, t))
                        Debug.LogError($"[Audio] Pista duplicada: '{t.trackId}'.", t);
                }
            }
        }
    }
}
