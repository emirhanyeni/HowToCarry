using UnityEngine;
using UnityEngine.Video;
using HowToCarry.Player;

public class IntroVideoController : MonoBehaviour
{
    [Header("Bileşenler")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private GameObject videoUIContainer; // RawImage (IntroVideoPanel) objesi
    [SerializeField] private PlayerMovement playerMovement;

    private bool _isVideoFinished = false;
    private bool _canSkip = false;

    private void Start()
    {
        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();

        if (playerMovement == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                playerMovement = playerObj.GetComponent<PlayerMovement>();
        }

        // Oyun arkada başlasın ama karakter hareket edemesin
        if (playerMovement != null)
        {
            playerMovement.LockCursor(false);
            Cursor.visible = false; // Video esnasında fare imleci ekranda görünmesin
        }

        // Video doğal olarak bittiğinde tetiklenecek olay
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached += OnVideoEnded;
            videoPlayer.Play();
        }

        // Sahne geçişindeki tuş basımlarının videoyu yanlışlıkla anında geçmesini önlemek için kısa gecikme
        Invoke(nameof(EnableSkip), 0.5f);
    }

    private void EnableSkip()
    {
        _canSkip = true;
    }

    private void Update()
    {
        if (_isVideoFinished || !_canSkip) return;

        // Space tuşuna basılırsa videoyu geç
        if (Input.GetKeyDown(KeyCode.Space))
        {
            SkipOrEndVideo();
        }
    }

    private void OnVideoEnded(VideoPlayer vp)
    {
        SkipOrEndVideo();
    }

    private void SkipOrEndVideo()
    {
        if (_isVideoFinished) return;
        _isVideoFinished = true;

        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoEnded;
            videoPlayer.Stop();
        }

        // Video ekranını kapat
        if (videoUIContainer != null)
            videoUIContainer.SetActive(false);
        else
            gameObject.SetActive(false);

        // Oyuncu kontrollerini aktif et ve fareyi ekrana kilitle
        if (playerMovement != null)
        {
            playerMovement.LockCursor(true);
        }
    }
}