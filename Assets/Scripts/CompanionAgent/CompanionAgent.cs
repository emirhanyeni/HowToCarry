using System;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using HowToCarry.Core;
using HowToCarry.Mechanics;

namespace HowToCarry.AI
{
    [Serializable]
    public class PuzzleRoomSetup
    {
        public string roomName = "Room";
        public Pickup targetCube;
        public PressurePlate pressurePlate;
        [Tooltip("Bu odada küp alındıktan sonra plakaya giderken geçilecek rampa/köşe/platform noktaları")]
        public Transform[] waypointsToPlate;
        [Tooltip("Eğitimde veya düşme durumunda ajanın ve küpün bu odada doğacağı noktalar")]
        public Transform agentSpawnPoint;
        public Transform cubeSpawnPoint;

        [HideInInspector] public Rigidbody cubeRb;
        [HideInInspector] public Vector3 initialAgentPos;
        [HideInInspector] public Quaternion initialAgentRot;
        [HideInInspector] public Vector3 initialCubePos;
        [HideInInspector] public Quaternion initialCubeRot;
    }

    [RequireComponent(typeof(CharacterController))]
    public class CompanionAgent : Agent, ICompanionCommand
    {
        [Header("4 Odalı Bulmaca Kurulumu")]
        [Tooltip("Sırasıyla Oda 1, Oda 2, Oda 3 ve Oda 4'ün küp ve plaka bilgilerini buraya ekleyin.")]
        [SerializeField] private PuzzleRoomSetup[] rooms;
        [SerializeField] private int startingRoomIndex = 0;

        [Header("Oyuncu Takibi ve Taşıma Noktası")]
        [Tooltip("Ajan bulmaca çözmüyorken (odalar arası geçişte) takip edeceği ana oyuncu.")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private float followStopDistance = 2.5f;
        [Tooltip("Ajanın küpü önünde tutacağı boş child obje")]
        [SerializeField] private Transform agentHoldPoint;

        [Header("Hareket ve Parkur (CharacterController)")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float turnSpeed = 180f;
        [SerializeField] private float jumpPower = 6.5f;
        [SerializeField] private float gravity = 12f;
        [SerializeField] private float groundedStickForce = 2f;
        [SerializeField] private float pickupDistance = 2.2f;
        [SerializeField] private float dropDistance = 1.6f;

        [Header("Düşme / Ceza Sınırı")]
        [SerializeField] private float fallDepthLimit = -3.5f;

        [Header("Eğitim & Oyun Modu")]
        [Tooltip("ML-Agents eğitimi sırasında TRUE, oyun içinde oyuncudan 'C' emri beklerken FALSE yapın.")]
        [SerializeField] private bool trainingMode = true;
        [Tooltip("Eğitim modundayken her bölümde 4 odadan rastgele birini seçip eğitir (Tek .onnx için).")]
        [SerializeField] private bool randomizeRoomInTraining = true;
        [SerializeField] private Animator animator;

        private CharacterController _controller;
        private int _currentRoomIndex;
        private float _verticalVelocity;
        private bool _isTaskActive;
        private int _currentWaypointIndex;
        private float _prevDistToCurrentGoal;

        // İstatistikler (README Raporu için)
        private int _totalEpisodes;
        private int _successfulEpisodes;
        private float _episodeStartTime;

        // Animator Hash'leri
        private static readonly int WalkHash = Animator.StringToHash("Walk");
        private static readonly int RunHash = Animator.StringToHash("Run");
        private static readonly int IdleHash = Animator.StringToHash("Idle");
        private static readonly int JumpHash = Animator.StringToHash("Jump");

        private PuzzleRoomSetup CurrentRoom =>
            (rooms != null && rooms.Length > 0 && _currentRoomIndex < rooms.Length) ? rooms[_currentRoomIndex] : null;

        public override void Initialize()
        {
            _controller = GetComponent<CharacterController>();

            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (animator != null)
                animator.applyRootMotion = false;

            if (agentHoldPoint == null)
            {
                GameObject hp = new GameObject("AgentHoldPoint");
                hp.transform.SetParent(transform);
                hp.transform.localPosition = new Vector3(0f, 0.4f, 1.1f);
                hp.transform.localRotation = Quaternion.identity;
                agentHoldPoint = hp.transform;
            }

            // Tüm odaların başlangıç konumlarını kaydet
            if (rooms != null)
            {
                foreach (var room in rooms)
                {
                    if (room.targetCube != null)
                    {
                        room.cubeRb = room.targetCube.GetComponent<Rigidbody>();
                        room.initialCubePos = room.cubeSpawnPoint != null ? room.cubeSpawnPoint.position : room.targetCube.transform.position;
                        room.initialCubeRot = room.cubeSpawnPoint != null ? room.cubeSpawnPoint.rotation : room.targetCube.transform.rotation;
                    }

                    room.initialAgentPos = room.agentSpawnPoint != null ? room.agentSpawnPoint.position : transform.position;
                    room.initialAgentRot = room.agentSpawnPoint != null ? room.agentSpawnPoint.rotation : transform.rotation;
                }
            }

            _currentRoomIndex = Mathf.Clamp(startingRoomIndex, 0, rooms != null ? Mathf.Max(0, rooms.Length - 1) : 0);
        }

        private void OnEnable()
        {
            SubscribeAllPlates(true);
        }

        private void OnDisable()
        {
            SubscribeAllPlates(false);
        }

        private void SubscribeAllPlates(bool subscribe)
        {
            if (rooms == null) return;
            foreach (var room in rooms)
            {
                if (room.pressurePlate != null)
                {
                    if (subscribe)
                        room.pressurePlate.onPressed.AddListener(HandlePuzzleSolved);
                    else
                        room.pressurePlate.onPressed.RemoveListener(HandlePuzzleSolved);
                }
            }
        }

        // Oyuncu 'C' tuşuna bastığında çalışır
        public void ExecuteTaskCommand()
        {
            if (CurrentRoom == null)
            {
                Debug.LogWarning("[CompanionAgent] Çözülecek aktif oda kalmadı!");
                return;
            }

            _isTaskActive = true;
            _currentWaypointIndex = 0;
            _episodeStartTime = Time.time;
            _prevDistToCurrentGoal = Vector3.Distance(transform.position, GetCurrentTargetPosition());
            Debug.Log($"[CompanionAgent] '{CurrentRoom.roomName}' için görev emri alındı! Küp alınıp plakaya götürülüyor...");
        }

        public override void OnEpisodeBegin()
        {
            if (rooms == null || rooms.Length == 0) return;

            // Eğitim modundaysak 4 odadan rastgele birini seç ki tek .onnx tüm odaları öğrensin
            if (trainingMode && randomizeRoomInTraining)
            {
                _currentRoomIndex = UnityEngine.Random.Range(0, rooms.Length);
            }

            _isTaskActive = trainingMode;
            _episodeStartTime = Time.time;
            _currentWaypointIndex = 0;
            _verticalVelocity = 0f;

            ResetCurrentRoomEntities(resetAgentPosition: trainingMode);
            _prevDistToCurrentGoal = Vector3.Distance(transform.position, GetCurrentTargetPosition());
        }

        private void ResetCurrentRoomEntities(bool resetAgentPosition)
        {
            var room = CurrentRoom;
            if (room == null) return;

            if (room.targetCube != null && room.targetCube.IsHeld)
                room.targetCube.AgentDrop();

            if (resetAgentPosition)
            {
                _controller.enabled = false;
                transform.position = room.initialAgentPos;
                transform.rotation = room.initialAgentRot;
                _controller.enabled = true;
            }

            if (room.targetCube != null && room.cubeRb != null)
            {
                room.cubeRb.linearVelocity = Vector3.zero;
                room.cubeRb.angularVelocity = Vector3.zero;
                room.targetCube.transform.position = room.initialCubePos;
                room.targetCube.transform.rotation = room.initialCubeRot;
            }
        }

        private Vector3 GetCurrentTargetPosition()
        {
            var room = CurrentRoom;
            if (room == null || room.targetCube == null || room.pressurePlate == null)
                return transform.position;

            // 1. Küp henüz alınmadıysa hedef BU ODANIN KÜPÜDÜR
            if (!room.targetCube.IsHeld)
                return room.targetCube.transform.position;

            // 2. Küp alındıysa ve ara Waypoint'ler varsa sıradaki Waypoint hedeftir
            if (room.waypointsToPlate != null && _currentWaypointIndex < room.waypointsToPlate.Length)
            {
                if (room.waypointsToPlate[_currentWaypointIndex] != null)
                    return room.waypointsToPlate[_currentWaypointIndex].position;
            }

            // 3. Tüm Waypoint'ler geçildiyse hedef BU ODANIN BASINÇ PLAKASIDIR
            return room.pressurePlate.transform.position;
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            var room = CurrentRoom;
            if (room == null || room.targetCube == null || room.pressurePlate == null)
            {
                sensor.AddObservation(new float[10]);
                return;
            }

            Vector3 currentGoal = GetCurrentTargetPosition();
            Vector3 toGoal = currentGoal - transform.position;
            Vector3 localToGoal = transform.InverseTransformDirection(toGoal.normalized);

            // 1. Güncel hedefe olan yerel yön (x, y, z) ve normalize mesafe (4 float)
            sensor.AddObservation(localToGoal.x);
            sensor.AddObservation(localToGoal.y);
            sensor.AddObservation(localToGoal.z);
            sensor.AddObservation(Mathf.Clamp01(toGoal.magnitude / 25f));

            // 2. Basınç plakasına olan yerel yön (x, z) ve mesafe (3 float)
            Vector3 toPlate = room.pressurePlate.transform.position - transform.position;
            Vector3 localToPlate = transform.InverseTransformDirection(toPlate.normalized);
            sensor.AddObservation(localToPlate.x);
            sensor.AddObservation(localToPlate.z);
            sensor.AddObservation(Mathf.Clamp01(toPlate.magnitude / 25f));

            // 3. Durum bayrakları (3 float) -> Toplam = 10 Observation
            sensor.AddObservation(room.targetCube.IsHeld ? 1.0f : 0.0f);
            sensor.AddObservation(_controller.isGrounded ? 1.0f : 0.0f);
            sensor.AddObservation(Mathf.Clamp(_verticalVelocity / jumpPower, -1f, 1f));
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            // OYUN MODUNDA GÖREV YOKKEN: Oyuncuyu odadan odaya takip et
            if (!_isTaskActive)
            {
                if (!trainingMode && playerTransform != null)
                {
                    FollowPlayerBetweenRooms();
                }
                else
                {
                    UpdateAnimations(0f, _controller.isGrounded);
                }
                return;
            }

            float moveInput = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
            float turnInput = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
            int jumpAction = actions.DiscreteActions[0];

            transform.Rotate(Vector3.up * (turnInput * turnSpeed * Time.fixedDeltaTime));

            bool grounded = _controller.isGrounded;
            if (grounded && _verticalVelocity < 0f)
                _verticalVelocity = -groundedStickForce;

            if (jumpAction == 1 && grounded)
            {
                _verticalVelocity = jumpPower;
                if (animator != null) animator.SetBool(JumpHash, true);
            }

            if (!grounded)
                _verticalVelocity -= gravity * Time.fixedDeltaTime;

            Vector3 move = transform.forward * (moveInput * moveSpeed);
            move.y = _verticalVelocity;
            _controller.Move(move * Time.fixedDeltaTime);

            UpdateAnimations(moveInput, grounded);
            HandleCubeInteractionAndRewards();
            CheckFallPenalty();
        }

        private void FollowPlayerBetweenRooms()
        {
            bool grounded = _controller.isGrounded;
            if (grounded && _verticalVelocity < 0f)
                _verticalVelocity = -groundedStickForce;
            else if (!grounded)
                _verticalVelocity -= gravity * Time.fixedDeltaTime;

            Vector3 toPlayer = playerTransform.position - transform.position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;

            float moveAmount = 0f;
            if (distance > followStopDistance)
            {
                Quaternion targetRot = Quaternion.LookRotation(toPlayer.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.fixedDeltaTime);
                moveAmount = 1f;
            }

            Vector3 move = transform.forward * (moveAmount * moveSpeed);
            move.y = _verticalVelocity;
            _controller.Move(move * Time.fixedDeltaTime);

            UpdateAnimations(moveAmount, grounded);
        }

        private void HandleCubeInteractionAndRewards()
        {
            var room = CurrentRoom;
            if (room == null || room.targetCube == null || room.pressurePlate == null) return;

            AddReward(-0.0005f);

            // 1. KÜPÜ ALMA KONTROLÜ
            if (!room.targetCube.IsHeld)
            {
                float distToCube = Vector3.Distance(transform.position, room.targetCube.transform.position);
                if (distToCube <= pickupDistance)
                {
                    room.targetCube.AgentPickup(agentHoldPoint, transform, animator);
                    AddReward(0.5f);
                    _prevDistToCurrentGoal = Vector3.Distance(transform.position, GetCurrentTargetPosition());
                    return;
                }
            }
            else
            {
                // 2. WAYPOINT GEÇİŞ KONTROLÜ
                if (room.waypointsToPlate != null && _currentWaypointIndex < room.waypointsToPlate.Length)
                {
                    float distToWp = Vector3.Distance(transform.position, room.waypointsToPlate[_currentWaypointIndex].position);
                    if (distToWp < 1.8f)
                    {
                        AddReward(0.3f);
                        _currentWaypointIndex++;
                        _prevDistToCurrentGoal = Vector3.Distance(transform.position, GetCurrentTargetPosition());
                        return;
                    }
                }

                // 3. PLAKAYA BIRAKMA KONTROLÜ
                float distToPlate = Vector3.Distance(transform.position, room.pressurePlate.transform.position);
                if (distToPlate <= dropDistance)
                {
                    room.targetCube.AgentDrop();
                    room.targetCube.transform.position = room.pressurePlate.transform.position + Vector3.up * 0.4f;
                }
            }

            float curDist = Vector3.Distance(transform.position, GetCurrentTargetPosition());
            float delta = _prevDistToCurrentGoal - curDist;
            AddReward(Mathf.Clamp(delta * 0.05f, -0.05f, 0.05f));
            _prevDistToCurrentGoal = curDist;
        }

        private void CheckFallPenalty()
        {
            var room = CurrentRoom;
            if (room == null) return;

            bool agentFell = transform.position.y < (room.initialAgentPos.y + fallDepthLimit);
            bool cubeFell = room.targetCube != null && room.targetCube.transform.position.y < (room.initialCubePos.y + fallDepthLimit);

            if (agentFell || cubeFell)
            {
                AddReward(-1.0f);
                _totalEpisodes++;
                if (trainingMode)
                    EndEpisode();
                else
                    ResetCurrentRoomEntities(resetAgentPosition: true);
            }
        }

        private void HandlePuzzleSolved()
        {
            if (!_isTaskActive) return;

            AddReward(2.5f);
            _totalEpisodes++;
            _successfulEpisodes++;

            float duration = Time.time - _episodeStartTime;
            float rate = (_successfulEpisodes / (float)_totalEpisodes) * 100f;
            Debug.Log($"[CompanionAgent] '{CurrentRoom?.roomName}' ÇÖZÜLDÜ! Süre: {duration:F2}sn | Başarı: %{rate:F1} ({_successfulEpisodes}/{_totalEpisodes})");

            if (trainingMode)
            {
                EndEpisode();
            }
            else
            {
                // Görevi bitir, sıradaki odaya geç ve oyuncudan yeni 'C' emrini bekle!
                _isTaskActive = false;
                UpdateAnimations(0f, true);

                if (_currentRoomIndex < rooms.Length - 1)
                {
                    _currentRoomIndex++;
                    Debug.Log($"[CompanionAgent] Sıradaki oda hazır: '{CurrentRoom.roomName}'. Yeni odada 'C' tuşuna basarak emir verebilirsiniz.");
                }
                else
                {
                    Debug.Log("[CompanionAgent] Tüm odaların bulmacaları tamamlandı!");
                }
            }
        }

        private void UpdateAnimations(float moveInput, bool grounded)
        {
            if (animator == null) return;

            if (grounded)
                animator.SetBool(JumpHash, false);

            bool isMoving = Mathf.Abs(moveInput) > 0.1f;
            animator.SetBool(WalkHash, isMoving);
            animator.SetBool(RunHash, false);
            animator.SetBool(IdleHash, !isMoving);
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var cont = actionsOut.ContinuousActions;
            cont[0] = Input.GetAxis("Vertical");
            cont[1] = Input.GetAxis("Horizontal");

            var disc = actionsOut.DiscreteActions;
            disc[0] = Input.GetKey(KeyCode.Space) ? 1 : 0;
        }
    }
}