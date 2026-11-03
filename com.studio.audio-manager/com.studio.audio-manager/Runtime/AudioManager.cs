using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Studio.Audio
{
    /// Motor de audio del juego. Colócalo en un objeto de la escena Boot.
    ///   - El juego pide sonidos por id de evento (ver GameAudio y AudioIds); este motor decide cómo suenan
    ///     a partir de los datos de cada SoundEvent.
    ///   - Reserva fija de voces (AudioSource), con prioridad, límite de copias y tiempo mínimo entre repeticiones.
    ///   - Cuatro bancos de volumen más el maestro, guardados entre sesiones.
    ///   - Música con fundido cruzado, intro opcional y bajada automática de la música (ducking).
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Datos")]
        [Tooltip("La biblioteca con todos los eventos y pistas (AudioLibrary).")]
        [SerializeField] AudioLibrary library;

        [Header("Voces")]
        [Tooltip("Sonidos que pueden sonar a la vez. Si se llena, se descarta o se roba el menos importante.")]
        [SerializeField, Range(8, 64)] int voiceCount = 24;

        [Header("Mezclador (opcional)")]
        [Tooltip("Grupos de un AudioMixer para aplicar efectos por banco. Si se dejan vacíos, no se usa ningún mezclador.")]
        [SerializeField] AudioMixerGroup musicGroup;
        [SerializeField] AudioMixerGroup sfxGroup;
        [SerializeField] AudioMixerGroup uiGroup;
        [SerializeField] AudioMixerGroup ambienceGroup;

        [Header("Ventana en segundo plano")]
        [Tooltip("Silencia TODO el audio cuando la ventana del juego no tiene el foco. Cada evento tiene además su propia casilla.")]
        [SerializeField] bool silenceWhenUnfocused = false;
        [Tooltip("En el Editor el 'foco' cambia al hacer clic en el Inspector u otras ventanas, y eso silenciaría la música y los "
                 + "sonidos que no suenan en segundo plano. Desmarcado (recomendado): en el Editor se ignora el foco. "
                 + "Márcalo solo para probar el comportamiento real con la ventana sin foco.")]
        [SerializeField] bool respectFocusInEditor = false;

        [Header("Música")]
        [Tooltip("Pista que suena al empezar la partida (id de la pista). Déjalo vacío para no poner música al arrancar.")]
        [SerializeField] string musicOnStart = "";

        [Header("Pruebas (menú de los tres puntos del componente, en Play)")]
        [SerializeField] string testEventId = "";
        [SerializeField] string testTrackId = "";

        const string KeyMaster = "audio_master";
        const string KeyBus = "audio_bus_";

        class EventState
        {
            public float LastPlay = -999f;
            public int LastIndex = -1;
            public int Sequence;
        }

        class Voice
        {
            public AudioSource Source;
            public SoundEvent Event;
            public int Id;
            public float EventVolume, UserScale = 1f;
            public float Fade = 1f, FadeTarget = 1f, FadeSeconds;
            public float StartAt;
            public bool Active, Stopping;
        }

        class Deck
        {
            public AudioSource Intro, Main;
            public MusicTrack Track;
            public float Level, Target, FadeSeconds = 1f;
        }

        readonly Dictionary<SoundEvent, EventState> states = new Dictionary<SoundEvent, EventState>();
        readonly HashSet<string> warned = new HashSet<string>();
        readonly float[] busVolume = { 0.7f, 1f, 1f, 0.8f };   // Music, Sfx, Ui, Ambience

        Voice[] voices;
        Deck[] decks;
        int activeDeck;
        MusicTrack currentTrack;
        int nextId = 1;

        float duck = 1f, duckGoal = 1f, duckUntil;

        public float MasterVolume { get; private set; } = 1f;

        /// El foco que cuenta para el audio: el real, salvo en el Editor (donde se ignora, a menos que se pida respetarlo).
        bool HasFocus => Application.isFocused || (Application.isEditor && !respectFocusInEditor);

        float FocusFactor => silenceWhenUnfocused && !HasFocus ? 0f : 1f;

        // ───────────── Ciclo de vida ─────────────
        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadSettings();
            BuildVoices();
            BuildDecks();

            if (library == null)
                Debug.LogWarning("[Audio] Falta la AudioLibrary en el AudioManager: no sonará nada.", this);
        }

        void Start()
        {
            if (Instance != this) return;
            if (!string.IsNullOrEmpty(musicOnStart)) PlayMusic(musicOnStart);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            UpdateDuck(dt);
            UpdateVoices(dt);
            UpdateMusic(dt);
        }

        void OnApplicationQuit() => PlayerPrefs.Save();

        // ───────────── Sonidos ─────────────
        /// Reproduce un evento por su id. Devuelve un identificador para poder pararlo o cambiarle el volumen.
        public AudioHandle Play(string id, float volumeScale = 1f)
        {
            if (string.IsNullOrEmpty(id)) return AudioHandle.None;

            if (library == null || !library.TryGetEvent(id, out SoundEvent soundEvent))
            {
                WarnOnce("evt:" + id, $"[Audio] Evento de sonido desconocido: '{id}'.");
                return AudioHandle.None;
            }

            return Play(soundEvent, volumeScale);
        }

        public AudioHandle Play(SoundEvent ev, float volumeScale = 1f)
        {
            // Sin sonidos todavía: silencio (es normal mientras se produce el audio)
            if (ev == null || !ev.HasClips) return AudioHandle.None;
            if (!ev.playInBackground && !HasFocus) return AudioHandle.None;

            EventState state = GetState(ev);
            float now = Time.unscaledTime;
            if (ev.cooldownSeconds > 0f && now - state.LastPlay < ev.cooldownSeconds) return AudioHandle.None;

            AudioClip clip = PickClip(ev, state);
            if (clip == null) return AudioHandle.None;

            // Límite de copias del mismo evento: se corta la más antigua
            if (ev.maxInstances > 0 && CountActive(ev) >= ev.maxInstances)
            {
                Voice oldest = OldestActive(ev);
                if (oldest == null) return AudioHandle.None;
                StopVoice(oldest, 0.03f);
            }

            Voice voice = AcquireVoice(ev.priority);
            if (voice == null) return AudioHandle.None;

            state.LastPlay = now;

            float volume = Db(ev.volumeDb + Random.Range(-ev.volumeVariationDb, ev.volumeVariationDb));
            float semitones = ev.pitchSemitones + Random.Range(-ev.pitchVariationSemitones, ev.pitchVariationSemitones);

            voice.Event = ev;
            voice.Id = nextId++;
            voice.EventVolume = volume;
            voice.UserScale = Mathf.Max(0f, volumeScale);
            voice.Stopping = false;
            voice.Fade = ev.fadeInSeconds > 0f ? 0f : 1f;
            voice.FadeTarget = 1f;
            voice.FadeSeconds = ev.fadeInSeconds;
            voice.StartAt = now + ev.delaySeconds;
            voice.Active = true;

            AudioSource source = voice.Source;
            source.clip = clip;
            source.loop = ev.loop;
            source.pitch = Mathf.Pow(2f, semitones / 12f);
            source.panStereo = ev.pan;
            source.outputAudioMixerGroup = GroupFor(ev.bus);
            source.volume = Mathf.Clamp01(VoiceVolume(voice));

            if (ev.delaySeconds > 0f) source.PlayDelayed(ev.delaySeconds);
            else source.Play();

            if (ev.duckMusicDb < 0f) Duck(ev.duckMusicDb, ev.duckSeconds);

            return new AudioHandle(voice.Id);
        }

        /// ¿El evento existe y ya tiene sonidos? (para elegir entre un sonido específico y uno general)
        public bool HasSound(string id) =>
            library != null && library.TryGetEvent(id, out SoundEvent ev) && ev.HasClips;

        /// ¿La pista existe y ya tiene audio?
        public bool HasMusic(string trackId) =>
            library != null && library.TryGetTrack(trackId, out MusicTrack track) && track.HasAudio;

        public bool IsPlaying(AudioHandle handle)
        {
            Voice voice = Find(handle);
            return voice != null && !voice.Stopping;
        }

        /// Para un sonido. fadeSeconds < 0 usa el fundido de salida del evento.
        public void Stop(AudioHandle handle, float fadeSeconds = -1f)
        {
            Voice voice = Find(handle);
            if (voice != null) StopVoice(voice, fadeSeconds);
        }

        /// Cambia el volumen de un sonido que está sonando (p. ej. un bucle que sube y baja con la intensidad).
        public void SetVolume(AudioHandle handle, float scale)
        {
            Voice voice = Find(handle);
            if (voice != null) voice.UserScale = Mathf.Max(0f, scale);
        }

        public void StopAll(float fadeSeconds = 0.1f)
        {
            foreach (var voice in voices)
                if (voice.Active) StopVoice(voice, fadeSeconds);
        }

        // ───────────── Música ─────────────
        public void PlayMusic(string trackId)
        {
            if (library == null || !library.TryGetTrack(trackId, out MusicTrack track))
            {
                WarnOnce("trk:" + trackId, $"[Audio] Pista de música desconocida: '{trackId}'.");
                return;
            }

            PlayMusic(track);
        }

        public void PlayMusic(MusicTrack track)
        {
            if (track == null) return;

            // Pista sin audio todavía: sigue sonando la anterior
            if (!track.HasAudio)
            {
                WarnOnce("nomusic:" + track.trackId,
                    $"[Audio] La pista '{track.trackId}' no tiene audio todavía (sigue sonando la anterior, si la hay). " +
                    $"Guarda el archivo {track.trackId}.wav en la carpeta Audio/Musica.");
                return;
            }

            if (currentTrack == track) return;

            currentTrack = track;
            Debug.Log($"[Audio] Música: '{track.trackId}' (entra en {track.crossfadeSeconds:0.#} s, volumen {track.volumeDb} dB).", track);

            Deck old = decks[activeDeck];
            old.Target = 0f;
            old.FadeSeconds = track.crossfadeSeconds;

            activeDeck = 1 - activeDeck;
            StartDeck(decks[activeDeck], track);
        }

        public void StopMusic(float fadeSeconds = 1f)
        {
            currentTrack = null;
            foreach (var deck in decks)
            {
                if (deck.Track == null) continue;
                deck.Target = 0f;
                deck.FadeSeconds = fadeSeconds;
            }
        }

        void StartDeck(Deck deck, MusicTrack track)
        {
            deck.Main.Stop();
            deck.Intro.Stop();

            deck.Track = track;
            deck.Level = 0f;
            deck.Target = 1f;
            deck.FadeSeconds = track.crossfadeSeconds;

            deck.Main.outputAudioMixerGroup = musicGroup;
            deck.Intro.outputAudioMixerGroup = musicGroup;

            // Arranque programado con el reloj de audio: sin huecos entre la intro y el bucle
            double start = AudioSettings.dspTime + 0.05;

            if (track.introClip != null && track.loopClip != null)
            {
                deck.Intro.clip = track.introClip;
                deck.Intro.loop = false;
                deck.Intro.PlayScheduled(start);

                double introLength = (double)track.introClip.samples / track.introClip.frequency;
                deck.Main.clip = track.loopClip;
                deck.Main.loop = track.loops;
                deck.Main.PlayScheduled(start + introLength);
            }
            else
            {
                deck.Main.clip = track.loopClip != null ? track.loopClip : track.introClip;
                deck.Main.loop = track.loops;
                deck.Main.PlayScheduled(start);
            }
        }

        // ───────────── Volúmenes ─────────────
        public float GetBusVolume(AudioBus bus) => busVolume[(int)bus];

        public void SetBusVolume(AudioBus bus, float value)
        {
            busVolume[(int)bus] = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(KeyBus + bus, busVolume[(int)bus]);
        }

        public void SetMasterVolume(float value)
        {
            MasterVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(KeyMaster, MasterVolume);
        }

        void LoadSettings()
        {
            MasterVolume = PlayerPrefs.GetFloat(KeyMaster, 1f);
            for (int i = 0; i < busVolume.Length; i++)
                busVolume[i] = PlayerPrefs.GetFloat(KeyBus + (AudioBus)i, busVolume[i]);
        }

        // ───────────── Interno: voces ─────────────
        void BuildVoices()
        {
            var root = new GameObject("Voices").transform;
            root.SetParent(transform, false);

            voices = new Voice[voiceCount];
            for (int i = 0; i < voiceCount; i++)
                voices[i] = new Voice { Source = NewSource(root, "Voice " + i) };
        }

        void BuildDecks()
        {
            var root = new GameObject("Music").transform;
            root.SetParent(transform, false);

            decks = new Deck[2];
            for (int i = 0; i < 2; i++)
            {
                decks[i] = new Deck
                {
                    Intro = NewSource(root, "Deck " + i + " intro"),
                    Main = NewSource(root, "Deck " + i + " loop")
                };
            }
        }

        static AudioSource NewSource(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;   // juego 2D
            return source;
        }

        Voice AcquireVoice(int priority)
        {
            Voice victim = null;

            foreach (var voice in voices)
            {
                if (!voice.Active) return voice;

                // El candidato a robar: el menos importante y, a igualdad, el más antiguo
                if (victim == null
                    || voice.Event.priority > victim.Event.priority
                    || (voice.Event.priority == victim.Event.priority && voice.StartAt < victim.StartAt))
                    victim = voice;
            }

            // Solo se roba si el sonido nuevo es al menos tan importante como la víctima
            if (victim != null && victim.Event.priority >= priority)
            {
                Release(victim);
                return victim;
            }

            return null;
        }

        void UpdateVoices(float dt)
        {
            float now = Time.unscaledTime;

            foreach (var voice in voices)
            {
                if (!voice.Active) continue;

                if (voice.FadeSeconds <= 0f) voice.Fade = voice.FadeTarget;
                else voice.Fade = Mathf.MoveTowards(voice.Fade, voice.FadeTarget, dt / voice.FadeSeconds);

                if (voice.Stopping && voice.Fade <= 0.001f) { Release(voice); continue; }

                // Un sonido que ya terminó libera su voz
                if (now > voice.StartAt + 0.1f && !voice.Source.isPlaying) { Release(voice); continue; }

                voice.Source.volume = Mathf.Clamp01(VoiceVolume(voice));
            }
        }

        float VoiceVolume(Voice voice)
        {
            float bus = busVolume[(int)voice.Event.bus];
            if (voice.Event.bus == AudioBus.Music) bus *= duck;
            // Los sonidos que no suenan en segundo plano (p. ej. los bucles de ambiente) callan sin foco y vuelven con él
            float background = !voice.Event.playInBackground && !HasFocus ? 0f : 1f;
            return voice.EventVolume * voice.UserScale * voice.Fade * bus * MasterVolume * FocusFactor * background;
        }

        void StopVoice(Voice voice, float fadeSeconds)
        {
            float fade = fadeSeconds >= 0f ? fadeSeconds : voice.Event.fadeOutSeconds;
            if (fade <= 0f) { Release(voice); return; }

            voice.Stopping = true;
            voice.FadeTarget = 0f;
            voice.FadeSeconds = fade;
        }

        void Release(Voice voice)
        {
            voice.Source.Stop();
            voice.Source.clip = null;
            voice.Active = false;
            voice.Stopping = false;
            voice.Event = null;
        }

        Voice Find(AudioHandle handle)
        {
            if (!handle.IsValid) return null;
            foreach (var voice in voices)
                if (voice.Active && voice.Id == handle.Id) return voice;
            return null;
        }

        int CountActive(SoundEvent ev)
        {
            int count = 0;
            foreach (var voice in voices)
                if (voice.Active && !voice.Stopping && voice.Event == ev) count++;
            return count;
        }

        Voice OldestActive(SoundEvent ev)
        {
            Voice oldest = null;
            foreach (var voice in voices)
            {
                if (!voice.Active || voice.Stopping || voice.Event != ev) continue;
                if (oldest == null || voice.StartAt < oldest.StartAt) oldest = voice;
            }
            return oldest;
        }

        // ───────────── Interno: elección del clip ─────────────
        EventState GetState(SoundEvent ev)
        {
            if (!states.TryGetValue(ev, out EventState state))
            {
                state = new EventState();
                states[ev] = state;
            }
            return state;
        }

        static AudioClip PickClip(SoundEvent ev, EventState state)
        {
            AudioClip[] clips = ev.clips;
            int valid = 0;
            foreach (var clip in clips) if (clip != null) valid++;
            if (valid == 0) return null;

            int index;
            switch (ev.playMode)
            {
                case ClipPlayMode.Sequential:
                    index = NextValid(clips, state.Sequence);
                    state.Sequence = (index + 1) % clips.Length;
                    break;

                case ClipPlayMode.Random:
                    index = RandomValid(clips, valid);
                    break;

                default:   // RandomNoRepeat
                    index = RandomValid(clips, valid);
                    if (valid > 1)
                    {
                        for (int guard = 0; index == state.LastIndex && guard < 8; guard++)
                            index = RandomValid(clips, valid);
                    }
                    break;
            }

            state.LastIndex = index;
            return clips[index];
        }

        static int RandomValid(AudioClip[] clips, int valid)
        {
            int pick = Random.Range(0, valid);
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] == null) continue;
                if (pick-- == 0) return i;
            }
            return 0;
        }

        static int NextValid(AudioClip[] clips, int start)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                int index = (start + i) % clips.Length;
                if (clips[index] != null) return index;
            }
            return 0;
        }

        // ───────────── Interno: ducking y música ─────────────
        void Duck(float db, float seconds)
        {
            float now = Time.unscaledTime;
            float level = Db(db);

            duckGoal = now < duckUntil ? Mathf.Min(duckGoal, level) : level;
            duckUntil = Mathf.Max(duckUntil, now + seconds);
        }

        void UpdateDuck(float dt)
        {
            float goal = Time.unscaledTime < duckUntil ? duckGoal : 1f;
            float rate = goal < duck ? 1f / 0.10f : 1f / 0.8f;   // baja rápido, sube despacio
            duck = Mathf.MoveTowards(duck, goal, rate * dt);
        }

        void UpdateMusic(float dt)
        {
            foreach (var deck in decks)
            {
                if (deck.Track == null) continue;

                float step = deck.FadeSeconds <= 0f ? 1f : dt / deck.FadeSeconds;
                deck.Level = Mathf.MoveTowards(deck.Level, deck.Target, step);

                if (deck.Target <= 0f && deck.Level <= 0.001f)
                {
                    deck.Main.Stop();
                    deck.Intro.Stop();
                    deck.Track = null;
                    continue;
                }

                float background = !deck.Track.playInBackground && !HasFocus ? 0f : 1f;
                float volume = Db(deck.Track.volumeDb) * deck.Level * busVolume[(int)AudioBus.Music]
                               * duck * MasterVolume * FocusFactor * background;

                volume = Mathf.Clamp01(volume);
                deck.Main.volume = volume;
                deck.Intro.volume = volume;
            }
        }

        // ───────────── Utilidades ─────────────
        AudioMixerGroup GroupFor(AudioBus bus)
        {
            switch (bus)
            {
                case AudioBus.Music: return musicGroup;
                case AudioBus.Ui: return uiGroup;
                case AudioBus.Ambience: return ambienceGroup;
                default: return sfxGroup;
            }
        }

        static float Db(float db) => Mathf.Pow(10f, db / 20f);

        void WarnOnce(string key, string message)
        {
            if (warned.Add(key)) Debug.LogWarning(message, this);
        }

        [ContextMenu("Reproducir evento de prueba")]
        void TestEvent()
        {
            if (string.IsNullOrEmpty(testEventId))
            {
                Debug.LogWarning("[Audio] Escribe un id en 'Test Event Id' (campo Pruebas del AudioManager).", this);
                return;
            }

            if (library == null || !library.TryGetEvent(testEventId, out SoundEvent ev))
            {
                Debug.LogWarning($"[Audio] No existe el evento '{testEventId}' (o falta la biblioteca).", this);
                return;
            }

            Debug.Log($"[Audio] Prueba del evento '{ev.eventId}': {(ev.HasClips ? ev.clips.Length + " sonido(s)" : "SIN SONIDOS todavía")}, " +
                      $"volumen {ev.volumeDb} dB, banco {ev.bus} ({busVolume[(int)ev.bus]:0.##}), maestro {MasterVolume:0.##}.", ev);
            Play(ev);
        }

        [ContextMenu("Reproducir música de prueba")]
        void TestTrack()
        {
            if (string.IsNullOrEmpty(testTrackId))
            {
                Debug.LogWarning("[Audio] Escribe un id en 'Test Track Id' (campo Pruebas del AudioManager).", this);
                return;
            }

            if (library == null || !library.TryGetTrack(testTrackId, out MusicTrack track))
            {
                Debug.LogWarning($"[Audio] No existe la pista '{testTrackId}' (o falta la biblioteca).", this);
                return;
            }

            Debug.Log($"[Audio] Prueba de la pista '{track.trackId}': intro={(track.introClip != null)}, bucle={(track.loopClip != null)}, " +
                      $"volumen {track.volumeDb} dB, fundido de entrada {track.crossfadeSeconds} s, " +
                      $"suena con la ventana en segundo plano={track.playInBackground}.", track);
            PlayMusic(track);
        }

        [ContextMenu("Parar música")]
        void TestStopMusic() => StopMusic();

        /// Lista el estado del motor: sirve para averiguar por qué no se oye algo.
        [ContextMenu("Diagnóstico de audio")]
        void Diagnose()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("[Audio] Diagnóstico");

            report.AppendLine(library == null
                ? "  ✗ Falta la AudioLibrary en el AudioManager."
                : $"  ✔ AudioLibrary: {library.events?.Length ?? 0} eventos y {library.tracks?.Length ?? 0} pistas.");

            int listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length;
            report.AppendLine(listeners == 0
                ? "  ✗ No hay ningún Audio Listener en la escena: no se oirá nada."
                : $"  ✔ Audio Listeners en la escena: {listeners}.");

            report.AppendLine($"  Volumen del sistema (AudioListener): {AudioListener.volume:0.##}. Audio en pausa: {AudioListener.pause}.");
            report.AppendLine($"  Maestro: {MasterVolume:0.##}. Música: {busVolume[0]:0.##}, Efectos: {busVolume[1]:0.##}, " +
                              $"Interfaz: {busVolume[2]:0.##}, Ambiente: {busVolume[3]:0.##}. Bajada de la música (ducking): {duck:0.##}.");
            report.AppendLine($"  Ventana con el foco (real): {Application.isFocused}. Foco que aplica el audio: {HasFocus}. " +
                              $"Silenciar sin foco: {silenceWhenUnfocused}. Respetar el foco en el Editor: {respectFocusInEditor}.");

            for (int i = 0; i < decks.Length; i++)
            {
                Deck deck = decks[i];
                report.AppendLine(deck.Track == null
                    ? $"  Cabecera de música {i}: libre."
                    : $"  Cabecera de música {i}: '{deck.Track.trackId}', nivel {deck.Level:0.##}, volumen real {deck.Main.volume:0.###}, " +
                      $"sonando={deck.Main.isPlaying || deck.Intro.isPlaying}.");
            }

            int active = 0;
            foreach (var voice in voices) if (voice.Active) active++;
            report.AppendLine($"  Voces de efectos activas: {active}/{voices.Length}.");

            Debug.Log(report.ToString(), this);
        }
    }
}
