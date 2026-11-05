using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using HowToCarry.Player;

public class IntroVideoController : MonoBehaviour
{
    [Header("Bileşenler")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private GameObject videoUIContainer; // RawImage (IntroVideoPanel) objesi
    [SerializeField] private PlayerMovement playerMovement;

    private MonoBehaviour _playerAudio;
    private RawImage _rawImage;
    private bool _isVideoFinished = false;
    private bool _canSkip = false;

    private void Awake()
    {
        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();

        if (videoUIContainer == null)
            videoUIContainer = gameObject;

        _rawImage = videoUIContainer.GetComponent<RawImage>();

        // Tag'e bağımlı kalmadan sahnedeki PlayerMovement'ı doğrudan bul
        if (playerMovement == null)
            playerMovement = FindFirstObjectByType<PlayerMovement>();

        // Sahnedeki tüm scriptler içinde PlayerAudio'yu bul (alt objede olsa bile yakalar)
        foreach (var comp in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (comp.GetType().Name == "PlayerAudio")
            {
                _playerAudio = comp;
                break;
            }
        }
    }

    private void Start()
    {
        SetPlayerState(false);

        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached += OnVideoEnded;
            videoPlayer.Play();
        }

        Invoke(nameof(EnableSkip), 0.5f);
    }

    private void EnableSkip()
    {
        _canSkip = true;
    }

    private void Update()
    {
        // Video bitene kadar başka hiçbir scriptin kontrolleri yanlışlıkla açmasına izin verme
        if (!_isVideoFinished)
        {
            if (playerMovement != null && playerMovement.enabled)
                playerMovement.enabled = false;

            if (_playerAudio != null && _playerAudio.enabled)
                _playerAudio.enabled = false;
        }

        if (_isVideoFinished || !_canSkip) return;

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

        // Görüntüyü ve varsa alt yazıları anında gizle (ama Coroutine ölmesin diye objeyi henüz kapatma)
        if (_rawImage != null)
            _rawImage.enabled = false;

        foreach (Transform child in videoUIContainer.transform)
        {
            child.gameObject.SetActive(false);
        }

        StartCoroutine(EnablePlayerControlsAfterKeyRelease());
    }

    private IEnumerator EnablePlayerControlsAfterKeyRelease()
    {
        // Oyuncu elini Space tuşundan tamamen çekene kadar bekle
        while (Input.GetKey(KeyCode.Space))
        {
            yield return null;
        }

        // Tuş bırakıldıktan sonra güvenlik için 1 kare daha bekle
        yield return null;

        SetPlayerState(true);

        // En son video panelini tamamen kapat
        if (videoUIContainer != null)
            videoUIContainer.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    private void SetPlayerState(bool active)
    {
        if (playerMovement != null)
        {
            playerMovement.LockCursor(active);
            playerMovement.enabled = active;
        }

        if (_playerAudio != null)
        {
            _playerAudio.enabled = active;
        }

        if (!active)
        {
            Cursor.visible = false;
        }
    }
}