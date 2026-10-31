using UnityEngine;

/// <summary>
/// Tanrı'nın cevap sesleri ve tepkileri. GodNPC her cevap geldiğinde PlayReaction(ikna) çağırır.
/// Olumlu cevapta positiveClip, olumsuz cevapta negativeClip çalar.
/// Olumsuz cevapta ayrıca ekran sallanır (ScreenShake atanmışsa).
/// GodNPC ile aynı objeye (veya sahnede herhangi bir objeye) eklenip GodNPC'ye atanır.
/// </summary>
public class GodVoice : MonoBehaviour
{
    [Header("Sesler")]
    [Tooltip("Tanrı olumlu cevap verince (ikna_basarili = true) çalar.")]
    [SerializeField] private AudioClip positiveClip;
    [Tooltip("Tanrı olumsuz cevap verince (ikna_basarili = false) çalar.")]
    [SerializeField] private AudioClip negativeClip;

    [Header("Ayarlar")]
    [Range(0f, 1f)] [SerializeField] private float volume = 1f;

    [Header("Olumsuz Cevapta Ekran Sallama")]
    [Tooltip("Oyuncu kamerasındaki ScreenShake bileşeni. Boşsa Main Camera'da aranır.")]
    [SerializeField] private ScreenShake screenShake;
    [SerializeField] private bool shakeOnNegative = true;
    [SerializeField] private float shakeDuration = 0.6f;
    [SerializeField] private float shakeCameraMagnitude = 0.2f;
    [SerializeField] private float shakeUIMagnitude = 30f;

    private void Start()
    {
        if (screenShake == null && Camera.main != null)
            screenShake = Camera.main.GetComponent<ScreenShake>();
    }

    public void PlayReaction(bool positive)
    {
        AudioClip clip = positive ? positiveClip : negativeClip;
        if (clip != null)
        {
            // 2D çalar (spatialBlend = 0): oyuncu nerede olursa olsun aynı seviyede duyar
            SoundManager.Instance.Play(clip, transform.position, volume, 0f, 0f);
        }

        if (!positive && shakeOnNegative && screenShake != null)
            screenShake.Shake(shakeDuration, shakeCameraMagnitude, shakeUIMagnitude);
    }
}
