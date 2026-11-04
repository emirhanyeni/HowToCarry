using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tüm sesleri çalan merkezi yönetici. Sahneye elle koymana GEREK YOK:
/// ilk ses çalındığında kendini otomatik oluşturur.
/// Birden fazla sesin üst üste binebilmesi için AudioSource havuzu kullanır.
/// Her kaynak çalınırken "meşgul" işaretlenir, aynı karede çalan iki ses asla aynı kaynağı paylaşmaz.
/// </summary>
public class SoundManager : MonoBehaviour
{
    private static SoundManager instance;

    public static SoundManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<SoundManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("SoundManager");
                    instance = go.AddComponent<SoundManager>();
                }
            }
            return instance;
        }
    }

    [Header("Havuz")]
    [Tooltip("Aynı anda çalabilecek en fazla ses sayısı.")]
    [SerializeField] private int maxVoices = 24;

    [Header("3D Ses Ayarları")]
    [SerializeField] private float minDistance = 2f;
    [SerializeField] private float maxDistance = 30f;

    [Header("Genel")]
    [Range(0f, 1f)][SerializeField] private float masterVolume = 1f;

    [Header("Hata Ayıklama")]
    [Tooltip("Açıkken her çalınan ses Console'a yazılır. Her şey çalışınca kapat.")]
    [SerializeField] private bool logPlays = true;

    private readonly List<AudioSource> pool = new List<AudioSource>();
    private readonly List<float> busyUntil = new List<float>();
    private AudioListener listener;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    /// <summary>
    /// Bir sesi belirtilen konumda çalar.
    /// spatialBlend: 0 = 2D (her yerden aynı duyulur, oyuncunun kendi sesleri için), 1 = 3D (konuma göre).
    /// minDist: bu mesafeye kadar ses tam seviyede duyulur. maxDist: bu mesafede ses tamamen biter.
    /// İkisi de -1 bırakılırsa SoundManager'daki varsayılan değerler (2 m / 30 m) kullanılır.
    /// </summary>
    public AudioSource Play(AudioClip clip, Vector3 position, float volume = 1f,
                            float pitchVariation = 0.08f, float spatialBlend = 1f,
                            float minDist = -1f, float maxDist = -1f)
    {
        if (clip == null) return null;

        int index = GetSourceIndex();
        AudioSource src = pool[index];

        src.Stop();
        src.transform.position = position;
        src.clip = clip;
        src.mute = false;
        src.volume = Mathf.Clamp01(volume * masterVolume);
        src.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        src.spatialBlend = spatialBlend;

        // Her çalışta mesafeyi yeniden ayarla (havuzdaki kaynak önceki sesten kalan değeri taşımasın)
        float min = minDist > 0f ? minDist : minDistance;
        float max = maxDist > 0f ? maxDist : maxDistance;
        src.minDistance = min;
        src.maxDistance = Mathf.Max(max, min + 0.1f);

        src.Play();

        // Kaynağı, ses bitene kadar meşgul say (isPlaying'in aynı karedeki gecikmesine güvenme)
        busyUntil[index] = Time.unscaledTime + clip.length / Mathf.Max(0.1f, Mathf.Abs(src.pitch)) + 0.05f;

        if (logPlays) LogPlay(clip, src, position, volume);
        return src;
    }

    private int GetSourceIndex()
    {
        float now = Time.unscaledTime;

        // Boşta olan kaynağı bul
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i] != null && now >= busyUntil[i] && !pool[i].isPlaying)
                return i;
        }

        // Yoksa ve limit dolmadıysa yenisini oluştur
        if (pool.Count < maxVoices)
            return CreateSource();

        // Limit dolu: en erken bitecek olan sesi kes ve onu kullan
        int best = 0;
        float earliest = float.MaxValue;
        for (int i = 0; i < pool.Count; i++)
        {
            if (busyUntil[i] < earliest)
            {
                earliest = busyUntil[i];
                best = i;
            }
        }
        return best;
    }

    private int CreateSource()
    {
        GameObject go = new GameObject("Voice_" + pool.Count);
        go.transform.SetParent(transform);
        AudioSource src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = false;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = minDistance;
        src.maxDistance = maxDistance;
        pool.Add(src);
        busyUntil.Add(0f);
        return pool.Count - 1;
    }

    private void LogPlay(AudioClip clip, AudioSource src, Vector3 position, float volume)
    {
        if (listener == null) listener = FindAnyObjectByType<AudioListener>();

        string dist = "AudioListener YOK!";
        if (listener != null)
            dist = Vector3.Distance(listener.transform.position, position).ToString("0.0") + " m";

        int playing = 0;
        foreach (AudioSource s in pool)
            if (s != null && s.isPlaying) playing++;

        Debug.Log($"[SoundManager] > {clip.name} | ses={volume:0.00} | 3D orani={src.spatialBlend:0.0} | " +
                  $"dinleyiciye uzaklik={dist} | duyulma menzili={src.minDistance:0}-{src.maxDistance:0} m | " +
                  $"{src.name} | calan ses sayisi={playing}");
    }

    /// <summary>
    /// Diziden rastgele bir klip seçer; mümkünse bir önceki çalınanı tekrar etmez.
    /// </summary>
    public static AudioClip Pick(AudioClip[] clips, AudioClip avoid = null)
    {
        if (clips == null || clips.Length == 0) return null;
        if (clips.Length == 1) return clips[0];

        AudioClip c;
        int guard = 0;
        do
        {
            c = clips[Random.Range(0, clips.Length)];
        } while (c == avoid && ++guard < 10);
        return c;
    }
}