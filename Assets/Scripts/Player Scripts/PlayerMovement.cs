using HowToCarry.AI;
using HowToCarry.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HowToCarry.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Yoldaş Ajan Bağlantısı")]
        [SerializeField] private CompanionAgent companionAgent;
        public Camera playerCamera;
        public float walkSpeed = 4f;
        public float runSpeed = 8f;
        public float jumpPower = 5.5f;
        public float gravity = 22f;
        [Tooltip("Düşerken yerçekimi bu değerle çarpılır. Büyüttükçe düşüş hızlanır, süzülme hissi azalır.")]
        public float fallMultiplier = 1.6f;
        public float lookSpeed = 2f;
        public float lookXLimit = 45f;
        public float defaultHeight = 2f;
        public float crouchHeight = 1f;
        public float crouchSpeed = 2f;

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

        [Header("Stamina")]
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float staminaDrainPerSecond = 20f;   // Koşarken saniyede harcanan
        [SerializeField] private float staminaRegenPerSecond = 15f;   // Dinlenirken saniyede dolan
        [SerializeField] private float regenDelay = 1.5f;             // Koşmayı bıraktıktan sonra dolmaya başlama süresi
        [SerializeField] private float minStaminaToRun = 15f;         // Bitince tekrar koşmak için gereken minimum
        [SerializeField] private Image staminaFill;                   // StaminaFill buraya atanacak

        private float currentStamina;
        private float regenTimer;
        private bool exhausted;

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
            currentStamina = maxStamina;

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

            bool wantsRun = Input.GetKey(KeyCode.LeftShift) && canMove &&
                            (Mathf.Abs(Input.GetAxis("Vertical")) > 0.1f || Mathf.Abs(Input.GetAxis("Horizontal")) > 0.1f);
            bool isRunning = UpdateStamina(wantsRun);
            bool crouching = Input.GetKey(KeyCode.R) && canMove;
            float walkNow = crouching ? crouchSpeed : walkSpeed;
            float runNow = crouching ? crouchSpeed : runSpeed;
            float curSpeedX = canMove ? (isRunning ? runNow : walkNow) * Input.GetAxis("Vertical") : 0;
            float curSpeedY = canMove ? (isRunning ? runNow : walkNow) * Input.GetAxis("Horizontal") : 0;
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
                // Düşerken (y hızı negatif) yerçekimi daha güçlü olsun
                float gravityNow = moveDirection.y < 0f ? gravity * fallMultiplier : gravity;
                moveDirection.y -= gravityNow * Time.deltaTime;
            }
            else if (moveDirection.y < 0f)
            {
                // Yerdeyken hafifçe aşağı it, isGrounded sürekli true kalsın
                moveDirection.y = -groundedStickForce;
            }

            characterController.height = crouching ? crouchHeight : defaultHeight;

            characterController.Move(moveDirection * Time.deltaTime);

            if (canMove)
            {
                rotationX += -Input.GetAxis("Mouse Y") * lookSpeed;
                rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);
                playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
                transform.rotation *= Quaternion.Euler(0, Input.GetAxis("Mouse X") * lookSpeed, 0);
            }
            HandleInteractions(); // Senin yazdığın E'ye basma kontrolü
            UpdateInteractionDisplay(); // GÖRSEL KONTROL: Ekrana yazı çıkarma
            UpdateAnimator(isRunning);
        }
        private void UpdateInteractionDisplay()
        {
            if (interactionPromptText == null || playerCamera == null) return;
            // YENİ: Eğer karakter kilitliyse (diyalogdaysa) yazıyı anında sil ve taramayı durdur
            if (!canMove)
            {
                interactionPromptText.text = string.Empty;
                return;
            }
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

        private bool UpdateStamina(bool wantsRun)
        {
            // Stamina tükendiyse belirli bir seviyeye gelene kadar koşmayı engelle
            if (exhausted && currentStamina >= minStaminaToRun)
                exhausted = false;

            bool running = wantsRun && !exhausted && currentStamina > 0f;

            if (running)
            {
                currentStamina -= staminaDrainPerSecond * Time.deltaTime;
                regenTimer = regenDelay;

                if (currentStamina <= 0f)
                {
                    currentStamina = 0f;
                    exhausted = true;
                    running = false;
                }
            }
            else
            {
                if (regenTimer > 0f)
                    regenTimer -= Time.deltaTime;
                else
                    currentStamina = Mathf.Min(maxStamina, currentStamina + staminaRegenPerSecond * Time.deltaTime);
            }

            if (staminaFill != null)
                staminaFill.fillAmount = currentStamina / maxStamina;

            return running;
        }

        private void HandleInteractions()
        {
            if (!canMove) return;
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
            // YENİ EKLENEN KONTROL: Eğer animatör yoksa VEYA dönüşüm geçirdiğimiz için maymun animatörü kapandıysa, yeni (insan) animatörü bul.
            if (animator == null || !animator.gameObject.activeInHierarchy)
            {
                animator = GetComponentInChildren<Animator>();
            }

            // animator bulunamadıysa patlamamak için güvenlik çıkışı
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
            if (Input.GetKeyDown(KeyCode.C) && canMove)
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