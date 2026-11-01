using UnityEngine;
using UnityEngine.SceneManagement;
using HowToCarry.Player;

/// <summary>
/// ESC menüsü: Oyuna Dön, Kontroller, Çıkış.
/// Oyun sahnesindeki Canvas'ta boş bir objeye eklenir.
/// Butonların On Click olaylarına Resume / OpenControls / CloseControls / ExitGame bağlanır.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [Header("Paneller")]
    [Tooltip("Oyuna Dön / Kontroller / Çıkış butonlarının olduğu panel (arka karartma dahil).")]
    [SerializeField] private GameObject pausePanel;
    [Tooltip("Ana menüdeki Kontroller panelinin oyun sahnesine kopyalanmış hali.")]
    [SerializeField] private GameObject controlsPanel;

    [Header("Çıkış")]
    [Tooltip("Açıksa Çıkış butonu ana menü sahnesine döner. Kapalıysa oyunu kapatır.")]
    [SerializeField] private bool exitToMainMenu = true;
    [Tooltip("Build Settings'teki ana menü sahnesinin adı birebir aynı olmalı.")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private PlayerMovement _player;
    private bool _isPaused;
    private bool _inControls;

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) _player = playerObj.GetComponent<PlayerMovement>();

        if (pausePanel != null) pausePanel.SetActive(false);
        if (controlsPanel != null) controlsPanel.SetActive(false);
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        if (_isPaused)
        {
            // Kontroller açıksa önce pause menüsüne dön, değilse oyuna dön
            if (_inControls) CloseControls();
            else Resume();
        }
        else
        {
            // Diyalog, Tanrı konuşması ya da final sırasında oyuncu kilitlidir; o zaman açma
            if (_player != null && !_player.CanMove) return;
            Pause();
        }
    }

    private void Pause()
    {
        _isPaused = true;
        _inControls = false;

        Time.timeScale = 0f;
        if (_player != null) _player.LockCursor(false); // fareyi serbest bırak, hareketi kilitle

        if (pausePanel != null) pausePanel.SetActive(true);
        if (controlsPanel != null) controlsPanel.SetActive(false);
    }

    // Oyuna Dön butonu
    public void Resume()
    {
        _isPaused = false;
        _inControls = false;

        if (pausePanel != null) pausePanel.SetActive(false);
        if (controlsPanel != null) controlsPanel.SetActive(false);

        Time.timeScale = 1f;
        if (_player != null) _player.LockCursor(true);
    }

    // Kontroller butonu
    public void OpenControls()
    {
        _inControls = true;
        if (pausePanel != null) pausePanel.SetActive(false);
        if (controlsPanel != null) controlsPanel.SetActive(true);
    }

    // Kontroller panelindeki Geri butonu
    public void CloseControls()
    {
        _inControls = false;
        if (controlsPanel != null) controlsPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(true);
    }

    // Çıkış butonu
    public void ExitGame()
    {
        Time.timeScale = 1f;

        if (exitToMainMenu)
        {
            // Statik değişken sahne değişince sıfırlanmaz, eski son kalmasın
            GodNPC.ReachedGoodEnding = false;
            SceneManager.LoadScene(mainMenuSceneName);
            return;
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnDestroy()
    {
        // Sahne pause halindeyken değişirse zaman donuk kalmasın
        Time.timeScale = 1f;
    }
}
