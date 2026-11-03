using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Studio.Audio.EditorTools
{
    /// Rutas de la carpeta de audio del proyecto.
    public static class AudioEditorPaths
    {
        const string DefaultRoot = "Assets/Audio";

        /// La carpeta de audio del proyecto: la que contiene el catálogo (AudioCatalog). Sin catálogo, Assets/Audio.
        public static string Root
        {
            get
            {
                string[] guids = AssetDatabase.FindAssets("t:AudioCatalogAsset");
                if (guids.Length == 0) return DefaultRoot;

                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return Path.GetDirectoryName(path).Replace('\\', '/');
            }
        }

        public static string EventsFolder => Root + "/Eventos";

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        public static List<T> LoadAll<T>() where T : UnityEngine.Object
        {
            var list = new List<T>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) list.Add(asset);
            }
            return list;
        }

        public static AudioCatalogAsset LoadCatalog() => LoadAll<AudioCatalogAsset>().FirstOrDefault();

        /// Nombre de archivo válido a partir de un id.
        public static string SafeName(string id)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) id = id.Replace(c, '_');
            return id;
        }
    }

    /// Menú Tools > Audio Manager.
    public static class AudioCatalogTools
    {
        const string Menu = "Tools/Audio Manager/";

        static AudioCatalogAsset RequireCatalog()
        {
            var catalog = AudioEditorPaths.LoadCatalog();
            if (catalog == null)
                Debug.LogError("[Audio] No hay catálogo. Usa Tools > Audio Manager > 0. Crear el catálogo.");
            return catalog;
        }

        [MenuItem(Menu + "0. Crear el catálogo", priority = 0)]
        static void CreateCatalog()
        {
            var existing = AudioEditorPaths.LoadCatalog();
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                Debug.Log("[Audio] Ya existe un catálogo: " + AssetDatabase.GetAssetPath(existing));
                return;
            }

            AudioEditorPaths.EnsureFolder("Assets/Audio");

            var catalog = ScriptableObject.CreateInstance<AudioCatalogAsset>();
            catalog.events.Add(new AudioCatalogAsset.EventDef
            {
                id = "UI_Click", bus = AudioBus.Ui, description = "Clic en un botón.", cooldown = 0.03f, maxInstances = 4
            });

            AssetDatabase.CreateAsset(catalog, "Assets/Audio/AudioCatalog.asset");
            AssetDatabase.SaveAssets();
            Selection.activeObject = catalog;
            EditorGUIUtility.PingObject(catalog);
            Debug.Log("[Audio] Catálogo creado en Assets/Audio/AudioCatalog.asset. Añade tus eventos y pulsa los botones de abajo.");
        }

        [MenuItem(Menu + "1. Crear o completar los sonidos del catálogo", priority = 1)]
        static void MenuCreateAssets()
        {
            var catalog = RequireCatalog();
            if (catalog != null) CreateOrUpdateAssets(catalog);
        }

        [MenuItem(Menu + "2. Asignar sonidos por nombre de archivo", priority = 2)]
        static void MenuAssign()
        {
            int assigned = AudioAutoAssign.Run(true);
            Debug.Log($"[Audio] Sonidos asignados por nombre: {assigned}.");
        }

        [MenuItem(Menu + "3. Generar AudioIds.cs", priority = 3)]
        static void MenuGenerateIds()
        {
            var catalog = RequireCatalog();
            if (catalog != null) AudioCodeGenerator.GenerateIds(catalog);
        }

        [MenuItem(Menu + "4. Generar la guía para el equipo de sonido", priority = 4)]
        static void MenuGenerateGuide()
        {
            var catalog = RequireCatalog();
            if (catalog != null) AudioCodeGenerator.GenerateGuide(catalog);
        }

        [MenuItem(Menu + "5. Informe de cobertura", priority = 5)]
        static void Coverage()
        {
            var events = AudioEditorPaths.LoadAll<SoundEvent>();
            var tracks = AudioEditorPaths.LoadAll<MusicTrack>();

            var missingEvents = events.Where(e => !e.HasClips).Select(e => e.eventId).OrderBy(s => s).ToList();
            var missingTracks = tracks.Where(t => !t.HasAudio).Select(t => t.trackId).OrderBy(s => s).ToList();

            string message =
                $"[Audio] Eventos con sonido: {events.Count - missingEvents.Count}/{events.Count}.  " +
                $"Pistas con audio: {tracks.Count - missingTracks.Count}/{tracks.Count}.";

            if (missingEvents.Count > 0)
                message += "\nEventos sin sonido:\n  - " + string.Join("\n  - ", missingEvents);
            if (missingTracks.Count > 0)
                message += "\nPistas sin audio:\n  - " + string.Join("\n  - ", missingTracks);

            Debug.Log(message);
        }

        /// Crea (o completa) un asset por cada evento y pista del catálogo, y la biblioteca que los junta.
        /// No toca los sonidos ya asignados ni los ajustes que alguien haya cambiado.
        public static void CreateOrUpdateAssets(AudioCatalogAsset catalog)
        {
            string root = AudioEditorPaths.Root;
            foreach (string sub in new[] { "", "/Eventos", "/Musica", "/SFX", "/UI", "/Ambiente" })
                AudioEditorPaths.EnsureFolder(root + sub);

            // ── Eventos ──
            var existingEvents = AudioEditorPaths.LoadAll<SoundEvent>()
                .Where(e => !string.IsNullOrEmpty(e.eventId))
                .GroupBy(e => e.eventId)
                .ToDictionary(g => g.Key, g => g.First());

            int createdEvents = 0;
            foreach (var def in catalog.events)
            {
                if (string.IsNullOrWhiteSpace(def.id)) continue;

                if (!existingEvents.TryGetValue(def.id, out SoundEvent ev))
                {
                    ev = ScriptableObject.CreateInstance<SoundEvent>();
                    ev.eventId = def.id;
                    ev.bus = def.bus;
                    ev.volumeDb = def.volumeDb;
                    ev.cooldownSeconds = def.cooldown;
                    ev.maxInstances = def.maxInstances;
                    ev.loop = def.loop;
                    ev.playInBackground = def.playInBackground;
                    ev.duckMusicDb = def.duckMusicDb;
                    ev.duckSeconds = def.duckSeconds;
                    ev.priority = def.priority;
                    ev.fadeInSeconds = def.loop ? 0.2f : 0f;
                    ev.fadeOutSeconds = def.loop ? 0.4f : 0f;

                    AssetDatabase.CreateAsset(ev, root + "/Eventos/SFX_" + AudioEditorPaths.SafeName(def.id) + ".asset");
                    createdEvents++;
                }

                ev.description = def.description;   // la descripción siempre se mantiene al día con el catálogo
                EditorUtility.SetDirty(ev);
            }

            // ── Pistas de música ──
            var existingTracks = AudioEditorPaths.LoadAll<MusicTrack>()
                .Where(t => !string.IsNullOrEmpty(t.trackId))
                .GroupBy(t => t.trackId)
                .ToDictionary(g => g.Key, g => g.First());

            int createdTracks = 0;
            foreach (var def in catalog.tracks)
            {
                if (string.IsNullOrWhiteSpace(def.id)) continue;

                if (!existingTracks.TryGetValue(def.id, out MusicTrack track))
                {
                    track = ScriptableObject.CreateInstance<MusicTrack>();
                    track.trackId = def.id;
                    AssetDatabase.CreateAsset(track, root + "/Musica/MUS_" + AudioEditorPaths.SafeName(def.id) + ".asset");
                    createdTracks++;
                }

                track.description = def.description;
                EditorUtility.SetDirty(track);
            }

            AssetDatabase.SaveAssets();

            // ── Biblioteca ──
            AudioLibrary library = AudioEditorPaths.LoadAll<AudioLibrary>().FirstOrDefault();
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<AudioLibrary>();
                AssetDatabase.CreateAsset(library, root + "/AudioLibrary.asset");
            }

            library.events = AudioEditorPaths.LoadAll<SoundEvent>().OrderBy(e => e.eventId).ToArray();
            library.tracks = AudioEditorPaths.LoadAll<MusicTrack>().OrderBy(t => t.trackId).ToArray();
            EditorUtility.SetDirty(library);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            int assigned = AudioAutoAssign.Run(false);

            Debug.Log($"[Audio] Listo. Eventos nuevos: {createdEvents}, pistas nuevas: {createdTracks}, " +
                      $"sonidos asignados por nombre: {assigned}. Biblioteca: {library.events.Length} eventos y " +
                      $"{library.tracks.Length} pistas ({AssetDatabase.GetAssetPath(library)}).", library);
        }
    }

    /// Asigna los archivos de audio a su evento según el nombre del archivo.
    ///   UI_Click_01.wav, UI_Click_02.wav -> evento UI_Click
    ///   Music_Main.wav (bucle) y Music_Main_Intro.wav (intro) -> pista Music_Main
    /// Solo AÑADE: nunca quita sonidos que alguien haya puesto a mano.
    public static class AudioAutoAssign
    {
        public static int Run(bool verbose)
        {
            // Los ids más largos primero: así "Hit_Big_01" va a Hit_Big y no a Hit
            var events = AudioEditorPaths.LoadAll<SoundEvent>()
                .Where(e => !string.IsNullOrEmpty(e.eventId))
                .OrderByDescending(e => e.eventId.Length).ToList();
            var tracks = AudioEditorPaths.LoadAll<MusicTrack>()
                .Where(t => !string.IsNullOrEmpty(t.trackId))
                .OrderByDescending(t => t.trackId.Length).ToList();

            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { AudioEditorPaths.Root });
            var paths = guids.Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToList();

            int added = 0;

            foreach (string path in paths)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) continue;

                string name = Path.GetFileNameWithoutExtension(path);

                // Música
                MusicTrack track = tracks.FirstOrDefault(t =>
                    name == t.trackId || name == t.trackId + "_Loop" || name == t.trackId + "_Intro");
                if (track != null)
                {
                    bool isIntro = name.EndsWith("_Intro");
                    if (isIntro && track.introClip != clip) { track.introClip = clip; added++; EditorUtility.SetDirty(track); }
                    if (!isIntro && track.loopClip != clip) { track.loopClip = clip; added++; EditorUtility.SetDirty(track); }
                    continue;
                }

                // Efectos
                SoundEvent ev = events.FirstOrDefault(e => name == e.eventId || name.StartsWith(e.eventId + "_"));
                if (ev == null) continue;

                var list = ev.clips != null ? ev.clips.ToList() : new List<AudioClip>();
                if (list.Contains(clip)) continue;

                list.Add(clip);
                ev.clips = list.ToArray();
                added++;
                EditorUtility.SetDirty(ev);

                if (verbose) Debug.Log($"[Audio] '{name}' -> {ev.eventId}", ev);
            }

            if (added > 0) AssetDatabase.SaveAssets();
            return added;
        }
    }
}
