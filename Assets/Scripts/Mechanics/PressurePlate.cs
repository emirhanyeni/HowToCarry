using System;
using UnityEngine;

namespace HowToCarry.Mechanics
{
    public class PressurePlate : MonoBehaviour
    {
        [SerializeField] private string targetTag = "CompanionCube";

        public event Action OnPlateActivated;
        public event Action OnPlateDeactivated;

        public bool IsActivated { get; private set; }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag(targetTag) && !IsActivated)
            {
                IsActivated = true;
                OnPlateActivated?.Invoke();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag(targetTag) && IsActivated)
            {
                IsActivated = false;
                OnPlateDeactivated?.Invoke();
            }
        }
    }
}