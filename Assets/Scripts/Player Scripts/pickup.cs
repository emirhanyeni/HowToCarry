using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Pickup : MonoBehaviour
{
    // Oyuncu için aynı anda sadece tek bir nesne tutulabilsin
    private static Pickup currentHeld;

    [Header("Alma / Fırlatma")]
    [SerializeField] private float pickupRange = 3f;
    [SerializeField] private float throwForce = 12f;

    [Header("Tutma Konumu (Inspector'dan ayarla)")]
    [Tooltip("HoldingMechanic'a göre yerel konum. Z = ileri, Y = yukarı, X = sağ.")]
    [SerializeField] private Vector3 holdOffset = new Vector3(0f, 0f, 1.5f);
    [SerializeField] private bool addObjectSizeToDistance = true;
    [SerializeField] private float extraClearance = 0.1f;

    [Header("Karakterle Çarpışma")]
    [SerializeField] private bool ignorePlayerCollision = true;
    [SerializeField] private Transform playerRoot;

    [Header("Takip")]
    [SerializeField] private float followSpeed = 15f;
    [SerializeField] private float maxHoldDistance = 1.5f;
    [SerializeField] private float maxFollowVelocity = 20f;

    [Header("Bırakma / Atma")]
    [SerializeField] private float collisionRestoreDelay = 0.3f;

    private bool isHolding;
    public bool IsHeld => isHolding;
    public event System.Action PickedUp;
    public event System.Action Dropped;

    private Coroutine restoreRoutine;
    private Rigidbody rb;
    private HoldingMechanic holdingMechanic;
    private Camera mainCam;
    private Collider[] myColliders;
    private Collider[] playerColliders;
    private Animator playerAnimator;

    // --- OTONOM AJAN (AI) TAŞIMA REFERANSLARI ---
    private bool isHeldByAgent;
    private Transform activeHoldTransform;
    private Collider[] activeCarrierColliders;
    private Animator activeCarrierAnimator;

    private static readonly int CarryHash = Animator.StringToHash("Carry");

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        myColliders = GetComponentsInChildren<Collider>();
    }

    private void Start()
    {
        holdingMechanic = HoldingMechanic.Instance;
        mainCam = Camera.main;

        if (holdingMechanic != null)
        {
            if (playerRoot == null)
                playerRoot = holdingMechanic.transform.root;

            playerColliders = playerRoot.GetComponentsInChildren<Collider>();
            playerAnimator = playerRoot.GetComponentInChildren<Animator>();
        }
    }

    private void Update()
    {
        // Ajan tutuyorsa oyuncu klavye girdisiyle müdahale etmesin
        if (isHeldByAgent || holdingMechanic == null) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (isHolding)
                Drop();
            else if (currentHeld == null && IsLookedAt())
                PickupItem();
        }

        if (isHolding && Input.GetMouseButtonDown(1))
            ThrowItem();
    }

    private void FixedUpdate()
    {
        if (!isHolding || activeHoldTransform == null) return;

        Vector3 toTarget = GetHoldPosition() - rb.position;

        // Oyuncu tutarken çok uzaklaşırsa bırak (Ajan eğitimde takılmasın diye ajanda daha esnek)
        if (!isHeldByAgent && toTarget.magnitude > maxHoldDistance)
        {
            Drop();
            return;
        }

        rb.linearVelocity = Vector3.ClampMagnitude(toTarget * followSpeed, maxFollowVelocity);
        rb.angularVelocity = Vector3.zero;
    }

    private Vector3 GetHoldPosition()
    {
        // Ajan tutuyorsa doğrudan ajanın HoldPoint noktasını + boyutu kullan
        if (isHeldByAgent && activeHoldTransform != null)
        {
            Vector3 agentOffset = new Vector3(0f, 0f, addObjectSizeToDistance ? GetObjectRadius() + extraClearance : 0f);
            return activeHoldTransform.TransformPoint(agentOffset);
        }

        Vector3 offset = holdOffset;
        if (addObjectSizeToDistance)
        {
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
        return Mathf.Max(b.extents.x, b.extents.z);
    }

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

    private void SetCarrierCollision(Collider[] carrierCols, bool ignore)
    {
        if (carrierCols == null) return;

        foreach (Collider mine in myColliders)
        {
            foreach (Collider other in carrierCols)
            {
                if (mine != null && other != null)
                    Physics.IgnoreCollision(mine, other, ignore);
            }
        }
    }

    // --- OYUNCU ALMA / BIRAKMA ---
    private void PickupItem()
    {
        isHolding = true;
        isHeldByAgent = false;
        currentHeld = this;

        activeHoldTransform = holdingMechanic.transform;
        activeCarrierColliders = playerColliders;
        activeCarrierAnimator = playerAnimator;

        rb.useGravity = false;
        if (activeCarrierAnimator != null) activeCarrierAnimator.SetBool(CarryHash, true);

        if (restoreRoutine != null)
        {
            StopCoroutine(restoreRoutine);
            restoreRoutine = null;
        }

        if (ignorePlayerCollision)
            SetCarrierCollision(activeCarrierColliders, true);

        PickedUp?.Invoke();
    }

    public void Drop()
    {
        if (!isHolding) return;

        isHolding = false;
        isHeldByAgent = false;
        if (currentHeld == this) currentHeld = null;

        rb.useGravity = true;
        if (activeCarrierAnimator != null) activeCarrierAnimator.SetBool(CarryHash, false);

        if (ignorePlayerCollision && gameObject.activeInHierarchy && activeCarrierColliders != null)
            restoreRoutine = StartCoroutine(RestoreCollisionRoutine(activeCarrierColliders));

        PickedUpOrDroppedCleanup();
        Dropped?.Invoke();
    }

    // --- OTONOM AJAN (ML-AGENTS) ALMA / BIRAKMA METOTLARI ---
    public void AgentPickup(Transform agentHoldPoint, Transform agentRoot, Animator agentAnim)
    {
        if (isHolding) Drop();

        isHolding = true;
        isHeldByAgent = true;
        activeHoldTransform = agentHoldPoint;
        activeCarrierColliders = agentRoot.GetComponentsInChildren<Collider>();
        activeCarrierAnimator = agentAnim;

        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (activeCarrierAnimator != null) activeCarrierAnimator.SetBool(CarryHash, true);

        if (restoreRoutine != null)
        {
            StopCoroutine(restoreRoutine);
            restoreRoutine = null;
        }

        if (ignorePlayerCollision)
            SetCarrierCollision(activeCarrierColliders, true);

        PickedUp?.Invoke();
    }

    public void AgentDrop()
    {
        if (!isHolding) return;
        Drop();
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    private void PickedUpOrDroppedCleanup()
    {
        activeHoldTransform = null;
        activeCarrierAnimator = null;
    }

    private void ThrowItem()
    {
        Drop();
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.AddForce(holdingMechanic.transform.forward * throwForce, ForceMode.Impulse);
    }

    private System.Collections.IEnumerator RestoreCollisionRoutine(Collider[] colsToRestore)
    {
        yield return new WaitForSeconds(collisionRestoreDelay);

        while (OverlapsCarrier(colsToRestore))
            yield return new WaitForFixedUpdate();

        SetCarrierCollision(colsToRestore, false);
        restoreRoutine = null;
    }

    private bool OverlapsCarrier(Collider[] carrierCols)
    {
        if (carrierCols == null) return false;

        foreach (Collider mine in myColliders)
        {
            foreach (Collider other in carrierCols)
            {
                if (mine != null && other != null && mine.bounds.Intersects(other.bounds))
                    return true;
            }
        }
        return false;
    }

    private void OnDisable()
    {
        if (ignorePlayerCollision && activeCarrierColliders != null)
            SetCarrierCollision(activeCarrierColliders, false);

        if (isHolding && activeCarrierAnimator != null)
            activeCarrierAnimator.SetBool(CarryHash, false);

        restoreRoutine = null;
        if (currentHeld == this) currentHeld = null;
        isHolding = false;
        isHeldByAgent = false;
    }
}