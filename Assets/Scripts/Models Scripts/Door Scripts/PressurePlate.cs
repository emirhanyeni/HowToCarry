using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Basınç plakası. Sadece Inspector'da atanan objenin (veya onun child'larının) üzerinde
/// durması kapıyı açar (yok eder), obje kalkınca kapıyı geri getirir.
/// Plakadaki Collider "Is Trigger" işaretli olmalı.
/// </summary>
namespace HowToCarry.Mechanics { 
[RequireComponent(typeof(Collider))]
public class PressurePlate : MonoBehaviour
{
    [Header("Ayarlar")]
    [Tooltip("Plakayı sadece bu obje açabilir (sahnedeki objeyi buraya sürükle)")]
    [SerializeField] private GameObject activator;

    [Tooltip("Açılacak kapı")]
    [SerializeField] private Door door;

    [Tooltip("Obje kalkınca kapı geri gelsin mi? Kapalıysa kapı kalıcı olarak yok olur.")]
    [SerializeField] private bool closeWhenReleased = true;

    [Header("Görsel (opsiyonel)")]
    [Tooltip("Basılınca aşağı inecek görsel kısım")]
    [SerializeField] private Transform plateVisual;
    [SerializeField] private float pressDepth = 0.05f;
    [SerializeField] private float moveSpeed = 8f;

    [Header("Eventler (opsiyonel)")]
    public UnityEvent onPressed;
    public UnityEvent onReleased;

    // Plakanın üzerindeki collider'lar (activator birden fazla collider'a sahip olabilir)
    private readonly HashSet<Collider> blocksOnPlate = new HashSet<Collider>();
    private bool isPressed;
    private Vector3 visualStartPos;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        if (plateVisual != null) visualStartPos = plateVisual.localPosition;
    }

    // Çarpan collider, atanan objeye ya da onun child'larına ait mi?
    private bool IsActivator(Collider other)
    {
        if (activator == null) return false;
        return other.transform.IsChildOf(activator.transform);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsActivator(other)) return;
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
        // Silinen/yok edilen collider'ları temizle (OnTriggerExit her zaman çağrılmayabilir)
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
}