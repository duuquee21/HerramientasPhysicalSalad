using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Studio.Audio.EditorTools
{
    /// Exporta e importa el catálogo como CSV (separado por punto y coma), para editarlo en una hoja de cálculo.
    /// Columnas: type;id;bus;volumeDb;cooldown;maxInstances;loop;playInBackground;duckMusicDb;duckSeconds;priority;description
    /// "type" es "event" o "track". En las pistas solo se usan id y description.
    public static class AudioCatalogCsv
    {
        const string Header = "type;id;bus;volumeDb;cooldown;maxInstances;loop;playInBackground;duckMusicDb;duckSeconds;priority;description";

        public static void Export(AudioCatalogAsset catalog, string path)
        {
            var sb = new StringBuilder();
            sb.AppendLine(Header);

            foreach (var e in catalog.events)
            {
                sb.AppendLine(string.Join(";", new[]
                {
                    "event", e.id, e.bus.ToString(), F(e.volumeDb), F(e.cooldown), e.maxInstances.ToString(),
                    B(e.loop), B(e.playInBackground), F(e.duckMusicDb), F(e.duckSeconds), e.priority.ToString(), Clean(e.description)
                }));
            }

            foreach (var t in catalog.tracks)
                sb.AppendLine("track;" + t.id + ";;;;;;;;;;" + Clean(t.description));

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));   // con BOM, para que Excel respete los acentos
            Debug.Log($"[Audio] Catálogo exportado: {catalog.events.Count} eventos y {catalog.tracks.Count} pistas -> {path}");
        }

        public static void Import(AudioCatalogAsset catalog, string path)
        {
            var events = new List<AudioCatalogAsset.EventDef>();
            var tracks = new List<AudioCatalogAsset.TrackDef>();

            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            for (int i = 1; i < lines.Length; i++)   // la primera línea es la cabecera
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                string[] p = line.Split(new[] { ';' }, 12);
                if (p.Length < 2 || string.IsNullOrWhiteSpace(p[1])) continue;

                string type = p[0].Trim().ToLowerInvariant();
                string description = p.Length > 11 ? p[11].Trim() : "";

                if (type == "track")
                {
                    tracks.Add(new AudioCatalogAsset.TrackDef { id = p[1].Trim(), description = description });
                }
                else if (type == "event")
                {
                    events.Add(new AudioCatalogAsset.EventDef
                    {
                        id = p[1].Trim(),
                        bus = ParseBus(Get(p, 2)),
                        volumeDb = Float(p, 3, 0f),
                        cooldown = Float(p, 4, 0f),
                        maxInstances = Mathf.RoundToInt(Float(p, 5, 0f)),
                        loop = Bool(p, 6, false),
                        playInBackground = Bool(p, 7, true),
                        duckMusicDb = Float(p, 8, 0f),
                        duckSeconds = Float(p, 9, 1f),
                        priority = Mathf.RoundToInt(Float(p, 10, 128f)),
                        description = description
                    });
                }
            }

            Undo.RecordObject(catalog, "Importar catálogo de audio");
            catalog.events = events;
            catalog.tracks = tracks;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Audio] Catálogo importado: {events.Count} eventos y {tracks.Count} pistas.");
        }

        // ───────────── Utilidades ─────────────
        static string Get(string[] p, int index) => index < p.Length ? p[index].Trim() : "";

        static AudioBus ParseBus(string text) =>
            System.Enum.TryParse(text, true, out AudioBus bus) ? bus : AudioBus.Sfx;

        static float Float(string[] p, int index, float fallback)
        {
            string text = Get(p, index).Replace(',', '.');
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : fallback;
        }

        static bool Bool(string[] p, int index, bool fallback)
        {
            string text = Get(p, index).ToLowerInvariant();
            if (text == "1" || text == "true" || text == "si" || text == "sí") return true;
            if (text == "0" || text == "false" || text == "no") return false;
            return fallback;
        }

        static string F(float value) => value.ToString(CultureInfo.InvariantCulture);
        static string B(bool value) => value ? "1" : "0";
        static string Clean(string text) => string.IsNullOrEmpty(text) ? "" : text.Replace(";", ",").Replace("\n", " ").Trim();
    }
}
