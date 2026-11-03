using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Studio.Audio.Samples
{
    /// Marca: este botón no lleva el sonido automático (porque tiene el suyo propio).
    public class SoundIgnore : MonoBehaviour
    {
        public static void Mark(GameObject target)
        {
            if (target.GetComponent<SoundIgnore>() == null)
                target.AddComponent<SoundIgnore>();
        }
    }

    /// Sonido al pasar el ratón por encima de un botón. Lo añade solo ButtonSounds.
    public class HoverSound : MonoBehaviour, IPointerEnterHandler
    {
        public string eventId = "UI_Hover";
        Selectable selectable;

        void Awake() => selectable = GetComponent<Selectable>();

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (selectable == null || selectable.IsInteractable())
                GameAudio.Play(eventId);
        }
    }

    /// Pone solo el sonido de clic (y de ratón encima) a todos los botones y casillas de la interfaz,
    /// incluidos los que se crean más tarde. Colócalo en un objeto de la escena inicial.
    /// Los ids de los eventos se cambian en el Inspector.
    public class ButtonSounds : MonoBehaviour
    {
        [SerializeField] string clickEventId = "UI_Click";
        [SerializeField] string hoverEventId = "UI_Hover";
        [SerializeField] string toggleEventId = "UI_Toggle";
        [SerializeField] bool hoverSounds = true;
        [Tooltip("Cada cuánto se buscan botones nuevos, en segundos.")]
        [SerializeField] float scanInterval = 0.5f;

        readonly HashSet<int> wired = new HashSet<int>();
        float nextScan;

        void Awake() => DontDestroyOnLoad(gameObject);

        void Update()
        {
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + scanInterval;

            // Solo los activos: así, cuando un panel se abre por primera vez, sus Awake ya han marcado
            // los botones con SoundIgnore antes de que se les ponga el sonido.
            foreach (var selectable in FindObjectsByType<Selectable>(FindObjectsSortMode.None))
            {
                int id = selectable.GetInstanceID();
                if (wired.Contains(id)) continue;

                bool isButton = selectable is Button;
                bool isToggle = selectable is Toggle;
                if (!isButton && !isToggle) continue;

                wired.Add(id);
                if (selectable.GetComponent<SoundIgnore>() != null) continue;

                if (selectable is Button button)
                    button.onClick.AddListener(() => GameAudio.Play(clickEventId));
                else
                    ((Toggle)selectable).onValueChanged.AddListener(_ => GameAudio.Play(toggleEventId));

                if (hoverSounds && selectable.GetComponent<HoverSound>() == null)
                    selectable.gameObject.AddComponent<HoverSound>().eventId = hoverEventId;
            }
        }
    }
}
