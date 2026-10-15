using UnityEngine;

namespace HowToCarry.Mechanics
{
    public class DoorController : MonoBehaviour
    {
        [SerializeField] private PressurePlate linkedPlate;
        [SerializeField] private Vector3 openOffset = new Vector3(0f, 4f, 0f);
        [SerializeField] private float speed = 3f;

        private Vector3 _closedPosition;
        private Vector3 _targetPosition;
        public bool IsOpen { get; private set; }

        private void Awake()
        {
            _closedPosition = transform.position;
            _targetPosition = _closedPosition;
        }

        private void OnEnable()
        {
            if (linkedPlate != null)
            {
                linkedPlate.OnPlateActivated += OpenDoor;
                linkedPlate.OnPlateDeactivated += CloseDoor;
            }
        }

        private void OnDisable()
        {
            if (linkedPlate != null)
            {
                linkedPlate.OnPlateActivated -= OpenDoor;
                linkedPlate.OnPlateDeactivated -= CloseDoor;
            }
        }

        private void Update()
        {
            transform.position = Vector3.MoveTowards(transform.position, _targetPosition, speed * Time.deltaTime);
        }

        public void OpenDoor()
        {
            IsOpen = true;
            _targetPosition = _closedPosition + openOffset;
        }

        public void CloseDoor()
        {
            IsOpen = false;
            _targetPosition = _closedPosition;
        }
    }
}