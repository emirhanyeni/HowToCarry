using UnityEngine;

/// <summary>
/// Oyuncu sesleri: yürüme, koşma, çömelirken yürüme, zıplama, iniş ve yüksekten düşme.
/// PlayerMovement scriptine DOKUNMADAN çalışır; CharacterController'ı izler.
/// Oyuncu objesine (PlayerMovement'ın olduğu obje) eklenir.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerAudio : MonoBehaviour
{
    [Header("Adım Sesleri (birden fazla klip atarsan rastgele seçilir)")]
    [SerializeField] private AudioClip[] walkClips;
    [SerializeField] private AudioClip[] runClips;

    [Tooltip("Bir adım sesi için katedilmesi gereken mesafe (metre). Küçültürsen adımlar sıklaşır.")]
    [SerializeField] private float walkStride = 3f;
    [SerializeField] private float runStride = 4.5f;
    [SerializeField] private float crouchStride = 2f;

    [Range(0f, 1f)] [SerializeField] private float walkVolume = 0.5f;
    [Range(0f, 1f)] [SerializeField] private float runVolume = 0.8f;
    [Range(0f, 1f)] [SerializeField] private float crouchVolume = 0.25f;

    [Tooltip("Bu hızın altında adım sesi çalmaz (duvara yaslanıp yürürken ses gelmesin).")]
    [SerializeField] private float minMoveSpeed = 0.5f;

    [Header("Zıplama")]
    [SerializeField] private AudioClip[] jumpClips;
    [Range(0f, 1f)] [SerializeField] private float jumpVolume = 0.7f;

    [Header("İniş / Yüksekten Düşme")]
    [Tooltip("Normal iniş sesi (zıplayıp inince).")]
    [SerializeField] private AudioClip[] landClips;
    [Range(0f, 1f)] [SerializeField] private float landVolume = 0.6f;
    [Tooltip("Bundan daha kısa düşüşlerde iniş sesi çalmaz (merdiven, küçük tümsek).")]
    [SerializeField] private float minLandDistance = 0.4f;

    [Tooltip("Yüksekten düşme sesi (ağır iniş).")]
    [SerializeField] private AudioClip[] highFallClips;
    [Tooltip("Bu yükseklikten (metre) fazla düşünce 'yüksekten düşme' sesi çalar. Normal zıplama ~2.5m'dir.")]
    [SerializeField] private float highFallDistance = 4f;
    [Tooltip("Bu yükseklikte yüksekten düşme sesi en yüksek seviyeye çıkar.")]
    [SerializeField] private float maxFallDistance = 15f;

    private CharacterController cc;
    private bool wasGrounded = true;
    private float peakY;
    private float stepAccumulator;
    private AudioClip lastStepClip;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        peakY = transform.position.y;
    }

    // PlayerMovement.Update'ten sonra çalışsın diye LateUpdate kullanıyoruz
    private void LateUpdate()
    {
        bool grounded = cc.isGrounded;
        Vector3 vel = cc.velocity;
        float horizontalSpeed = new Vector3(vel.x, 0f, vel.z).magnitude;

        // Yerden ayrıldık ve yukarı hızımız var -> zıplama
        if (wasGrounded && !grounded && vel.y > 1f)
        {
            Play(jumpClips, jumpVolume);
        }

        // Havadayken en yüksek noktayı takip et (düşme mesafesi için)
        if (grounded) peakY = transform.position.y;
        else peakY = Mathf.Max(peakY, transform.position.y);

        // Yere indik
        if (!wasGrounded && grounded)
        {
            HandleLanding(peakY - transform.position.y);
        }

        // Adım sesleri
        if (grounded && horizontalSpeed > minMoveSpeed)
        {
            stepAccumulator += horizontalSpeed * Time.deltaTime;

            bool crouching = Input.GetKey(KeyCode.R);
            bool running = !crouching && Input.GetKey(KeyCode.LeftShift);

            float stride = crouching ? crouchStride : (running ? runStride : walkStride);
            if (stepAccumulator >= stride)
            {
                stepAccumulator = 0f;
                float vol = crouching ? crouchVolume : (running ? runVolume : walkVolume);
                AudioClip[] set = (running && runClips != null && runClips.Length > 0) ? runClips : walkClips;
                PlayStep(set, vol);
            }
        }
        else if (!grounded)
        {
            stepAccumulator = 0f;
        }

        wasGrounded = grounded;
    }

    private void HandleLanding(float fallDistance)
    {
        stepAccumulator = 0f;

        if (fallDistance >= highFallDistance)
        {
            float t = Mathf.InverseLerp(highFallDistance, maxFallDistance, fallDistance);
            Play(highFallClips, Mathf.Lerp(0.7f, 1f, t));
        }
        else if (fallDistance >= minLandDistance)
        {
            Play(landClips, landVolume);
        }
    }

    private void PlayStep(AudioClip[] clips, float volume)
    {
        AudioClip clip = SoundManager.Pick(clips, lastStepClip);
        if (clip == null) return;
        lastStepClip = clip;
        SoundManager.Instance.Play(clip, transform.position, volume, 0.08f, 0f);
    }

    private void Play(AudioClip[] clips, float volume)
    {
        AudioClip clip = SoundManager.Pick(clips);
        if (clip == null) return;
        SoundManager.Instance.Play(clip, transform.position, volume, 0.05f, 0f);
    }
}
