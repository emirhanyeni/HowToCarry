using UnityEngine;

/// <summary>
/// Obje sahnede belirdiğinde (sahne başlangıcı, Instantiate veya SetActive(true)) spawn sesi çalar.
/// Oyuncuya, bloğa ya da spawn olan herhangi bir objeye / prefab'a eklenebilir.
/// </summary>
public class SpawnSound : MonoBehaviour
{
    [SerializeField] private AudioClip[] spawnClips;
    [Range(0f, 1f)] [SerializeField] private float volume = 0.8f;
    [Tooltip("0 = 2D (her yerden aynı duyulur, oyuncu spawn'ı için), 1 = 3D (konuma göre).")]
    [Range(0f, 1f)] [SerializeField] private float spatialBlend = 1f;
    [Tooltip("Sesin çalmasından önceki bekleme (saniye).")]
    [SerializeField] private float delay = 0f;

    private void OnEnable()
    {
        if (delay > 0f) Invoke(nameof(PlayNow), delay);
        else PlayNow();
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(PlayNow));
    }

    // İstersen kodla veya UnityEvent'ten elle de çağırabilirsin
    public void PlayNow()
    {
        AudioClip clip = SoundManager.Pick(spawnClips);
        if (clip == null) return;
        SoundManager.Instance.Play(clip, transform.position, volume, 0.05f, spatialBlend);
    }
}
