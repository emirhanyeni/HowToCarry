using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Ana menü buton sesleri: butonun üstüne gelince (hover) bir ses, tıklayınca başka bir ses.
/// Sahnede boş bir objeye (örn. MainMenu objesine) eklemen yeterli; sahnedeki TÜM butonları
/// (kapalı olan Ayarlar paneli butonları dahil) otomatik bulur, tek tek ayar yapmaya gerek yok.
/// Sesleri SoundManager çalar (sahneye elle koymana gerek yok, kendini oluşturur).
/// </summary>
public class MenuSounds : MonoBehaviour
{
    [Header("Sesler")]
    [Tooltip("Fare butonun üstüne gelince çalar (butonlar arası geçiş).")]
    [SerializeField] private AudioClip hoverClip;
    [Tooltip("Butona tıklanınca çalar.")]
    [SerializeField] private AudioClip clickClip;

    [Header("Ses Seviyesi")]
    [Range(0f, 1f)] [SerializeField] private float hoverVolume = 0.5f;
    [Range(0f, 1f)] [SerializeField] private float clickVolume = 0.8f;

    [Header("Ayarlar")]
    [Tooltip("Açıksa kapalı (inactive) paneldeki butonlar da dahil edilir. Ayarlar paneli için gerekli.")]
    [SerializeField] private bool includeInactiveButtons = true;

    private void Start()
    {
        var mode = includeInactiveButtons ? FindObjectsInactive.Include : FindObjectsInactive.Exclude;
        Button[] buttons = FindObjectsByType<Button>(mode, FindObjectsSortMode.None);

        foreach (Button button in buttons)
        {
            Register(button);
        }
    }

    private void Register(Button button)
    {
        // Hover: EventTrigger ile (butonda zaten varsa onu kullan, ezme)
        EventTrigger trigger = button.GetComponent<EventTrigger>();
        if (trigger == null) trigger = button.gameObject.AddComponent<EventTrigger>();

        var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enterEntry.callback.AddListener(_ =>
        {
            // Tıklanamaz (interactable kapalı) butonda ses çalmasın
            if (button.IsInteractable()) Play(hoverClip, hoverVolume);
        });
        trigger.triggers.Add(enterEntry);

        // Tık: butonun mevcut onClick olaylarına dokunmadan ekstra dinleyici ekle
        button.onClick.AddListener(() => Play(clickClip, clickVolume));
    }

    private void Play(AudioClip clip, float volume)
    {
        if (clip == null) return;

        // spatialBlend = 0 -> 2D, menüde her yerden aynı seviyede duyulur
        SoundManager.Instance.Play(clip, Vector3.zero, volume, 0.03f, 0f);
    }
}
