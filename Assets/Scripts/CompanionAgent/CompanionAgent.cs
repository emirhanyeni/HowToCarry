using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
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
        public Transform agentSpawnPoint;
        public Transform cubeSpawnPoint;

        [Tooltip("OPSİYONEL: Küpe giderken geçilmesi gereken ara noktalar (rampa başı, zıplama noktası vb.). Sırayla.")]
        public Transform[] waypointsToCube;
        [Tooltip("OPSİYONEL: Küp alındıktan sonra plakaya giderken geçilecek ara noktalar. Sırayla.")]
        public Transform[] waypointsToPlate;

        [HideInInspector] public Rigidbody cubeRb;
        [HideInInspector] public Vector3 agentStartPos;
        [HideInInspector] public Quaternion agentStartRot;
        [HideInInspector] public Vector3 cubeStartPos;
        [HideInInspector] public Quaternion cubeStartRot;
        [HideInInspector] public float killY;
    }

    /// <summary>
    /// Görev: 1) CompanionCube'u bul ve havada önünde tut  2) PressurePlate'e götür  3) tam üstüne bırak.
    /// Eğitim: Behavior Type = Default + mlagents-learn.  Oyun: trainingMode = false + .onnx modeli.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class CompanionAgent : Agent, ICompanionCommand
    {
        // Behavior Parameters > Space Size bu değere EŞİT olmalı (aşağıdaki context menu bunu otomatik ayarlar)
        public const int ObservationSize = 22;
        private const int WallRayCount = 7;
        private static readonly float[] GroundProbeDistances = { 0.5f, 1.2f, 2.0f, 3.0f };

        [Header("Animasyon")]
        [SerializeField] private Animator animator;
        private Vector3 _lastPosition;
        private float _smoothedSpeed = 0f;

        // Animator'daki parametrelerin C# karşılıkları
        private static readonly int WalkHash = Animator.StringToHash("Walk");
        private static readonly int RunHash = Animator.StringToHash("Run");
        private static readonly int IdleHash = Animator.StringToHash("Idle");
        private static readonly int JumpHash = Animator.StringToHash("Jump");
        private static readonly int CarryingHash = Animator.StringToHash("Carrying");

        // İleride puzzle küpü taşıdığında veya zıpladığında bunları true yapacağız
        public bool isCarryingItem = false;
        public bool isJumping = false;

        [Header("Odalar (sağ tık > Auto Fill Rooms From Scene)")]
        [SerializeField] private PuzzleRoomSetup[] rooms;
        [SerializeField] private int startingRoomIndex = 0;

        [Header("Oyuncu Takibi (sadece oyun modunda)")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private float followStopDistance = 2.5f;
        [SerializeField] private float teleportDistance = 15f; // EKLENEN SATIR: Bu mesafeyi aşarsa ışınlanır

        [Header("Küp Tutma Noktası (CompanionAgent altındaki HoldPoint)")]
        [SerializeField] private Transform agentHoldPoint;

        [Header("Hareket")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float turnSpeed = 180f;
        [SerializeField] private float jumpPower = 6.5f;
        [SerializeField] private float gravity = 12f;
        [SerializeField] private float groundedStickForce = 2f;

        [Header("Görev Mesafeleri")]
        [SerializeField] private float pickupDistance = 2.0f;
        [SerializeField] private float pickupHeightTolerance = 1.5f;
        [SerializeField] private float waypointRadius = 1.8f;
        [Tooltip("Küp plakanın merkezine yatayda bu kadar yaklaşınca bırakılır")]
        [SerializeField] private float plateDropRadius = 0.5f;
        [SerializeField] private float plateDropHeight = 2.5f;
        [Tooltip("Küp ajandan bu kadar uzaklaşırsa (takılma) tutma noktasına geri ışınlanır")]
        [SerializeField] private float maxCarryDistance = 3.5f;

        [Header("Ödüller")]
        [SerializeField] private float progressScale = 0.1f;
        [SerializeField] private float pickupReward = 1.0f;
        [SerializeField] private float waypointReward = 0.3f;
        [SerializeField] private float successReward = 3.0f;
        [SerializeField] private float fallPenalty = 1.0f;
        [Tooltip("Süre dolana kadar toplam birikecek zaman cezası")]
        [SerializeField] private float timeoutPenalty = 0.5f;
        [SerializeField] private float jumpPenalty = 0.001f;

        [Header("Düşme")]
        [Tooltip("Odadaki en alçak önemli noktanın (spawn, küp, plaka, waypoint) bu kadar altı = düştü")]
        [SerializeField] private float fallMargin = 5f;

        [Header("Sensörler")]
        [SerializeField] private float sensorHeight = 0.5f;
        [SerializeField] private float wallRayLength = 8f;
        [SerializeField] private float groundProbeLength = 6f;

        [Header("Eğitim & Oyun Modu")]
        [Tooltip("Eğitimde TRUE. Oyunda (model ile) FALSE yapın.")]
        [SerializeField] private bool trainingMode = true;
        [SerializeField] private bool randomizeRoomInTraining = true;
        [SerializeField] private float spawnYawJitter = 25f;

        private CharacterController _controller;
        private int _currentRoomIndex;
        private float _verticalVelocity;
        private bool _isTaskActive;
        private int _cubeWp;
        private int _plateWp;
        private float _prevDist;
        private bool _solved;
        private int _episodeRoom;
        private int _episodesStarted;
        private float _episodeStartTime;
        private int[] _roomEpisodes;
        private int[] _roomWins;
        private readonly RaycastHit[] _hits = new RaycastHit[16];

        private PuzzleRoomSetup CurrentRoom =>
            (rooms != null && _currentRoomIndex >= 0 && _currentRoomIndex < rooms.Length) ? rooms[_currentRoomIndex] : null;

        // ------------------------------------------------------------------ KURULUM
        

        public override void Initialize()
        {
            _controller = GetComponent<CharacterController>();

            if (animator == null) animator = GetComponentInChildren<Animator>();
            _lastPosition = transform.position;

            if (agentHoldPoint == null)
            {
                var hp = new GameObject("AgentHoldPoint");
                hp.transform.SetParent(transform);
                hp.transform.localPosition = new Vector3(0f, 1.0f, 1.2f);
                hp.transform.localRotation = Quaternion.identity;
                agentHoldPoint = hp.transform;
            }

            if (!trainingMode) MaxStep = 0; // oyunda bölüm zaman aşımı olmasın

            ValidateSetup();

            int n = rooms != null ? rooms.Length : 0;
            _roomEpisodes = new int[n];
            _roomWins = new int[n];

            for (int i = 0; i < n; i++)
            {
                var r = rooms[i];
                if (r.targetCube != null) r.cubeRb = r.targetCube.GetComponent<Rigidbody>();

                r.agentStartPos = r.agentSpawnPoint != null ? r.agentSpawnPoint.position : transform.position;
                r.agentStartRot = r.agentSpawnPoint != null ? r.agentSpawnPoint.rotation : transform.rotation;
                r.cubeStartPos = r.cubeSpawnPoint != null ? r.cubeSpawnPoint.position
                               : (r.targetCube != null ? r.targetCube.transform.position : r.agentStartPos);
                r.cubeStartRot = r.cubeSpawnPoint != null ? r.cubeSpawnPoint.rotation
                               : (r.targetCube != null ? r.targetCube.transform.rotation : Quaternion.identity);

                float low = Mathf.Min(r.agentStartPos.y, r.cubeStartPos.y);
                if (r.pressurePlate != null) low = Mathf.Min(low, r.pressurePlate.transform.position.y);
                low = MinY(r.waypointsToCube, low);
                low = MinY(r.waypointsToPlate, low);
                r.killY = low - fallMargin;
            }

            _currentRoomIndex = Mathf.Clamp(startingRoomIndex, 0, Mathf.Max(0, n - 1));
        }
        private void Update()
        {
            // Burada ajanın önceden var olan diğer hareket veya yapay zeka kodları olabilir...

            UpdateAnimations(); // BİZİM EKLEDİĞİMİZ TETİKLEYİCİ SATIR
        }
        private void UpdateAnimations()
        {
            // Dönüşüm kontrolü (T-Pose engelleme)
            if (animator == null || !animator.gameObject.activeInHierarchy)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (animator == null) return;

            // Anlık hızı hesapla
            float currentSpeed = (transform.position - _lastPosition).magnitude / Time.deltaTime;
            _lastPosition = transform.position;

            // YENİ: Anlık hızı doğrudan vermek yerine, yumuşatarak geçiş yap (Sarsıntıları emer)
            _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, currentSpeed, Time.deltaTime * 10f);

            // Yumuşatılmış hızı baz al
            bool isMoving = _smoothedSpeed > 0.1f;
            bool isRunning = _smoothedSpeed > 4.5f;

            // Animasyonları tetikle
            animator.SetBool(WalkHash, isMoving && !isRunning);
            animator.SetBool(RunHash, isRunning);
            animator.SetBool(IdleHash, !isMoving);
            animator.SetBool(JumpHash, isJumping);
            animator.SetBool(CarryingHash, isCarryingItem);
        }
        private static float MinY(Transform[] pts, float current)
        {
            if (pts == null) return current;
            foreach (var p in pts) if (p != null) current = Mathf.Min(current, p.position.y);
            return current;
        }

        private void ValidateSetup()
        {
            if (GetComponent<DecisionRequester>() == null)
                Debug.LogError("[CompanionAgent] Decision Requester YOK! Sağ tık > '1) Setup ML-Agents Components' çalıştırın.", this);

            var bp = GetComponent<BehaviorParameters>();
            if (bp != null && bp.BrainParameters.VectorObservationSize != ObservationSize)
                Debug.LogError($"[CompanionAgent] Vector Observation Space Size {ObservationSize} olmalı (şu an {bp.BrainParameters.VectorObservationSize}).", this);

            if (rooms == null || rooms.Length == 0)
            {
                Debug.LogError("[CompanionAgent] Rooms boş! Sağ tık > '2) Auto Fill Rooms From Scene'.", this);
                return;
            }
            for (int i = 0; i < rooms.Length; i++)
            {
                if (rooms[i].targetCube == null || rooms[i].pressurePlate == null)
                    Debug.LogError($"[CompanionAgent] Rooms[{i}] için Target Cube veya Pressure Plate atanmamış!", this);
            }
        }

        // ------------------------------------------------------------------ OYUN KOMUTU ('C' tuşu)
        public void ExecuteTaskCommand()
        {
            // 1. DURUM: Ajan zaten görevdeyse GÖREVİ İPTAL ET
            if (_isTaskActive)
            {
                _isTaskActive = false;

                // Eğer görev iptal edildiğinde ajan küpü elinde tutuyorsa, olduğu yere bıraksın
                if (CurrentRoom != null && CurrentRoom.targetCube != null && CurrentRoom.targetCube.IsHeld)
                {
                    CurrentRoom.targetCube.AgentDrop();
                }

                Debug.Log("[CompanionAgent] Görev iptal edildi! Ajan elindekini bıraktı ve takibe dönüyor.");
                return;
            }

            // 2. DURUM: Ajan boştaysa (takipteyse) EN DÜŞÜK İNDEKSLİ ÇÖZÜLMEMİŞ ODAYI BUL
            if (!trainingMode && rooms != null)
            {
                int unsolvedRoomIndex = -1;

                for (int i = 0; i < rooms.Length; i++)
                {
                    // Eğer odanın plakası var ve henüz BASILMAMIŞSA (isPressed == false)
                    if (rooms[i] != null && rooms[i].pressurePlate != null && !rooms[i].pressurePlate.isPressed)
                    {
                        unsolvedRoomIndex = i;
                        break; // İlk çözülmemiş odayı bulduğumuzda döngüden çık
                    }
                }

                if (unsolvedRoomIndex != -1)
                {
                    _currentRoomIndex = unsolvedRoomIndex; // Bulunan odayı güncel görev yap
                }
                else
                {
                    Debug.Log("[CompanionAgent] Bütün odalar zaten çözülmüş! Yapılacak görev yok.");
                    return;
                }
            }

            if (CurrentRoom == null)
            {
                Debug.LogWarning("[CompanionAgent] Çözülecek aktif oda kalmadı!");
                return;
            }

            // Görevi Başlat
            _isTaskActive = true;
            _solved = false;
            _cubeWp = 0;
            _plateWp = 0;
            _episodeStartTime = Time.time;
            _prevDist = GoalDistance(CurrentRoom);
            Debug.Log($"[CompanionAgent] '{CurrentRoom.roomName}' (İndeks: {_currentRoomIndex}) görevi başladı.");
        }

        private void ResetRoom(PuzzleRoomSetup room, bool resetAgent, bool jitter)
        {
            if (room == null) return;

            if (room.targetCube != null && room.targetCube.IsHeld)
                room.targetCube.AgentDrop();

            if (resetAgent)
            {
                _controller.enabled = false;
                transform.SetPositionAndRotation(room.agentStartPos, room.agentStartRot);
                if (jitter) transform.Rotate(0f, UnityEngine.Random.Range(-spawnYawJitter, spawnYawJitter), 0f);
                _controller.enabled = true;
                _verticalVelocity = 0f;
            }

            if (room.targetCube != null && room.cubeRb != null)
            {
                room.cubeRb.linearVelocity = Vector3.zero;
                room.cubeRb.angularVelocity = Vector3.zero;
                room.cubeRb.position = room.cubeStartPos;
                room.cubeRb.rotation = room.cubeStartRot;
                room.targetCube.transform.SetPositionAndRotation(room.cubeStartPos, room.cubeStartRot);
            }
            Physics.SyncTransforms();
        }

        // ------------------------------------------------------------------ HEDEF MANTIĞI
        private static bool NextWaypoint(Transform[] wps, ref int idx, out Vector3 pos)
        {
            pos = Vector3.zero;
            if (wps == null) return false;
            while (idx < wps.Length && wps[idx] == null) idx++;
            if (idx >= wps.Length) return false;
            pos = wps[idx].position;
            return true;
        }

        /// <summary>goal = gidilecek nokta, reference = mesafenin ölçüleceği nokta (ajan ya da son aşamada küp)</summary>
        private bool TryGetGoal(PuzzleRoomSetup room, out Vector3 goal, out Vector3 reference)
        {
            goal = reference = transform.position;
            if (room == null || room.targetCube == null || room.pressurePlate == null) return false;

            Vector3 wp;
            if (!room.targetCube.IsHeld)
            {
                goal = NextWaypoint(room.waypointsToCube, ref _cubeWp, out wp) ? wp : room.targetCube.transform.position;
                return true;
            }
            if (NextWaypoint(room.waypointsToPlate, ref _plateWp, out wp))
            {
                goal = wp;
                return true;
            }
            goal = room.pressurePlate.transform.position;
            reference = room.targetCube.transform.position; // son aşamada KÜP plakaya yaklaşmalı
            return true;
        }

        private float GoalDistance(PuzzleRoomSetup room)
        {
            return TryGetGoal(room, out var goal, out var reference) ? Vector3.Distance(reference, goal) : 0f;
        }

        private bool AdvanceWaypoint(Transform[] wps, ref int idx)
        {
            if (!NextWaypoint(wps, ref idx, out var pos)) return false;
            if (Vector3.Distance(transform.position, pos) > waypointRadius) return false;
            idx++;
            AddReward(waypointReward);
            return true;
        }

        // ------------------------------------------------------------------ GÖZLEMLER (22 float)
        public override void CollectObservations(VectorSensor sensor)
        {
            var room = CurrentRoom;
            if (!TryGetGoal(room, out var goal, out var reference))
            {
                sensor.AddObservation(new float[ObservationSize]);
                return;
            }

            // 1) Güncel hedef: yerel yön (3) + mesafe (1)
            Vector3 toGoal = goal - reference;
            Vector3 localGoal = transform.InverseTransformDirection(toGoal.normalized);
            sensor.AddObservation(localGoal);
            sensor.AddObservation(Mathf.Clamp01(toGoal.magnitude / 30f));

            // 2) Plaka: yerel yön x,z (2) + mesafe (1)
            Vector3 toPlate = room.pressurePlate.transform.position - transform.position;
            Vector3 localPlate = transform.InverseTransformDirection(toPlate.normalized);
            sensor.AddObservation(localPlate.x);
            sensor.AddObservation(localPlate.z);
            sensor.AddObservation(Mathf.Clamp01(toPlate.magnitude / 30f));

            // 3) Durum: küp elde mi, yerde mi, dikey hız, ileri hız (4)
            sensor.AddObservation(room.targetCube.IsHeld ? 1f : 0f);
            sensor.AddObservation(_controller.isGrounded ? 1f : 0f);
            sensor.AddObservation(Mathf.Clamp(_verticalVelocity / jumpPower, -1f, 1f));
            sensor.AddObservation(Mathf.Clamp(Vector3.Dot(_controller.velocity, transform.forward) / moveSpeed, -1f, 1f));

            // 4) Duvar/engel ışınları: -90..+90 derece, 7 adet (1 = boş, 0 = çok yakın)
            Vector3 origin = transform.position + Vector3.up * sensorHeight;
            for (int i = 0; i < WallRayCount; i++)
            {
                float angle = -90f + i * (180f / (WallRayCount - 1));
                Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * transform.forward;
                sensor.AddObservation(CastNormalized(origin, dir, wallRayLength, room));
            }

            // 5) Zemin probları: ileride zemin var mı? (1 = boşluk/uçurum, küçük = zemin yakın)
            for (int i = 0; i < GroundProbeDistances.Length; i++)
            {
                Vector3 p = transform.position + transform.forward * GroundProbeDistances[i] + Vector3.up * (sensorHeight + 0.5f);
                sensor.AddObservation(CastNormalized(p, Vector3.down, groundProbeLength, room));
            }
        }

        private float CastNormalized(Vector3 origin, Vector3 dir, float maxDist, PuzzleRoomSetup room)
        {
            int n = Physics.RaycastNonAlloc(origin, dir, _hits, maxDist, ~0, QueryTriggerInteraction.Ignore);
            float best = maxDist;
            for (int i = 0; i < n; i++)
            {
                var c = _hits[i].collider;
                if (c.transform.IsChildOf(transform)) continue; // kendimiz
                if (room != null && room.targetCube != null && room.targetCube.IsHeld &&
                    c.transform.IsChildOf(room.targetCube.transform)) continue; // elimizdeki küp
                if (_hits[i].distance < best) best = _hits[i].distance;
            }
            return best / maxDist;
        }

        // ------------------------------------------------------------------ AKSİYONLAR
        public override void OnActionReceived(ActionBuffers actions)
        {
            if (!_isTaskActive)
            {
                IdleOrFollowPlayer();
                return;
            }

            float moveInput = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
            float turnInput = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
            bool jump = actions.DiscreteActions[0] == 1;

            MoveAgent(moveInput, turnInput, jump);
            UpdateTask();
            CheckFall();
        }

        private void MoveAgent(float moveInput, float turnInput, bool wantsJump)
        {
            float dt = Time.fixedDeltaTime;
            transform.Rotate(0f, turnInput * turnSpeed * dt, 0f);

            bool grounded = _controller.isGrounded;
            if (grounded && _verticalVelocity < 0f)
                _verticalVelocity = -groundedStickForce;

            if (wantsJump && grounded)
            {
                _verticalVelocity = jumpPower;
                AddReward(-jumpPenalty);
                isJumping = true; // YENİ: Zıpladığında animasyonu tetikle
            }
            else if (!grounded)
            {
                _verticalVelocity -= gravity * dt;
            }

            // YENİ: Yere değdiği an zıplama animasyonunu kapat
            if (grounded && _verticalVelocity <= 0)
            {
                isJumping = false;
            }

            float speed = moveInput >= 0f ? moveInput : moveInput * 0.5f;
            Vector3 move = transform.forward * (speed * moveSpeed);
            move.y = _verticalVelocity;
            _controller.Move(move * dt);
        }

        private void UpdateTask()
        {
            var room = CurrentRoom;
            if (room == null || room.targetCube == null || room.pressurePlate == null) return;
            var cube = room.targetCube;

            AddReward(MaxStep > 0 ? -timeoutPenalty / MaxStep : -0.0001f);

            if (!cube.IsHeld)
            {
                if (AdvanceWaypoint(room.waypointsToCube, ref _cubeWp))
                    _prevDist = GoalDistance(room);

                // Küpü eline al (havada önünde asılı kalır)
                Vector3 d = cube.transform.position - transform.position;
                float dy = Mathf.Abs(d.y);
                d.y = 0f;
                if (d.magnitude <= pickupDistance && dy <= pickupHeightTolerance)
                {
                    cube.AgentPickup(agentHoldPoint, transform, null);
                    AddReward(pickupReward);
                    _prevDist = GoalDistance(room);
                    isCarryingItem = true; // YENİ: Küpü eline aldığında taşıma animasyonuna geç
                    return;
                }
            }
            else
            {
                if (AdvanceWaypoint(room.waypointsToPlate, ref _plateWp))
                    _prevDist = GoalDistance(room);

                // Küp takılıp geride kaldıysa tutma noktasına getir
                if (agentHoldPoint != null &&
                    Vector3.Distance(cube.transform.position, agentHoldPoint.position) > maxCarryDistance)
                {
                    room.cubeRb.position = agentHoldPoint.position;
                    cube.transform.position = agentHoldPoint.position;
                }

                // Tüm waypoint'ler geçildiyse ve KÜP plakanın tam üstündeyse bırak
                if (!NextWaypoint(room.waypointsToPlate, ref _plateWp, out _))
                {
                    Vector3 plate = room.pressurePlate.transform.position;
                    Vector3 c = cube.transform.position;
                    Vector3 flat = c - plate;
                    float dy = flat.y;
                    flat.y = 0f;
                    if (flat.magnitude <= plateDropRadius && dy <= plateDropHeight && dy >= -1f)
                    {
                        DropOnPlate(room);
                        return;
                    }
                }
            }

            // Hedefe yaklaşma ödülü (potansiyel tabanlı: ileri-geri gitmekle sömürülemez)
            float dist = GoalDistance(room);
            AddReward(Mathf.Clamp((_prevDist - dist) * progressScale, -0.05f, 0.05f));
            _prevDist = dist;
        }

        private void DropOnPlate(PuzzleRoomSetup room)
        {
            var cube = room.targetCube;
            cube.AgentDrop();
            isCarryingItem = false;
            // Plakanın tam merkezine yerleştir (yerçekimi yüzeye oturtur)
            Vector3 target = room.pressurePlate.transform.position + Vector3.up * 0.4f;
            room.cubeRb.linearVelocity = Vector3.zero;
            room.cubeRb.angularVelocity = Vector3.zero;
            room.cubeRb.position = target;
            cube.transform.position = target;
            Physics.SyncTransforms();

            _solved = true;
            AddReward(successReward);

            float duration = Time.time - _episodeStartTime;
            if (trainingMode)
            {
                if (_episodeRoom < _roomWins.Length && (_roomEpisodes[_episodeRoom] + 1) % 50 == 0)
                    Debug.Log($"[CompanionAgent] '{room.roomName}' çözüldü ({duration:F1}sn). " +
                              $"Oda başarısı: {_roomWins[_episodeRoom] + 1}/{_roomEpisodes[_episodeRoom] + 1}");
                EndEpisode();
            }
            else
            {
                Debug.Log($"[CompanionAgent] '{room.roomName}' ÇÖZÜLDÜ! Süre: {duration:F1}sn");
                _isTaskActive = false;
                if (_currentRoomIndex < rooms.Length - 1)
                {
                    _currentRoomIndex++;
                    Debug.Log($"[CompanionAgent] Sıradaki oda: '{CurrentRoom.roomName}'. 'C' ile emir verin.");
                }
                else Debug.Log("[CompanionAgent] Tüm odalar tamamlandı!");
            }
        }

        private void CheckFall()
        {
            var room = CurrentRoom;
            if (room == null) return;

            bool agentFell = transform.position.y < room.killY;
            bool cubeFell = room.targetCube != null && room.targetCube.transform.position.y < room.killY;
            if (!agentFell && !cubeFell) return;

            AddReward(-fallPenalty);
            if (trainingMode)
            {
                EndEpisode();
            }
            else
            {
                ResetRoom(room, resetAgent: true, jitter: false);
                _cubeWp = 0;
                _plateWp = 0;
                _prevDist = GoalDistance(room);
            }
        }
        // Oyun modunda görev yokken oyuncuyu takip et
        private void IdleOrFollowPlayer()
        {
            float dt = Time.fixedDeltaTime;
            bool grounded = _controller.isGrounded;
            if (grounded && _verticalVelocity < 0f) _verticalVelocity = -groundedStickForce;
            else if (!grounded) _verticalVelocity -= gravity * dt;

            float moveAmount = 0f;
            if (!trainingMode && playerTransform != null)
            {
                Vector3 toPlayer = playerTransform.position - transform.position;
                toPlayer.y = 0f;
                float currentDistance = toPlayer.magnitude;

                // 1. DURUM: Ajan çok uzaklaştıysa veya takıldıysa oyuncunun arkasına IŞINLA
                if (currentDistance > teleportDistance)
                {
                    _controller.enabled = false; // Işınlarken CharacterController'ı kapatmak zorunludur
                                                 // Oyuncunun 1.5 metre arkasına ve hafifçe yukarısına (zemine düşmesi için) ışınla
                    transform.position = playerTransform.position - (playerTransform.forward * 1.5f) + (Vector3.up * 1f);
                    transform.rotation = playerTransform.rotation;
                    _controller.enabled = true;
                    return; // Işınlanma karesinde yürümeyi iptal et
                }
                // 2. DURUM: Ajan normal takip mesafesindeyse yürüyerek gel
                else if (currentDistance > followStopDistance)
                {
                    Quaternion targetRot = Quaternion.LookRotation(toPlayer.normalized);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * dt);
                    moveAmount = 1f;
                }
            }

            Vector3 move = transform.forward * (moveAmount * moveSpeed);
            move.y = _verticalVelocity;
            _controller.Move(move * dt);
        }

        // ------------------------------------------------------------------ MANUEL TEST (Behavior Type = Heuristic Only)
        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var cont = actionsOut.ContinuousActions;
            cont[0] = 0f;
            cont[1] = 0f;
            if (Input.GetKey(KeyCode.W)) cont[0] = 1f;
            if (Input.GetKey(KeyCode.S)) cont[0] = -1f;
            if (Input.GetKey(KeyCode.D)) cont[1] = 1f;
            if (Input.GetKey(KeyCode.A)) cont[1] = -1f;
            var disc = actionsOut.DiscreteActions;
            disc[0] = Input.GetKey(KeyCode.Space) ? 1 : 0;
        }

        // ------------------------------------------------------------------ EDITÖR YARDIMCILARI
        [ContextMenu("1) Setup ML-Agents Components")]
        private void SetupMlAgentsComponents()
        {
            var dr = GetComponent<DecisionRequester>();
            if (dr == null) dr = gameObject.AddComponent<DecisionRequester>();
            dr.DecisionPeriod = 5;
            dr.TakeActionsBetweenDecisions = true;

            var bp = GetComponent<BehaviorParameters>();
            if (bp == null) bp = gameObject.AddComponent<BehaviorParameters>();
            bp.BehaviorName = "CompanionBehavior";
            bp.BehaviorType = BehaviorType.Default;
            bp.BrainParameters.VectorObservationSize = ObservationSize;
            bp.BrainParameters.NumStackedVectorObservations = 1;
            bp.BrainParameters.ActionSpec = new ActionSpec(2, new[] { 2 });
            bp.UseChildSensors = true;

            MaxStep = 4000;

            var cc = GetComponent<CharacterController>();
            cc.slopeLimit = 45f;
            cc.stepOffset = 0.3f;
            cc.skinWidth = 0.08f;
            cc.minMoveDistance = 0f;

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(dr);
            UnityEditor.EditorUtility.SetDirty(bp);
            UnityEditor.EditorUtility.SetDirty(cc);
            UnityEditor.EditorUtility.SetDirty(this);
#endif
            Debug.Log("[CompanionAgent] Decision Requester (period 5, Take Actions Between Decisions), Behavior Parameters (22 obs, 2 continuous, 1 discrete[2]) ve MaxStep=4000 ayarlandı.");
        }

        [ContextMenu("2) Auto Fill Rooms From Scene")]
        private void AutoFillRooms()
        {
            GameObject[] cubes, plates;
            try
            {
                cubes = GameObject.FindGameObjectsWithTag("CompanionCube");
                plates = GameObject.FindGameObjectsWithTag("PressurePlate");
            }
            catch (UnityException e)
            {
                Debug.LogError("[CompanionAgent] 'CompanionCube' veya 'PressurePlate' tag'i tanımlı değil: " + e.Message);
                return;
            }

            var list = new List<PuzzleRoomSetup>();
            for (int i = 0; i < 16; i++)
            {
                var agentSp = GameObject.Find($"AgentSpawnPoint_Room{i}");
                if (agentSp == null) break;
                var cubeSp = GameObject.Find($"PuzzleCubeSpawn_Room{i}");

                Vector3 cubeRef = cubeSp != null ? cubeSp.transform.position : agentSp.transform.position;
                var room = new PuzzleRoomSetup
                {
                    roomName = $"Room{i}",
                    agentSpawnPoint = agentSp.transform,
                    cubeSpawnPoint = cubeSp != null ? cubeSp.transform : null,
                    waypointsToCube = new Transform[0],
                    waypointsToPlate = new Transform[0]
                };

                var c = Nearest(cubes, cubeRef);
                if (c != null) room.targetCube = c.GetComponent<Pickup>();
                var p = Nearest(plates, agentSp.transform.position);
                if (p != null) room.pressurePlate = p.GetComponent<PressurePlate>();
                list.Add(room);
            }

            rooms = list.ToArray();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
            Debug.Log($"[CompanionAgent] {rooms.Length} oda dolduruldu. Inspector'da her odanın küp/plaka eşleşmesini KONTROL edin.");
        }

        private static GameObject Nearest(GameObject[] objs, Vector3 pos)
        {
            GameObject best = null;
            float bestD = float.MaxValue;
            foreach (var o in objs)
            {
                float d = (o.transform.position - pos).sqrMagnitude;
                if (d < bestD) { bestD = d; best = o; }
            }
            return best;
        }
    }
}
