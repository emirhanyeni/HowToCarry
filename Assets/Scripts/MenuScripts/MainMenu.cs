using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Header("Sahne")]
    [Tooltip("Build Settings'teki oyun sahnesinin adı birebir aynı yazılmalı.")]
    [SerializeField] private string gameSceneName = "HowToCarry";

    [Header("Paneller")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject controlsPanel;

    private void Start()
    {
        // Menüde fare serbest olmalı, oyundan dönüldüyse zaman da normale dönmeli
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        ShowMain();
    }

    // Oyna butonu
    public void PlayGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    // Ayarlar butonu
    public void OpenSettings()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (controlsPanel != null) controlsPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    // Kontroller butonu
    public void OpenControls()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (controlsPanel != null) controlsPanel.SetActive(true);
    }

    // Ayarlar / Kontroller panelindeki Geri butonu
    public void ShowMain()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (controlsPanel != null) controlsPanel.SetActive(false);
        if (mainPanel != null) mainPanel.SetActive(true);
    }

    // Çıkış butonu
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
