using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Basınç plakası. Üzerinde "Block" tag'ine sahip bir obje durunca kapıyı açar (yok eder),
/// blok kalkınca kapıyı geri getirir.
/// Plakadaki Collider "Is Trigger" işaretli olmalı.
/// </summary>
[RequireComponent(typeof(Collider))]
public class PressurePlate : MonoBehaviour
{
    [Header("Ayarlar")]
    [Tooltip("Plakayı basabilen objelerin tag'i")]
    [SerializeField] private string blockTag = "Block";

    [Tooltip("Açılacak kapı")]
    [SerializeField] private Door door;

    [Tooltip("Blok kalkınca kapı geri gelsin mi? Kapalıysa kapı kalıcı olarak yok olur.")]
    [SerializeField] private bool closeWhenReleased = true;

    [Header("Görsel (opsiyonel)")]
    [Tooltip("Basılınca aşağı inecek görsel kısım")]
    [SerializeField] private Transform plateVisual;
    [SerializeField] private float pressDepth = 0.05f;
    [SerializeField] private float moveSpeed = 8f;

    [Header("Eventler (opsiyonel)")]
    public UnityEvent onPressed;
    public UnityEvent onReleased;

    // Plakanın üzerindeki blokları sayıyoruz (birden fazla blok olabilir)
    private readonly HashSet<Collider> blocksOnPlate = new HashSet<Collider>();
    private bool isPressed;
    private Vector3 visualStartPos;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        if (plateVisual != null) visualStartPos = plateVisual.localPosition;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(blockTag)) return;
        blocksOnPlate.Add(other);
        UpdateState();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!blocksOnPlate.Remove(other)) return;
        UpdateState();
    }

    private void Update()
    {
        // Silinen/yok edilen blokları temizle (OnTriggerExit her zaman çağrılmayabilir)
        if (blocksOnPlate.Count > 0 && blocksOnPlate.RemoveWhere(c => c == null || !c.gameObject.activeInHierarchy) > 0)
            UpdateState();

        // Plaka görselini yumuşakça indir/kaldır
        if (plateVisual != null)
        {
            Vector3 target = isPressed ? visualStartPos + Vector3.down * pressDepth : visualStartPos;
            plateVisual.localPosition = Vector3.Lerp(plateVisual.localPosition, target, Time.deltaTime * moveSpeed);
        }
    }

    private void UpdateState()
    {
        bool shouldBePressed = blocksOnPlate.Count > 0;
        if (shouldBePressed == isPressed) return;
        isPressed = shouldBePressed;

        if (isPressed)
        {
            if (door != null) door.Open();
            onPressed?.Invoke();
        }
        else if (closeWhenReleased)
        {
            if (door != null) door.Close();
            onReleased?.Invoke();
        }
    }
}
