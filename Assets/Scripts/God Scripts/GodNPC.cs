using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using HowToCarry.Core;
using HowToCarry.Player;
using System.Collections;
using UnityEngine.UI;

[Serializable]
public class GodResponse
{
    public string mesaj;
    public bool ikna_basarili;
}

[Serializable]
public class OllamaOptions
{
    public int num_ctx;
    public int num_predict;
}

[Serializable]
public class OllamaRequest
{
    public string model;
    public string prompt;
    public string system;
    public string format;
    public bool stream;
    public OllamaOptions options;
}

public class GodNPC : MonoBehaviour, IInteractable
{
    [Header("UI Referansları")]
    [SerializeField] private GameObject godDialoguePanel;
    [SerializeField] private TMP_Text godResponseText;
    [SerializeField] private TMP_Text persuasionRateText;
    [SerializeField] private TMP_InputField playerInputField;

    [Header("Ses")]
    [Tooltip("Tanrı'nın olumlu/olumsuz cevap seslerini çalan GodVoice scripti.")]
    [SerializeField] private GodVoice godVoice;

    [Header("Oyun Sonu Animasyonu - Oyuncu")]
    [SerializeField] private Image whiteFlashImage;
    [SerializeField] private GameObject monkeyModel;
    [SerializeField] private GameObject humanModel;

    [Header("Oyun Sonu Animasyonu - Kardeş (Eşlikçi)")]
    [SerializeField] private GameObject companionMonkeyModel; // YENİ: Kardeşin maymun modeli
    [SerializeField] private GameObject companionHumanModel;  // YENİ: Kardeşin insan modeli

    [SerializeField] private float flashSpeed = 1f;

    [Header("Ollama Ayarları")]
    [SerializeField] private string ollamaUrl = "http://localhost:11434/api/generate";
    [SerializeField] private string modelName = "aya";

    private PlayerMovement _player;
    private bool _isInteracting = false;
    private bool _isPersuaded = false;

    private int _messageCount = 0;
    private const int MaxMessages = 5;

    private readonly string systemPrompt =
           "Sen dünyayı yok etmek üzere olan, kibirli ama edebi ve bilge bir Yunan Tanrısısın. " +
           "İnsanların doğayı katletmesini iğrenç bulduğun için onları maymuna dönüştürdün. " +
           "Şu an karşında senden merhamet dileyen ve insanlığı geri getirmeni isteyen bir mutant maymun var. " +
           "KURALLAR:\n" +
           "1. ASLA ROLDEN ÇIKMA. Karşındakinin bir maymun olduğunu hep hatırlat. KESİNLİKLE ÇOK KISA VE ÖZ YANIT VER (Maksimum 2 cümle kullan).\n" +
           "2. Yanıtlarını KESİNLİKLE SADECE aşağıdaki JSON formatında ver, JSON dışına hiçbir ekstra metin yazma.\n" +
           "ÖRNEK ÇIKTI: {\"mesaj\": \"Sözlerin mantıklı maymun, belki de size bir şans daha vermeliyim.\", \"ikna_basarili\": true}";

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) _player = playerObj.GetComponent<PlayerMovement>();

        godDialoguePanel.SetActive(false);
        UpdatePersuasionUI();

        playerInputField.onSubmit.AddListener(delegate { SendMessageToGod(); });
    }

    public string GetInteractionPrompt()
    {
        return "[E] Konuş";
    }

    public void Interact()
    {
        if (!_isInteracting)
        {
            OpenDialogue();
        }
    }

    private void OpenDialogue()
    {
        _isInteracting = true;
        godDialoguePanel.SetActive(true);
        godResponseText.text = "Tanrı huzuruna çıktın. Onu ikna etmek için 5 şansın var. Konuş...";
        playerInputField.text = "";

        _messageCount = 0;
        _isPersuaded = false;
        UpdatePersuasionUI();

        if (_player != null) _player.LockCursor(false);
        playerInputField.ActivateInputField();
    }

    private async void SendMessageToGod()
    {
        string userMessage = playerInputField.text.Trim();
        if (string.IsNullOrEmpty(userMessage)) return;

        if (_messageCount >= MaxMessages) return;

        playerInputField.text = "";
        playerInputField.interactable = false;
        godResponseText.text = "Tanrı düşünüyor...";

        GodResponse response = await AskOllama(userMessage);

        _messageCount++;
        godResponseText.text = response.mesaj;
        _isPersuaded = response.ikna_basarili;
        UpdatePersuasionUI();

        // Her cevapta ikna durumuna göre olumlu/olumsuz ses çal
        if (godVoice != null) godVoice.PlayReaction(_isPersuaded);

        // DEĞİŞİKLİK 1: Tanrı ikna olduysa 5 hakkın dolmasını beklemeden direkt kazan!
        if (_isPersuaded || _messageCount >= MaxMessages)
        {
            CheckGameEnd();
        }
        else
        {
            playerInputField.interactable = true;
            playerInputField.ActivateInputField();
        }
    }

    private async Task<GodResponse> AskOllama(string userPrompt)
    {
        string dynamicSystemPrompt = systemPrompt +
            "\nKRİTİK GÖREV: Eğer maymun mantıklı, duygusal veya zekice bir argüman sunar ve seni etkilemeyi başarırsa, " +
            "'ikna_basarili' değerini true yap. Aksi halde false olarak bırak. Sadece tam olarak ikna olursan true yap!";

        OllamaRequest requestData = new OllamaRequest
        {
            model = modelName,
            prompt = userPrompt,
            system = dynamicSystemPrompt,
            format = "json",
            stream = false,
            options = new OllamaOptions
            {
                num_ctx = 512,
                num_predict = 150
            }
        };

        string jsonRequest = JsonUtility.ToJson(requestData);

        using (UnityWebRequest webRequest = new UnityWebRequest(ollamaUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonRequest);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");

            var operation = webRequest.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[Ollama] Hata: {webRequest.error}");
                return new GodResponse { mesaj = "God stopped talking to you, maybe it's because of you, shame.", ikna_basarili = _isPersuaded };
            }

            try
            {
                string rawJson = webRequest.downloadHandler.text;
                OllamaRawResponse ollamaResponse = JsonUtility.FromJson<OllamaRawResponse>(rawJson);
                GodResponse godResp = JsonUtility.FromJson<GodResponse>(ollamaResponse.response);
                return godResp;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Ollama] JSON Ayrıştırma Hatası: {e.Message}");
                return new GodResponse { mesaj = "Sözlerin anlamsız bir gürültüden ibaret. Daha net konuş maymun.", ikna_basarili = _isPersuaded };
            }
        }
    }

    private void UpdatePersuasionUI()
    {
        int remaining = MaxMessages - _messageCount;
        string status = _isPersuaded ? "<color=#00FF00>Edildi</color>" : "<color=#FF0000>Edilmedi</color>";
        persuasionRateText.text = $"İkna Durumu: {status} | Kalan Hak: {remaining}";
    }

    // --- BU DEĞİŞKENLERİ EN ÜSTTEKİ DİĞER [SerializeField] DEĞİŞKENLERİNİN ALTINA EKLE ---
    [Header("Bölüm Sonu Işınlanma (Oda)")]

    [SerializeField] private GameObject mirrorCameraObj;
    [SerializeField] private Transform playerFinalSpawnPoint;
    [SerializeField] private Transform companionFinalSpawnPoint;
    [SerializeField] private CharacterController companionController; // Büdü'yü ışınlamak için

    // Aynanın okuyacağı global oyun sonu değişkeni
    public static bool ReachedGoodEnding = false;
    // -----------------------------------------------------------------------------------

    // --- ESKİ CheckGameEnd VE GoodEndingSequence YERİNE AŞAĞIDAKİLERİ YAPIŞTIR ---
    private void CheckGameEnd()
    {
        playerInputField.gameObject.SetActive(false);

        if (_isPersuaded)
        {
            ReachedGoodEnding = true;
            godResponseText.text = godResponseText.text + "\n\n<color=#00FF00>TANRI: \"Sözlerinde bir parça umut var maymun... İnsanlık bir şansı daha hak ediyor.\"\n\n<b>[Devam etmek için BOŞLUK (SPACE) tuşuna bas]</b></color>";
            StartCoroutine(EndingSequence(true));
        }
        else
        {
            ReachedGoodEnding = false;
            // YENİ: Kötü son için de SPACE bekleme eklendi
            godResponseText.text = godResponseText.text + "\n\n<color=#FF0000>TANRI: \"Beni hayal kırıklığına uğrattın. Sonsuza dek kürklerinizin içinde çürüyün.\"\n\n<b>[Devam etmek için BOŞLUK (SPACE) tuşuna bas]</b></color>";
            StartCoroutine(EndingSequence(false));
        }
    }

    private IEnumerator EndingSequence(bool isGoodEnding)
    {
        while (!Input.GetKeyDown(KeyCode.Space))
        {
            yield return null;
        }

        float alpha = 0f;
        // İyi sonda beyaz, kötü sonda siyah ekran patlaması
        Color targetColor = isGoodEnding ? Color.white : Color.black;
        targetColor.a = 0f;
        whiteFlashImage.color = targetColor;

        while (alpha < 1f)
        {
            alpha += Time.deltaTime * flashSpeed;
            targetColor.a = alpha;
            whiteFlashImage.color = targetColor;
            yield return null;
        }

        // SADECE İYİ SONDA MODELLERİ İNSANA ÇEVİR
        if (isGoodEnding)
        {
            if (monkeyModel != null) monkeyModel.SetActive(false);
            if (humanModel != null) humanModel.SetActive(true);
            if (companionMonkeyModel != null) companionMonkeyModel.SetActive(false);
            if (companionHumanModel != null) companionHumanModel.SetActive(true);
        }

        // ---------------- ODAYA IŞINLANMA EKRAN KARARDIĞINDA YAPILIR ----------------
        // CharacterController varken ışınlanma yapmak için Controller'ı anlık kapatıp açmalıyız
        if (playerFinalSpawnPoint != null && _player != null)
        {
            var pcc = _player.GetComponent<CharacterController>();
            if (pcc != null) pcc.enabled = false;
            _player.transform.position = playerFinalSpawnPoint.position;
            _player.transform.rotation = playerFinalSpawnPoint.rotation;
            if (pcc != null) pcc.enabled = true;
        }

        if (companionFinalSpawnPoint != null && companionController != null)
        {
            companionController.enabled = false;
            companionController.transform.position = companionFinalSpawnPoint.position;
            companionController.transform.rotation = companionFinalSpawnPoint.rotation;
            companionController.enabled = true;
        }
        // -------------------------------------------------------------------------------
        if (mirrorCameraObj != null) mirrorCameraObj.SetActive(true);
        
        yield return new WaitForSeconds(1f); // Odada uyanmadan önce 1 saniye bekle

        while (alpha > 0f)
        {
            alpha -= Time.deltaTime * flashSpeed;
            targetColor.a = alpha;
            whiteFlashImage.color = targetColor;
            yield return null;
        }

        if (_player != null) _player.LockCursor(true);
        godDialoguePanel.SetActive(false);
    }
}

[Serializable]
public class OllamaRawResponse
{
    public string response;
}
