using System.Collections;
using UnityEngine;

/// <summary>
/// Kapı. Open() çağrılınca küçülerek yok olur, Close() çağrılınca geri gelir.
/// Kapı objesinin üzerinde bir Collider olmalı (oyuncuyu engellemesi için).
/// </summary>
public class Door : MonoBehaviour
{
    [Header("Ayarlar")]
    [Tooltip("Kapanma/açılma animasyon süresi. 0 yaparsan anında yok olur.")]
    [SerializeField] private float animationDuration = 0.3f;

    private Collider[] colliders;
    private Vector3 originalScale;
    private Coroutine routine;

    private void Awake()
    {
        colliders = GetComponentsInChildren<Collider>();
        originalScale = transform.localScale;
    }

    public void Open()
    {
        // Oyuncu geçebilsin diye collider'ı hemen kapat
        SetColliders(false);
        StartAnimation(Vector3.zero, disableAtEnd: true);
    }

    public void Close()
    {
        gameObject.SetActive(true);
        SetColliders(true);
        StartAnimation(originalScale, disableAtEnd: false);
    }

    private void StartAnimation(Vector3 targetScale, bool disableAtEnd)
    {
        if (routine != null) StopCoroutine(routine);

        if (animationDuration <= 0f)
        {
            transform.localScale = targetScale;
            if (disableAtEnd) gameObject.SetActive(false);
            return;
        }

        // Obje kapalıyken coroutine başlatılamaz, bu yüzden Close() içinde önce SetActive(true) yapıyoruz
        routine = StartCoroutine(ScaleRoutine(targetScale, disableAtEnd));
    }

    private IEnumerator ScaleRoutine(Vector3 target, bool disableAtEnd)
    {
        Vector3 start = transform.localScale;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / animationDuration;
            transform.localScale = Vector3.Lerp(start, target, t);
            yield return null;
        }
        transform.localScale = target;
        if (disableAtEnd) gameObject.SetActive(false);
    }

    private void SetColliders(bool state)
    {
        foreach (var c in colliders) c.enabled = state;
    }
}
