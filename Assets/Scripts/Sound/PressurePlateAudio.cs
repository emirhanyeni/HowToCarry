using UnityEngine;

/// <summary>
/// Basınç plakası ve kapı sesleri.
/// Door'un durumunu kendisi izler, UnityEvent bağlamana GEREK YOK.
/// (İstersen OnPressed/OnReleased eventlerine de bağlayabilirsin; çift çalmaz.)
/// Bu scripti plakaya ya da boş bir objeye koy. Kapıya koyarsan da çalışır.
/// Ses gelmezse Console'daki [PressurePlateAudio] mesajlarına bak.
/// </summary>
public class PressurePlateAudio : MonoBehaviour
{
    [Header("Bağlantılar")]
    [Tooltip("ZORUNLU: sesini istediğin kapı (sahneden sürükle).")]
    [SerializeField] private Door door;
    [Tooltip("Plaka sesinin çıkacağı yer. Boşsa bu objenin konumu kullanılır.")]
    [SerializeField] private Transform plate;

    [Header("Plaka Sesleri")]
    [SerializeField] private AudioClip plateDownClip;
    [SerializeField] private AudioClip plateUpClip;
    [Range(0f, 1f)][SerializeField] private float plateVolume = 0.8f;

    [Header("Kapı Sesleri")]
    [SerializeField] private AudioClip doorOpenClip;
    [SerializeField] private AudioClip doorCloseClip;
    [Range(0f, 1f)][SerializeField] private float doorVolume = 1f;
    [Tooltip("0 = kapı ne kadar uzakta olursa olsun aynı seviyede duyulur (2D). 1 = uzaklaştıkça kısılır (3D, 30 metrede tamamen biter).")]
    [Range(0f, 1f)][SerializeField] private float doorSpatialBlend = 0.3f;
    [Tooltip("Kapıya bu mesafeye kadar yaklaşınca ses tam seviyede duyulur (metre).")]
    [SerializeField] private float doorMinDistance = 20f;
    [Tooltip("Kapıdan bu kadar uzakta ses tamamen biter (metre). Arası yavaş yavaş kısılır.")]
    [SerializeField] private float doorMaxDistance = 200f;

    [Header("Otomatik Algılama")]
    [Tooltip("Açıkken kapının açılıp kapandığını kendisi fark eder; event bağlamaya gerek kalmaz.")]
    [SerializeField] private bool autoDetectFromDoor = true;

    [Header("Hata Ayıklama")]
    [SerializeField] private bool logMessages = true;

    private Collider[] doorColliders;
    private bool wasOpen;
    private float lastOpenTime = -10f;
    private float lastCloseTime = -10f;
    private const float DuplicateWindow = 0.25f;

    private void Start()
    {
        if (door == null)
        {
            Debug.LogWarning("[PressurePlateAudio] 'Door' alanı boş! Kapıyı Inspector'dan sürükle.", this);
            return;
        }

        doorColliders = door.GetComponentsInChildren<Collider>(true);
        wasOpen = IsDoorOpen();

        Log($"Hazır. Kapı: '{door.name}', collider sayısı: {doorColliders.Length}, " +
            $"otomatik algılama: {autoDetectFromDoor}.");

        if (FindAnyObjectByType<AudioListener>() == null)
            Debug.LogWarning("[PressurePlateAudio] Sahnede AudioListener YOK, hiçbir ses duyulmaz! " +
                             "Kameraya Audio Listener ekle.", this);
        if (AudioListener.pause || AudioListener.volume <= 0f)
            Debug.LogWarning("[PressurePlateAudio] AudioListener duraklatılmış ya da ses seviyesi 0.", this);

        Check(doorOpenClip, "Door Open Clip");
        Check(doorCloseClip, "Door Close Clip");
        Check(plateDownClip, "Plate Down Clip");
        Check(plateUpClip, "Plate Up Clip");
    }

    private void Update()
    {
        if (!autoDetectFromDoor || door == null) return;

        bool isOpen = IsDoorOpen();
        if (isOpen == wasOpen) return;
        wasOpen = isOpen;

        if (isOpen) PlayOpen();
        else PlayClose();
    }

    // Door.Open() collider'ları hemen kapatır, Door.Close() hemen açar.
    // Collider yoksa kapının aktif olup olmadığına bakılır.
    private bool IsDoorOpen()
    {
        if (doorColliders != null && doorColliders.Length > 0)
            return !doorColliders[0].enabled;
        return !door.gameObject.activeSelf;
    }

    // İstersen PressurePlate > On Pressed() listesine bağla (zorunlu değil)
    public void OnPressed() { PlayOpen(); }

    // İstersen PressurePlate > On Released() listesine bağla (zorunlu değil)
    public void OnReleased() { PlayClose(); }

    private void PlayOpen()
    {
        if (Time.time - lastOpenTime < DuplicateWindow) return;
        lastOpenTime = Time.time;

        Log("Kapı AÇILDI, sesler çalınıyor.");
        SoundManager.Instance.Play(plateDownClip, PlatePosition(), plateVolume);
        SoundManager.Instance.Play(doorOpenClip, DoorPosition(), doorVolume, 0.03f, doorSpatialBlend,
                                   doorMinDistance, doorMaxDistance);
    }

    private void PlayClose()
    {
        if (Time.time - lastCloseTime < DuplicateWindow) return;
        lastCloseTime = Time.time;

        Log("Kapı KAPANDI, sesler çalınıyor.");
        SoundManager.Instance.Play(plateUpClip, PlatePosition(), plateVolume);
        SoundManager.Instance.Play(doorCloseClip, DoorPosition(), doorVolume, 0.03f, doorSpatialBlend,
                                   doorMinDistance, doorMaxDistance);
    }

    private Vector3 PlatePosition()
    {
        return plate != null ? plate.position : transform.position;
    }

    private Vector3 DoorPosition()
    {
        return door != null ? door.transform.position : transform.position;
    }

    private void Check(AudioClip clip, string label)
    {
        if (clip == null)
            Debug.LogWarning($"[PressurePlateAudio] {label} atanmamış, bu ses çalmayacak.", this);
    }

    private void Log(string message)
    {
        if (logMessages) Debug.Log("[PressurePlateAudio] " + message, this);
    }
}