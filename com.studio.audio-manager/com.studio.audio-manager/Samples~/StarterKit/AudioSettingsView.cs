using UnityEngine;
using UnityEngine.UI;

namespace Studio.Audio.Samples
{
    /// Deslizadores de volumen para el menú de ajustes: Maestro, Música, Efectos y Ambiente.
    /// "Efectos" controla también los sonidos de la interfaz. Los valores se guardan entre sesiones.
    /// Cualquier deslizador que se deje vacío se ignora.
    public class AudioSettingsView : MonoBehaviour
    {
        [SerializeField] Slider masterSlider;
        [SerializeField] Slider musicSlider;
        [SerializeField] Slider sfxSlider;
        [SerializeField] Slider ambienceSlider;

        [Tooltip("Al mover el de Efectos suena este evento de muestra, para oír el nivel. Vacío = ninguno.")]
        [SerializeField] string previewEventId = "UI_Click";

        float lastPreview;

        void Awake()
        {
            Setup(masterSlider, v => { var m = AudioManager.Instance; if (m != null) m.SetMasterVolume(v); });
            Setup(musicSlider, v => { var m = AudioManager.Instance; if (m != null) m.SetBusVolume(AudioBus.Music, v); });
            Setup(ambienceSlider, v => { var m = AudioManager.Instance; if (m != null) m.SetBusVolume(AudioBus.Ambience, v); });
            Setup(sfxSlider, v =>
            {
                var m = AudioManager.Instance;
                if (m == null) return;

                m.SetBusVolume(AudioBus.Sfx, v);
                m.SetBusVolume(AudioBus.Ui, v);

                if (!string.IsNullOrEmpty(previewEventId) && Time.unscaledTime - lastPreview > 0.15f)
                {
                    lastPreview = Time.unscaledTime;
                    GameAudio.Play(previewEventId);
                }
            });
        }

        // Al abrirse el panel, los deslizadores muestran los valores actuales (sin disparar sonidos)
        void OnEnable()
        {
            var m = AudioManager.Instance;
            if (m == null) return;

            Show(masterSlider, m.MasterVolume);
            Show(musicSlider, m.GetBusVolume(AudioBus.Music));
            Show(sfxSlider, m.GetBusVolume(AudioBus.Sfx));
            Show(ambienceSlider, m.GetBusVolume(AudioBus.Ambience));
        }

        static void Setup(Slider slider, UnityEngine.Events.UnityAction<float> onChanged)
        {
            if (slider == null) return;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.onValueChanged.AddListener(onChanged);
        }

        static void Show(Slider slider, float value)
        {
            if (slider != null) slider.SetValueWithoutNotify(value);
        }
    }
}
