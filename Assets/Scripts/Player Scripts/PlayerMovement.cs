using HowToCarry.AI;
using HowToCarry.Core;
using TMPro;
using UnityEngine;

namespace HowToCarry.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Yoldaş Ajan Bağlantısı")]
        [SerializeField] private CompanionAgent companionAgent;
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

        [Header("Interaction & Command (OOP)")]
        [SerializeField] private float interactDistance = 3.5f;
        [SerializeField] private MonoBehaviour companionObject; // Inspector'dan CompanionAgent atanacak

        [Header("UI Etkileşim")]
        [SerializeField] private TextMeshProUGUI interactionPromptText;

        private Vector3 moveDirection = Vector3.zero;
        private float rotationX = 0;
        private CharacterController characterController;
        private ICompanionCommand _companion;

        private bool canMove = true;

        // Diyalog ekranı açıkken hareketi dışarıdan kilitlemek için kapsüllenmiş özellik
        public bool CanMove
        {
            get => canMove;
            set => canMove = value;
        }

        // Animator parametre isimleri (Animator penceresindekilerle birebir aynı olmalı)
        private static readonly int WalkHash = Animator.StringToHash("Walk");
        private static readonly int RunHash = Animator.StringToHash("Run");
        private static readonly int IdleHash = Animator.StringToHash("Idle");
        private static readonly int JumpHash = Animator.StringToHash("Jump");

        void Awake()
        {
            if (companionObject != null)
            {
                _companion = companionObject as ICompanionCommand;
            }
        }

        void Start()
        {
            characterController = GetComponent<CharacterController>();
            LockCursor(true);

            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (animator == null)
                Debug.LogWarning("Karakterde Animator bulunamadı, animasyonlar çalışmayacak.", this);
            else
                animator.applyRootMotion = false; // Hareketi sadece CharacterController yapsın
        }

        void Update()
        {
            HandleCompanionCommand();
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

                HandleInteractions();
            }
            HandleInteractions(); // Senin yazdığın E'ye basma kontrolü
            UpdateInteractionDisplay(); // GÖRSEL KONTROL: Ekrana yazı çıkarma
            UpdateAnimator(isRunning);
        }
        private void UpdateInteractionDisplay()
        {
            if (interactionPromptText == null || playerCamera == null) return;

            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            Debug.DrawRay(playerCamera.transform.position, playerCamera.transform.forward * interactDistance, Color.red); // Bu satırı ekle
            // Eğer baktığımız yerde interactDistance sınırları içinde bir IInteractable varsa
            if (Physics.Raycast(ray, out RaycastHit hit, interactDistance) && hit.collider.TryGetComponent(out IInteractable interactable))
            {
                // Arayüzden (Interface) metni çek ve ekrana yaz
                interactionPromptText.text = interactable.GetInteractionPrompt();
            }
            else
            {
                // Boşa bakıyorsak veya obje etkileşime açık değilse yazıyı temizle
                interactionPromptText.text = string.Empty;
            }
        }

        private void HandleInteractions()
        {
            // E Tuşu: NPC veya nesnelerle etkileşim (Raycast)
            if (Input.GetKeyDown(KeyCode.E) && playerCamera != null)
            {
                Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
                if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
                {
                    if (hit.collider.TryGetComponent(out IInteractable interactable))
                    {
                        interactable.Interact();
                    }
                }
            }
        }

        public void LockCursor(bool isLocked)
        {
            Cursor.lockState = isLocked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !isLocked;
            canMove = isLocked;
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
        private void HandleCompanionCommand()
        {
            // Klavyeden 'C' tuşuna basıldığında ajana görevi başlat/durdur emri ver
            if (Input.GetKeyDown(KeyCode.C))
            {
                if (companionAgent != null)
                {
                    companionAgent.ExecuteTaskCommand();
                }
                else
                {
                    Debug.LogWarning("[PlayerController] CompanionAgent referansı atanmamış!");
                }
            }
        }
    }
}