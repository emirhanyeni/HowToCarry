using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

public class DialogueUI : MonoBehaviour
{
    // Singleton deseni: Diğer scriptlerden (Melek'ten) kolayca erişebilmek için
    public static DialogueUI Instance { get; private set; }

    [Header("UI Elemanları")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private Button choice1Button;
    [SerializeField] private Button choice2Button;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        dialoguePanel.SetActive(false); // Oyun başlarken panel kapalı olsun
        HideChoices();
    }

    public void ShowPanel(bool show)
    {
        dialoguePanel.SetActive(show);
        if (!show) HideChoices();
    }

    public void UpdateText(string text)
    {
        dialogueText.text = text;
        HideChoices(); // Düz metin akarken butonları gizle
    }

    // Seçenekleri ve tıklanınca ne olacağını (Action) belirleyen metod
    public void ShowChoices(string c1Text, Action onC1Clicked, string c2Text, Action onC2Clicked)
    {
        choice1Button.gameObject.SetActive(true);
        choice2Button.gameObject.SetActive(true);

        choice1Button.GetComponentInChildren<TextMeshProUGUI>().text = c1Text;
        choice2Button.GetComponentInChildren<TextMeshProUGUI>().text = c2Text;

        // Butonlara önceki görevleri unuttur ve yenilerini ekle
        choice1Button.onClick.RemoveAllListeners();
        choice1Button.onClick.AddListener(() => onC1Clicked());

        choice2Button.onClick.RemoveAllListeners();
        choice2Button.onClick.AddListener(() => onC2Clicked());
    }

    private void HideChoices()
    {
        choice1Button.gameObject.SetActive(false);
        choice2Button.gameObject.SetActive(false);
    }
}