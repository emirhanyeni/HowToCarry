using HowToCarry.Core;
using HowToCarry.Mechanics;
using HowToCarry.Player; // PlayerMovement'a erişim için eklendi
using UnityEngine;

public class AngelNPC : MonoBehaviour, IInteractable
{
    [Header("Diyalog Ayarları")]
    [TextArea(2, 5)]
    [SerializeField] private string[] dialogueLines;

    [Header("Hedef Kapı")]
    [SerializeField] private Door godRoomDoor;

    [Header("Ses Ayarları")]
    [SerializeField] private AudioClip doorOpenSound;
    [SerializeField] private AudioSource audioSource;

    private int _currentLine = 0;
    private bool _isTalking = false;
    private bool _waitingForChoice = false;

    private PlayerMovement _player; // Oyuncu referansı eklendi

    private void Start()
    {
        // Oyuncuyu "Player" etiketiyle bul ve scriptini al
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            _player = playerObj.GetComponent<PlayerMovement>();
        }
        else
        {
            Debug.LogWarning("[AngelNPC] Sahnede 'Player' tagine sahip obje bulunamadı!");
        }
    }

    public string GetInteractionPrompt()
    {
        return "[E] Konuş";
    }

    public void Interact()
    {
        if (!_isTalking)
        {
            StartDialogue();
        }
    }

    private void StartDialogue()
    {
        _isTalking = true;
        _waitingForChoice = false;
        _currentLine = 0;

        // Diyalog başlarken fareyi görünür yap, hareketi ve zıplamayı kitle
        if (_player != null) _player.LockCursor(false);

        DialogueUI.Instance.ShowPanel(true);
        DialogueUI.Instance.UpdateText(dialogueLines[_currentLine]);
    }

    private void Update()
    {
        if (!_isTalking || _waitingForChoice) return;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            AdvanceDialogue();
        }
    }

    private void AdvanceDialogue()
    {
        _currentLine++;

        if (_currentLine < dialogueLines.Length)
        {
            DialogueUI.Instance.UpdateText(dialogueLines[_currentLine]);
        }
        else
        {
            _waitingForChoice = true;
            DialogueUI.Instance.UpdateText("İçeri girmeye hazır mısın?");

            DialogueUI.Instance.ShowChoices(
                "Dünyayı kurtarmaya hazırım", AcceptDestiny,
                "Biraz etrafta dolanacağım", DeclineDestiny
            );
        }
    }

    private void AcceptDestiny()
    {
        DialogueUI.Instance.ShowPanel(false);
        _isTalking = false;

        // Seçim yapıldıktan sonra fareyi gizle ve oyuncu hareketini geri ver
        if (_player != null) _player.LockCursor(true);

        if (godRoomDoor != null)
        {
            godRoomDoor.Open();

            // Kapı açıldığında seçilen sesi çal
            if (doorOpenSound != null)
            {
                if (audioSource != null)
                {
                    audioSource.PlayOneShot(doorOpenSound);
                }
                else
                {
                    // AudioSource atanmadıysa sesi doğrudan kapının konumunda oluşturup çalar
                    AudioSource.PlayClipAtPoint(doorOpenSound, godRoomDoor.transform.position);
                }
            }
        }
        Debug.Log("[AngelNPC] Oyuncu Tanrının odasına giriyor...");
    }

    private void DeclineDestiny()
    {
        DialogueUI.Instance.ShowPanel(false);
        _isTalking = false;

        // Seçim yapıldıktan sonra fareyi gizle ve oyuncu hareketini geri ver
        if (_player != null) _player.LockCursor(true);

        Debug.Log("[AngelNPC] Oyuncu teklifi reddetti.");
    }
}