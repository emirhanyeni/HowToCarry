using UnityEngine;

/// <summary>
/// Tutulabilen blok sesleri: ele alma, elden bırakma, düşme/çarpma ve itilme/sürtünme (kayma) sesi.
/// Pickup scriptinin olduğu objeye (bloğa) eklenir.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class BlockAudio : MonoBehaviour
{
    [Header("Ele Alma / Bırakma")]
    [SerializeField] private AudioClip[] pickupClips;
    [SerializeField] private AudioClip[] dropClips;
    [Range(0f, 1f)] [SerializeField] private float pickupVolume = 0.7f;
    [Range(0f, 1f)] [SerializeField] private float dropVolume = 0.6f;

    [Header("Düşme / Çarpma")]
    [Tooltip("Bloğun bir yere çarpınca çıkardığı ses. Çarpma hızına göre güçlenir.")]
    [SerializeField] private AudioClip[] impactClips;
    [Tooltip("Bu çarpma hızının altında ses çalmaz.")]
    [SerializeField] private float minImpactSpeed = 1.5f;
    [Tooltip("Bu hızda ses en yüksek seviyeye ulaşır.")]
    [SerializeField] private float maxImpactSpeed = 15f;
    [Range(0f, 1f)] [SerializeField] private float minImpactVolume = 0.2f;
    [Range(0f, 1f)] [SerializeField] private float maxImpactVolume = 1f;
    [Tooltip("İki çarpma sesi arasındaki en kısa süre (titreşimde spam olmasın).")]
    [SerializeField] private float impactCooldown = 0.12f;

    [Header("İtilme / Sürtünme (kayma) Sesi")]
    [Tooltip("Loop edilen sürtünme sesi. Blok yerde kayarken çalar.")]
    [SerializeField] private AudioClip slideClip;
    [Tooltip("Bu yatay hızın altında kayma sesi çalmaz.")]
    [SerializeField] private float minSlideSpeed = 0.3f;
    [Tooltip("Bu hızda kayma sesi en yüksek seviyeye ulaşır.")]
    [SerializeField] private float maxSlideSpeed = 6f;
    [Range(0f, 1f)] [SerializeField] private float slideMaxVolume = 0.6f;
    [Tooltip("Ses açılma/kapanma yumuşatma hızı.")]
    [SerializeField] private float slideFadeSpeed = 8f;

    private Rigidbody rb;
    private Pickup pickup;
    private AudioSource slideSource;
    private AudioClip lastImpactClip;
    private float lastImpactTime = -10f;
    private bool touching;
    private float targetSlideVolume;
    private float targetSlidePitch = 1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        pickup = GetComponent<Pickup>();

        // Bloğun kendi üzerinde, loop eden kayma sesi kaynağı
        slideSource = gameObject.AddComponent<AudioSource>();
        slideSource.playOnAwake = false;
        slideSource.loop = true;
        slideSource.spatialBlend = 1f;
        slideSource.rolloffMode = AudioRolloffMode.Linear;
        slideSource.minDistance = 2f;
        slideSource.maxDistance = 25f;
        slideSource.volume = 0f;
        slideSource.clip = slideClip;
    }

    private void OnEnable()
    {
        if (pickup != null)
        {
            pickup.PickedUp += HandlePickedUp;
            pickup.Dropped += HandleDropped;
        }
    }

    private void OnDisable()
    {
        if (pickup != null)
        {
            pickup.PickedUp -= HandlePickedUp;
            pickup.Dropped -= HandleDropped;
        }
        if (slideSource != null) slideSource.Stop();
    }

    private void HandlePickedUp()
    {
        SoundManager.Instance.Play(SoundManager.Pick(pickupClips), transform.position, pickupVolume);
    }

    private void HandleDropped()
    {
        SoundManager.Instance.Play(SoundManager.Pick(dropClips), transform.position, dropVolume);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (impactClips == null || impactClips.Length == 0) return;

        float speed = collision.relativeVelocity.magnitude;
        if (speed < minImpactSpeed) return;
        if (Time.time - lastImpactTime < impactCooldown) return;
        lastImpactTime = Time.time;

        float t = Mathf.InverseLerp(minImpactSpeed, maxImpactSpeed, speed);
        float volume = Mathf.Lerp(minImpactVolume, maxImpactVolume, t);

        AudioClip clip = SoundManager.Pick(impactClips, lastImpactClip);
        lastImpactClip = clip;

        Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
        SoundManager.Instance.Play(clip, point, volume, 0.1f);
    }

    private void OnCollisionStay(Collision collision)
    {
        touching = true;
    }

    private void FixedUpdate()
    {
        bool held = pickup != null && pickup.IsHeld;

        Vector3 v = rb.linearVelocity;
        v.y = 0f;
        float speed = v.magnitude;

        if (touching && !held && slideClip != null && speed >= minSlideSpeed)
        {
            float t = Mathf.InverseLerp(minSlideSpeed, maxSlideSpeed, speed);
            targetSlideVolume = Mathf.Lerp(0.15f, 1f, t) * slideMaxVolume;
            targetSlidePitch = Mathf.Lerp(0.85f, 1.15f, t);
        }
        else
        {
            targetSlideVolume = 0f;
        }

        touching = false; // Bir sonraki fizik adımında OnCollisionStay tekrar işaretler
    }

    private void Update()
    {
        if (slideSource == null || slideClip == null) return;

        slideSource.volume = Mathf.MoveTowards(slideSource.volume, targetSlideVolume, slideFadeSpeed * Time.deltaTime);
        slideSource.pitch = Mathf.Lerp(slideSource.pitch, targetSlidePitch, Time.deltaTime * 6f);

        if (slideSource.volume > 0.01f && !slideSource.isPlaying)
            slideSource.Play();
        else if (slideSource.volume <= 0.01f && targetSlideVolume <= 0f && slideSource.isPlaying)
            slideSource.Stop();
    }
}
