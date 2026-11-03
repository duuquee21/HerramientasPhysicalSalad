using UnityEditor;
using UnityEngine;

namespace Studio.Audio.EditorTools
{
    /// Aplica solo los ajustes de importación correctos según la carpeta, para que quien hace el audio
    /// no tenga que tocar nada de Unity. Solo actúa la PRIMERA vez que se importa un archivo,
    /// así que nunca pisa ajustes cambiados a mano.
    ///   Musica/   -> streaming (no ocupa memoria), Vorbis
    ///   Ambiente/ -> comprimido en memoria, Vorbis (bucles largos)
    ///   UI/ y SFX/ y el resto -> se descomprime al cargar, ADPCM (respuesta inmediata, poco peso)
    /// Además, al importar audio nuevo se asigna solo a su evento por el nombre del archivo.
    public class AudioImportPostprocessor : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            string root = AudioEditorPaths.Root + "/";
            if (!assetPath.StartsWith(root)) return;
            if (!assetImporter.importSettingsMissing) return;

            var importer = (AudioImporter)assetImporter;
            bool music = assetPath.StartsWith(root + "Musica/");
            bool ambience = assetPath.StartsWith(root + "Ambiente/");

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;

            if (music)
            {
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.7f;
            }
            else if (ambience)
            {
                settings.loadType = AudioClipLoadType.CompressedInMemory;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.7f;
            }
            else
            {
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.ADPCM;
            }

            importer.defaultSampleSettings = settings;
            importer.loadInBackground = music || ambience;
        }

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            string root = AudioEditorPaths.Root + "/";
            bool anyAudio = false;

            foreach (string path in imported)
            {
                if (path.StartsWith(root) && IsAudioFile(path)) { anyAudio = true; break; }
            }

            if (anyAudio) EditorApplication.delayCall += () => AudioAutoAssign.Run(false);
        }

        static bool IsAudioFile(string path)
        {
            string lower = path.ToLowerInvariant();
            return lower.EndsWith(".wav") || lower.EndsWith(".ogg") || lower.EndsWith(".mp3")
                   || lower.EndsWith(".aif") || lower.EndsWith(".aiff") || lower.EndsWith(".flac");
        }
    }
}
