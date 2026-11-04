using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Büdü'nün ekranda kısa süreli konuşma yazısı. CompanionAgent görevi aldığında Say() çağırır.
/// Bir Canvas içine TMP_Text koy, bu scripti boş bir objeye (örn. MenuManager değil, oyun sahnesindeki
/// Canvas'a) ekleyip Speech Text alanına o yazıyı ata.
/// </summary>
public class CompanionSpeech : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("Yazının gösterileceği TMP_Text. Başlangıçta kapalı olması gerekmez, script kapatır.")]
    [SerializeField] private TMP_Text speechText;

    [Header("Metin")]
    [SerializeField] private string defaultMessage = "<b>Büdü:</b> Hemen Çözüyorum Abi";
    [SerializeField] private float displayDuration = 2.5f;

    private Coroutine _routine;

    private void Awake()
    {
        if (speechText != null) speechText.gameObject.SetActive(false);
    }

    public void Say()
    {
        Say(defaultMessage);
    }

    public void Say(string message)
    {
        if (speechText == null) return;

        // Arka arkaya C'ye basılırsa süre baştan başlasın
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(ShowRoutine(message));
    }

    private IEnumerator ShowRoutine(string message)
    {
        speechText.text = message;
        speechText.gameObject.SetActive(true);

        // unscaled: Time.timeScale 0 olsa bile süre işlesin
        yield return new WaitForSecondsRealtime(displayDuration);

        speechText.gameObject.SetActive(false);
        _routine = null;
    }

    private void OnDisable()
    {
        if (speechText != null) speechText.gameObject.SetActive(false);
        _routine = null;
    }
}
