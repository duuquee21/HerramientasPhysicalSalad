using System.Collections.Generic;
using UnityEngine;

namespace Studio.Audio.Samples
{
    /// Elige la música según el estado del juego. El resto del juego solo dice qué estados están activos:
    ///     StateMusicDirector.SetState("combate", true);
    /// y este director decide qué pista suena: la primera regla (de arriba abajo) cuyo estado esté activo
    /// y cuya pista tenga audio. Si ninguna, suena la pista por defecto. Una pista sin audio todavía se salta,
    /// así que el juego nunca se queda sin música mientras se produce.
    /// Colócalo en un objeto de la escena inicial.
    public class StateMusicDirector : MonoBehaviour
    {
        [System.Serializable]
        public class Rule
        {
            [Tooltip("Nombre del estado, por ejemplo: combate, menu, tienda.")]
            public string state;
            public string trackId;
        }

        public static StateMusicDirector Instance { get; private set; }

        [Tooltip("Por orden de prioridad: gana la primera regla cuyo estado esté activo y cuya pista tenga audio.")]
        [SerializeField] List<Rule> rules = new List<Rule>();
        [SerializeField] string defaultTrack;

        readonly HashSet<string> active = new HashSet<string>();

        public static void SetState(string state, bool isActive)
        {
            if (Instance == null) return;

            if (isActive) Instance.active.Add(state);
            else Instance.active.Remove(state);

            Instance.Evaluate();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start() => Evaluate();

        void Evaluate()
        {
            foreach (var rule in rules)
            {
                if (active.Contains(rule.state) && GameAudio.HasMusic(rule.trackId))
                {
                    GameAudio.Music(rule.trackId);
                    return;
                }
            }

            if (!string.IsNullOrEmpty(defaultTrack) && GameAudio.HasMusic(defaultTrack))
                GameAudio.Music(defaultTrack);
        }
    }
}
