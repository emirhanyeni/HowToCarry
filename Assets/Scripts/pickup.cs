using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Pickup : MonoBehaviour
{
    // Aynı anda sadece tek bir nesne tutulabilsin
    private static Pickup currentHeld;

    [Header("Alma / Fırlatma")]
    [SerializeField] private float pickupRange = 3f;        // Alma menzili
    [SerializeField] private float throwForce = 12f;        // Impulse olarak uygulanır (kütleye bölünür)

    [Header("Tutma Konumu (Inspector'dan ayarla)")]
    [Tooltip("HoldingMechanic'a göre yerel konum. Z = ileri, Y = yukarı, X = sağ. Blok karaktere çarpıyorsa Z değerini artır.")]
    [SerializeField] private Vector3 holdOffset = new Vector3(0f, 0f, 1.5f);
    [Tooltip("Açıkken nesnenin boyutu kadar ek mesafe otomatik eklenir (büyük bloklar karaktere girmesin).")]
    [SerializeField] private bool addObjectSizeToDistance = true;
    [Tooltip("Ek güvenlik mesafesi (otomatik boyuta eklenir).")]
    [SerializeField] private float extraClearance = 0.1f;

    [Header("Karakterle Çarpışma")]
    [Tooltip("Tutarken karakterin collider'ları ile çarpışmayı kapatır (titremeyi önler).")]
    [SerializeField] private bool ignorePlayerCollision = true;
    [Tooltip("Boş bırakılırsa HoldingMechanic'ın en üst parent'ı (karakter) otomatik kullanılır.")]
    [SerializeField] private Transform playerRoot;

    [Header("Takip")]
    [SerializeField] private float followSpeed = 15f;       // Tutma noktasına gitme hızı
    [SerializeField] private float maxHoldDistance = 1.5f;  // Tutma noktasından bu kadar uzaklaşırsa bırak (duvara takılma vb.)
    [Tooltip("Takip sırasında nesnenin ulaşabileceği en yüksek hız (ani sıçramaları önler).")]
    [SerializeField] private float maxFollowVelocity = 20f;

    [Header("Bırakma / Atma")]
    [Tooltip("Bıraktıktan sonra karakterle çarpışma en az bu kadar saniye kapalı kalır (atış titremesini önler).")]
    [SerializeField] private float collisionRestoreDelay = 0.3f;

    private bool isHolding;
    private Coroutine restoreRoutine;
    private Rigidbody rb;
    private HoldingMechanic holdingMechanic;
    private Camera mainCam;
    private Collider[] myColliders;
    private Collider[] playerColliders;

    // --- ANİMASYON (eklenen) ---
    private Animator playerAnimator;
    private static readonly int CarryHash = Animator.StringToHash("Carry");

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        // Hareket akıcı görünsün diye her zaman açık (tutarken, atarken ve düşerken)
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        myColliders = GetComponentsInChildren<Collider>();
    }

    private void Start()
    {
        holdingMechanic = HoldingMechanic.Instance;
        mainCam = Camera.main;

        if (holdingMechanic == null)
        {
            Debug.LogError("Sahnede HoldingMechanic bulunamadı! Pickup çalışmayacak.", this);
            return;
        }

        if (playerRoot == null)
            playerRoot = holdingMechanic.transform.root;

        playerColliders = playerRoot.GetComponentsInChildren<Collider>();

        // --- ANİMASYON (eklenen) ---
        playerAnimator = playerRoot.GetComponentInChildren<Animator>();
        if (playerAnimator == null)
            Debug.LogWarning("Karakterde Animator bulunamadı, Carry animasyonu çalışmayacak.", this);
    }

    private void Update()
    {
        if (holdingMechanic == null) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (isHolding)
                Drop();
            else if (currentHeld == null && IsLookedAt())
                PickupItem();
        }

        // Sağ tık ile fırlat
        if (isHolding && Input.GetMouseButtonDown(1))
            ThrowItem();
    }

    // Fizik işlemleri FixedUpdate'te yapılmalı
    private void FixedUpdate()
    {
        if (!isHolding) return;

        Vector3 toTarget = GetHoldPosition() - rb.position;

        // Nesne bir yere takılıp tutma noktasından çok uzakta kalırsa bırak
        if (toTarget.magnitude > maxHoldDistance)
        {
            Drop();
            return;
        }

        // Parent yapmadan, yumuşak şekilde tutma noktasına çek
        rb.linearVelocity = Vector3.ClampMagnitude(toTarget * followSpeed, maxFollowVelocity);
        rb.angularVelocity = Vector3.zero;
    }

    // Nesnenin tutulacağı dünya konumu
    private Vector3 GetHoldPosition()
    {
        Vector3 offset = holdOffset;

        if (addObjectSizeToDistance)
        {
            // Nesnenin ileri yöndeki yarım boyutu + güvenlik payı kadar öteye koy
            offset.z += GetObjectRadius() + extraClearance;
        }

        return holdingMechanic.transform.TransformPoint(offset);
    }

    private float GetObjectRadius()
    {
        Bounds b = new Bounds(transform.position, Vector3.zero);
        bool first = true;
        foreach (Collider c in myColliders)
        {
            if (first) { b = c.bounds; first = false; }
            else b.Encapsulate(c.bounds);
        }
        // Bounds extents en büyük yarım boyut; küp için güvenli bir değer
        return Mathf.Max(b.extents.x, b.extents.z);
    }

    // Oyuncu bu nesneye bakıyor mu ve menzilde mi?
    private bool IsLookedAt()
    {
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null) return false;

        Ray ray = new Ray(mainCam.transform.position, mainCam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, pickupRange, ~0, QueryTriggerInteraction.Ignore))
        {
            return hit.rigidbody == rb || hit.collider.transform.IsChildOf(transform);
        }
        return false;
    }

    private void SetPlayerCollision(bool ignore)
    {
        if (playerColliders == null) return;

        foreach (Collider mine in myColliders)
        {
            foreach (Collider other in playerColliders)
            {
                if (mine != null && other != null)
                    Physics.IgnoreCollision(mine, other, ignore);
            }
        }
    }

    private void PickupItem()
    {
        isHolding = true;
        currentHeld = this;

        rb.useGravity = false;

        // --- ANİMASYON (eklenen) ---
        if (playerAnimator != null) playerAnimator.SetBool(CarryHash, true);

        // Önceki bırakmadan kalan bekleyen "çarpışmayı aç" işlemi varsa iptal et
        if (restoreRoutine != null)
        {
            StopCoroutine(restoreRoutine);
            restoreRoutine = null;
        }

        if (ignorePlayerCollision)
            SetPlayerCollision(true);
    }

    private void Drop()
    {
        isHolding = false;
        if (currentHeld == this) currentHeld = null;

        rb.useGravity = true;

        // --- ANİMASYON (eklenen) ---
        if (playerAnimator != null) playerAnimator.SetBool(CarryHash, false);

        // Çarpışmayı hemen açma: nesne hâlâ karakterin içindeyse fizik motoru
        // onları ayırmaya çalışır ve titreme/sekme olur.
        if (ignorePlayerCollision && gameObject.activeInHierarchy)
            restoreRoutine = StartCoroutine(RestorePlayerCollisionRoutine());
    }

    private void ThrowItem()
    {
        Drop();

        // Tutarken verilen takip hızı atışın üstüne eklenmesin
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.AddForce(holdingMechanic.transform.forward * throwForce, ForceMode.Impulse);
    }

    private System.Collections.IEnumerator RestorePlayerCollisionRoutine()
    {
        yield return new WaitForSeconds(collisionRestoreDelay);

        // Nesne hâlâ karakterin collider'larıyla iç içeyse bekle
        while (OverlapsPlayer())
            yield return new WaitForFixedUpdate();

        SetPlayerCollision(false);
        restoreRoutine = null;
    }

    private bool OverlapsPlayer()
    {
        if (playerColliders == null) return false;

        foreach (Collider mine in myColliders)
        {
            foreach (Collider other in playerColliders)
            {
                if (mine != null && other != null && mine.bounds.Intersects(other.bounds))
                    return true;
            }
        }
        return false;
    }

    // Nesne devre dışı kalır / yok olursa static referans takılı kalmasın
    private void OnDisable()
    {
        if (ignorePlayerCollision)
            SetPlayerCollision(false);

        // --- ANİMASYON (eklenen) ---
        if (isHolding && playerAnimator != null)
            playerAnimator.SetBool(CarryHash, false);

        restoreRoutine = null;
        if (currentHeld == this) currentHeld = null;
        isHolding = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, pickupRange);

        // Tutma noktasını Scene görünümünde göster (oyun çalışırken)
        if (holdingMechanic != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(GetHoldPosition(), 0.1f);
        }
    }
}
