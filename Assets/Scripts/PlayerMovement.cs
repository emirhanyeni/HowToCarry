using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    public Camera playerCamera;
    public float walkSpeed = 6f;
    public float runSpeed = 12f;
    public float jumpPower = 7f;
    public float gravity = 10f;
    public float lookSpeed = 2f;
    public float lookXLimit = 45f;
    public float defaultHeight = 2f;
    public float crouchHeight = 1f;
    public float crouchSpeed = 3f;

    [Header("Animasyon")]
    [Tooltip("Boş bırakılırsa çocuk objelerde (UglyCuteMonkey) otomatik aranır.")]
    [SerializeField] private Animator animator;
    [Tooltip("Yerdeyken karakteri yere yapıştıran küçük aşağı kuvvet (isGrounded titremesini önler).")]
    [SerializeField] private float groundedStickForce = 2f;

    private Vector3 moveDirection = Vector3.zero;
    private float rotationX = 0;
    private CharacterController characterController;

    private bool canMove = true;

    // Animator parametre isimleri (Animator penceresindekilerle birebir aynı olmalı)
    private static readonly int WalkHash = Animator.StringToHash("Walk");
    private static readonly int RunHash = Animator.StringToHash("Run");
    private static readonly int IdleHash = Animator.StringToHash("Idle");
    private static readonly int JumpHash = Animator.StringToHash("Jump");

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator == null)
            Debug.LogWarning("Karakterde Animator bulunamadı, animasyonlar çalışmayacak.", this);
        else
            animator.applyRootMotion = false; // Hareketi sadece CharacterController yapsın
    }

    void Update()
    {
        Vector3 forward = transform.TransformDirection(Vector3.forward);
        Vector3 right = transform.TransformDirection(Vector3.right);

        bool isRunning = Input.GetKey(KeyCode.LeftShift);
        float curSpeedX = canMove ? (isRunning ? runSpeed : walkSpeed) * Input.GetAxis("Vertical") : 0;
        float curSpeedY = canMove ? (isRunning ? runSpeed : walkSpeed) * Input.GetAxis("Horizontal") : 0;
        float movementDirectionY = moveDirection.y;
        moveDirection = (forward * curSpeedX) + (right * curSpeedY);

        bool grounded = characterController.isGrounded;

        // Yere inince zıplama animasyonunu kapat
        if (grounded && animator != null)
            animator.SetBool(JumpHash, false);

        if (Input.GetButton("Jump") && canMove && grounded)
        {
            moveDirection.y = jumpPower;
            if (animator != null) animator.SetBool(JumpHash, true);
        }
        else
        {
            moveDirection.y = movementDirectionY;
        }

        if (!grounded)
        {
            moveDirection.y -= gravity * Time.deltaTime;
        }
        else if (moveDirection.y < 0f)
        {
            // Yerdeyken hafifçe aşağı it, isGrounded sürekli true kalsın
            moveDirection.y = -groundedStickForce;
        }

        if (Input.GetKey(KeyCode.R) && canMove)
        {
            characterController.height = crouchHeight;
            walkSpeed = crouchSpeed;
            runSpeed = crouchSpeed;
        }
        else
        {
            characterController.height = defaultHeight;
            walkSpeed = 6f;
            runSpeed = 12f;
        }

        characterController.Move(moveDirection * Time.deltaTime);

        if (canMove)
        {
            rotationX += -Input.GetAxis("Mouse Y") * lookSpeed;
            rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);
            playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
            transform.rotation *= Quaternion.Euler(0, Input.GetAxis("Mouse X") * lookSpeed, 0);
        }

        UpdateAnimator(isRunning);
    }

    private void UpdateAnimator(bool isRunning)
    {
        if (animator == null) return;

        // Sadece yatay hareketi say (zıplama/yerçekimi hareket sayılmasın)
        Vector3 horizontal = new Vector3(moveDirection.x, 0f, moveDirection.z);
        bool moving = canMove && horizontal.magnitude > 0.1f;
        bool running = moving && isRunning;

        animator.SetBool(WalkHash, moving && !running);
        animator.SetBool(RunHash, running);
        animator.SetBool(IdleHash, !moving);
    }
}
