using UnityEngine;
using TMPro;
using HowToCarry.Core;
using HowToCarry.Player;
using System.Collections;

public class MirrorEnding : MonoBehaviour, IInteractable
{
    [Header("Final Arayüzü")]
    [Tooltip("Tanrı diyalog panelini kopyalayıp buraya atabilirsin")]
    [SerializeField] private GameObject finalDialoguePanel;
    [SerializeField] private TMP_Text finalDialogueText;

    private bool isTriggered = false;

    public string GetInteractionPrompt()
    {
        if (isTriggered) return string.Empty;
        return "[E] Aynaya Bak";
    }

    public void Interact()
    {
        if (isTriggered) return;
        isTriggered = true;
        StartCoroutine(ShowFinalDialogue());
    }

    private IEnumerator ShowFinalDialogue()
    {
        // Oyuncuyu Tag (Etiket) ile bulup güvenli bir şekilde scripti alıyoruz
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null && playerObj.TryGetComponent(out PlayerMovement player))
        {
            player.LockCursor(false);
        }

        finalDialoguePanel.SetActive(true);

        // GodNPC'den gelen statik veriye göre metni belirle
        if (GodNPC.ReachedGoodEnding)
        {
            finalDialogueText.text = "<b>Edi:</b> Başardık Büdü... Eski halimize döndük.\n\n" +
                                     "<b>Büdü:</b> Sonunda! İlk işim bilgisayara oturmak!\n\n" +
                                     "<color=#00FF00>--- İYİ SON: İnsanlık Kurtuldu ---</color>";
        }
        else
        {
            finalDialogueText.text = "<b>Edi:</b> Bu kürk... çok kaşındırıyor. Başaramadık Büdü.\n\n" +
                                     "<b>Büdü:</b> En azından muzlar bedava abi...\n\n" +
                                     "<color=#FF0000>--- KÖTÜ SON: Sonsuz Evrim ---</color>";
        }

        yield return new WaitForSeconds(1.5f);

        finalDialogueText.text += "\n\n<i>[Oyundan çıkmak için SPACE tuşuna bas]</i>";

        while (!Input.GetKeyDown(KeyCode.Space))
        {
            yield return null;
        }

        Debug.Log("Oyun Bitti! (Build alınmış oyunda kapanacaktır)");
        Application.Quit();
    }
}