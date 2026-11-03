using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Studio.Audio.EditorTools
{
    /// Reproduce sonidos dentro del Editor, sin dar a Play.
    public static class AudioPreview
    {
        static AudioSource source;

        static AudioSource Source
        {
            get
            {
                if (source == null)
                {
                    var go = EditorUtility.CreateGameObjectWithHideFlags("Audio Preview", HideFlags.HideAndDontSave, typeof(AudioSource));
                    source = go.GetComponent<AudioSource>();
                    source.playOnAwake = false;
                }
                return source;
            }
        }

        /// Reproduce el evento con sus ajustes (volumen, tono y variaciones incluidos).
        public static void Play(SoundEvent ev)
        {
            var valid = new List<AudioClip>();
            if (ev.clips != null)
                foreach (var clip in ev.clips) if (clip != null) valid.Add(clip);

            if (valid.Count == 0) return;

            float db = ev.volumeDb + Random.Range(-ev.volumeVariationDb, ev.volumeVariationDb);
            float semitones = ev.pitchSemitones + Random.Range(-ev.pitchVariationSemitones, ev.pitchVariationSemitones);

            AudioSource s = Source;
            s.Stop();
            s.clip = valid[Random.Range(0, valid.Count)];
            s.volume = Mathf.Clamp01(Mathf.Pow(10f, db / 20f));
            s.pitch = Mathf.Pow(2f, semitones / 12f);
            s.panStereo = ev.pan;
            s.loop = ev.loop;
            s.Play();
        }

        public static void PlayClip(AudioClip clip, float volumeDb, bool loop)
        {
            if (clip == null) return;

            AudioSource s = Source;
            s.Stop();
            s.clip = clip;
            s.volume = Mathf.Clamp01(Mathf.Pow(10f, volumeDb / 20f));
            s.pitch = 1f;
            s.panStereo = 0f;
            s.loop = loop;
            s.Play();
        }

        public static void Stop()
        {
            if (source != null) source.Stop();
        }
    }

    [CustomEditor(typeof(SoundEvent))]
    public class SoundEventEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var ev = (SoundEvent)target;

            if (!string.IsNullOrEmpty(ev.description))
                EditorGUILayout.HelpBox(ev.description, MessageType.Info);

            if (!ev.HasClips)
            {
                EditorGUILayout.HelpBox(
                    "Sin sonidos todavía: en el juego este evento está en silencio.\n" +
                    "Arrastra archivos a 'Clips', o guarda en la carpeta Audio archivos que se llamen " +
                    ev.eventId + "_01.wav, " + ev.eventId + "_02.wav...",
                    MessageType.Warning);
            }

            serializedObject.Update();

            SerializedProperty property = serializedObject.GetIterator();
            bool enter = true;
            while (property.NextVisible(enter))
            {
                enter = false;
                if (property.name == "m_Script") continue;

                // El juego no lleva música: estos campos no se muestran
                if (property.name == "duckMusicDb" || property.name == "duckSeconds") continue;

                // El id y la descripción los controla el catálogo del código
                using (new EditorGUI.DisabledScope(property.name == "eventId" || property.name == "description"))
                    EditorGUILayout.PropertyField(property, true);
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▶ Probar", GUILayout.Height(30))) AudioPreview.Play(ev);
                if (GUILayout.Button("■ Parar", GUILayout.Height(30))) AudioPreview.Stop();
            }

            EditorGUILayout.HelpBox("Si no suena, activa el icono del altavoz en la barra superior de la vista Scene.", MessageType.None);
        }
    }

    [CustomEditor(typeof(MusicTrack))]
    public class MusicTrackEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var track = (MusicTrack)target;

            if (!string.IsNullOrEmpty(track.description))
                EditorGUILayout.HelpBox(track.description, MessageType.Info);

            if (!track.HasAudio)
            {
                EditorGUILayout.HelpBox(
                    "Sin música todavía. Guarda en la carpeta Audio/Musica el bucle como " + track.trackId +
                    ".wav y, si tiene intro, como " + track.trackId + "_Intro.wav.",
                    MessageType.Warning);
            }

            serializedObject.Update();

            SerializedProperty property = serializedObject.GetIterator();
            bool enter = true;
            while (property.NextVisible(enter))
            {
                enter = false;
                if (property.name == "m_Script") continue;

                using (new EditorGUI.DisabledScope(property.name == "trackId" || property.name == "description"))
                    EditorGUILayout.PropertyField(property, true);
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(track.introClip == null))
                    if (GUILayout.Button("▶ Intro", GUILayout.Height(30))) AudioPreview.PlayClip(track.introClip, track.volumeDb, false);

                using (new EditorGUI.DisabledScope(track.loopClip == null))
                    if (GUILayout.Button("▶ Bucle", GUILayout.Height(30))) AudioPreview.PlayClip(track.loopClip, track.volumeDb, track.loops);

                if (GUILayout.Button("■ Parar", GUILayout.Height(30))) AudioPreview.Stop();
            }
        }
    }

    [CustomEditor(typeof(AudioCatalogAsset))]
    public class AudioCatalogEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var catalog = (AudioCatalogAsset)target;

            EditorGUILayout.HelpBox(
                "Aquí se definen todos los sonidos y pistas del juego. Tras cambiar la lista, usa los botones de abajo, en orden.",
                MessageType.Info);

            DrawDefaultInspector();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Acciones", EditorStyles.boldLabel);

            if (GUILayout.Button("1. Crear o completar los sonidos del catálogo")) AudioCatalogTools.CreateOrUpdateAssets(catalog);
            if (GUILayout.Button("2. Asignar sonidos por nombre de archivo")) Debug.Log($"[Audio] Sonidos asignados por nombre: {AudioAutoAssign.Run(true)}.");
            if (GUILayout.Button("3. Generar AudioIds.cs")) AudioCodeGenerator.GenerateIds(catalog);
            if (GUILayout.Button("4. Generar la guía para el equipo de sonido")) AudioCodeGenerator.GenerateGuide(catalog);

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Exportar a CSV..."))
                {
                    string path = EditorUtility.SaveFilePanel("Exportar catálogo", "", "AudioCatalog.csv", "csv");
                    if (!string.IsNullOrEmpty(path)) AudioCatalogCsv.Export(catalog, path);
                }

                if (GUILayout.Button("Importar desde CSV..."))
                {
                    string path = EditorUtility.OpenFilePanel("Importar catálogo", "", "csv");
                    if (!string.IsNullOrEmpty(path)) AudioCatalogCsv.Import(catalog, path);
                }
            }
        }
    }
}
