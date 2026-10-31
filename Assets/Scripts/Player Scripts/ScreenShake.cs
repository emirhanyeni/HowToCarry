using System.Collections;
using UnityEngine;

/// <summary>
/// Ekran sallama. Kameranın bulunduğu objeye (Player'ın Main Camera'sı) eklenir.
/// Kamerayı sallar; istersen diyalog panelini de (Canvas Overlay kamerayla sallanmaz) sallar.
/// PlayerMovement kameranın sadece localRotation'ını değiştirir, burada sadece localPosition
/// oynandığı için çakışma olmaz.
/// </summary>
public class ScreenShake : MonoBehaviour
{
    [Header("Varsayılan Sallama")]
    [SerializeField] private float defaultDuration = 0.5f;
    [Tooltip("Kamera için metre cinsinden sallanma gücü.")]
    [SerializeField] private float defaultCameraMagnitude = 0.15f;
    [Tooltip("UI için piksel cinsinden sallanma gücü.")]
    [SerializeField] private float defaultUIMagnitude = 25f;

    [Header("Sallanacak UI (opsiyonel)")]
    [Tooltip("Tanrı diyalog panelinin RectTransform'unu buraya sürükle. Boşsa sadece kamera sallanır.")]
    [SerializeField] private RectTransform[] uiTargets;

    private Vector3 _cameraStartLocalPos;
    private Vector2[] _uiStartPos;
    private Coroutine _routine;
    private bool _isShaking;

    private void Awake()
    {
        _cameraStartLocalPos = transform.localPosition;
    }

    public void Shake()
    {
        Shake(defaultDuration, defaultCameraMagnitude, defaultUIMagnitude);
    }

    public void Shake(float duration, float cameraMagnitude, float uiMagnitude)
    {
        // Önceki sallama bitmeden yenisi gelirse önce eski konuma dön
        if (_routine != null)
        {
            StopCoroutine(_routine);
            RestorePositions();
        }
        _routine = StartCoroutine(ShakeRoutine(duration, cameraMagnitude, uiMagnitude));
    }

    private IEnumerator ShakeRoutine(float duration, float camMag, float uiMag)
    {
        // Başlangıç konumlarını sallama başlarken kaydet
        _cameraStartLocalPos = transform.localPosition;
        _uiStartPos = new Vector2[uiTargets != null ? uiTargets.Length : 0];
        for (int i = 0; i < _uiStartPos.Length; i++)
            if (uiTargets[i] != null) _uiStartPos[i] = uiTargets[i].anchoredPosition;

        _isShaking = true;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // Zamanla azalan güç (başta sert, sonda yumuşak)
            float fade = 1f - (elapsed / duration);

            Vector2 camOffset = Random.insideUnitCircle * camMag * fade;
            transform.localPosition = _cameraStartLocalPos + new Vector3(camOffset.x, camOffset.y, 0f);

            for (int i = 0; i < _uiStartPos.Length; i++)
            {
                if (uiTargets[i] == null) continue;
                uiTargets[i].anchoredPosition = _uiStartPos[i] + Random.insideUnitCircle * uiMag * fade;
            }

            // unscaledDeltaTime: Time.timeScale 0 olsa bile çalışır
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        RestorePositions();
        _routine = null;
    }

    private void RestorePositions()
    {
        if (!_isShaking) return;
        _isShaking = false;

        transform.localPosition = _cameraStartLocalPos;
        for (int i = 0; i < _uiStartPos.Length; i++)
            if (uiTargets[i] != null) uiTargets[i].anchoredPosition = _uiStartPos[i];
    }

    private void OnDisable()
    {
        // Sallanırken obje kapanırsa kamera/UI kaymış kalmasın
        RestorePositions();
        _routine = null;
    }
}
