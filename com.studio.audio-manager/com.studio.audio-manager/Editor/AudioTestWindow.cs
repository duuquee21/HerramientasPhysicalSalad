using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Studio.Audio.EditorTools
{
    /// Ventana para quien hace el audio: lista todos los eventos y pistas, dice cuáles tienen sonido
    /// y permite escucharlos sin dar a Play. Menú Tools > Audio Manager > Panel de pruebas.
    public class AudioTestWindow : EditorWindow
    {
        static readonly string[] BusNames = { "Todos", "Música", "Efectos", "Interfaz", "Ambiente" };

        string search = "";
        bool onlyMissing;
        int busFilter;
        Vector2 scroll;

        List<SoundEvent> events = new List<SoundEvent>();
        List<MusicTrack> tracks = new List<MusicTrack>();

        [MenuItem("Tools/Audio Manager/Panel de pruebas", priority = 20)]
        static void Open()
        {
            var window = GetWindow<AudioTestWindow>("Audio");
            window.minSize = new Vector2(440, 380);
            window.Reload();
        }

        void OnEnable() => Reload();
        void OnFocus() => Reload();
        void OnProjectChange() { Reload(); Repaint(); }

        void Reload()
        {
            events = AudioEditorPaths.LoadAll<SoundEvent>()
                .OrderBy(e => e.bus).ThenBy(e => e.eventId).ToList();
            tracks = AudioEditorPaths.LoadAll<MusicTrack>().OrderBy(t => t.trackId).ToList();
        }

        void OnGUI()
        {
            // Resumen y botones
            int withSound = events.Count(e => e.HasClips);
            int tracksWithAudio = tracks.Count(t => t.HasAudio);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(
                $"Eventos con sonido: {withSound}/{events.Count}     Pistas con audio: {tracksWithAudio}/{tracks.Count}",
                EditorStyles.boldLabel);

            if (events.Count == 0)
            {
                EditorGUILayout.HelpBox("No hay eventos todavía. Usa el menú Tools > Audio Manager > 1. Crear o completar los sonidos del catálogo.", MessageType.Warning);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Asignar sonidos por nombre")) { AudioAutoAssign.Run(true); Reload(); }
                if (GUILayout.Button("■ Parar")) AudioPreview.Stop();
            }

            // Filtros
            using (new EditorGUILayout.HorizontalScope())
            {
                search = EditorGUILayout.TextField("Buscar", search);
                busFilter = EditorGUILayout.Popup(busFilter, BusNames, GUILayout.Width(90));
            }
            onlyMissing = EditorGUILayout.ToggleLeft("Solo los que faltan", onlyMissing);

            EditorGUILayout.Space(4);
            scroll = EditorGUILayout.BeginScrollView(scroll);

            DrawEvents();
            DrawTracks();

            EditorGUILayout.EndScrollView();
        }

        void DrawEvents()
        {
            AudioBus? current = null;

            foreach (var ev in events)
            {
                if (!Matches(ev.eventId, ev.HasClips, (int)ev.bus)) continue;

                if (current != ev.bus)
                {
                    current = ev.bus;
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField(BusNames[(int)ev.bus + 1], EditorStyles.boldLabel);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    Status(ev.HasClips);

                    if (GUILayout.Button(ev.eventId, EditorStyles.label))
                    {
                        Selection.activeObject = ev;
                        EditorGUIUtility.PingObject(ev);
                    }

                    int count = ev.clips == null ? 0 : ev.clips.Count(c => c != null);
                    GUILayout.Label(count == 0 ? "" : count + (count == 1 ? " sonido" : " sonidos"), GUILayout.Width(80));

                    using (new EditorGUI.DisabledScope(!ev.HasClips))
                        if (GUILayout.Button("▶", GUILayout.Width(30))) AudioPreview.Play(ev);
                }
            }
        }

        void DrawTracks()
        {
            bool header = false;

            foreach (var track in tracks)
            {
                if (busFilter != 0 && busFilter != 1) break;   // solo con "Todos" o "Música"
                if (!Matches(track.trackId, track.HasAudio, -1)) continue;

                if (!header)
                {
                    header = true;
                    EditorGUILayout.Space(8);
                    EditorGUILayout.LabelField("Pistas de música", EditorStyles.boldLabel);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    Status(track.HasAudio);

                    if (GUILayout.Button(track.trackId, EditorStyles.label))
                    {
                        Selection.activeObject = track;
                        EditorGUIUtility.PingObject(track);
                    }

                    using (new EditorGUI.DisabledScope(track.introClip == null))
                        if (GUILayout.Button("▶ Intro", GUILayout.Width(62))) AudioPreview.PlayClip(track.introClip, track.volumeDb, false);

                    using (new EditorGUI.DisabledScope(track.loopClip == null))
                        if (GUILayout.Button("▶ Bucle", GUILayout.Width(62))) AudioPreview.PlayClip(track.loopClip, track.volumeDb, track.loops);
                }
            }
        }

        bool Matches(string id, bool hasSound, int bus)
        {
            if (onlyMissing && hasSound) return false;
            if (busFilter > 0 && bus >= 0 && bus != busFilter - 1) return false;
            if (!string.IsNullOrEmpty(search) && id.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) < 0) return false;
            return true;
        }

        static void Status(bool ok)
        {
            Color previous = GUI.contentColor;
            GUI.contentColor = ok ? new Color(0.4f, 0.85f, 0.4f) : new Color(0.95f, 0.5f, 0.4f);
            GUILayout.Label(ok ? "✔" : "✗", GUILayout.Width(18));
            GUI.contentColor = previous;
        }
    }
}
