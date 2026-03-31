using UnityEngine;

/// <summary>
/// BGM 매니저 - 씬 전환 시에도 유지되며 4곡을 셔플 재생
/// </summary>
public class BGMManager : MonoBehaviour
{
    public static BGMManager Instance { get; private set; }

    [Header("BGM Clips")]
    [Tooltip("Assets/Audio/BGM 폴더의 곡들을 드래그")]
    public AudioClip[] bgmClips;

    [Header("Settings")]
    [Range(0f, 1f)]
    public float volume = 0.5f;

    AudioSource audioSource;
    int currentIndex = -1;
    int[] shuffledOrder;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.loop = false;
        audioSource.playOnAwake = false;
        audioSource.volume = volume;
    }

    void Start()
    {
        if (bgmClips != null && bgmClips.Length > 0)
        {
            ShuffleOrder();
            PlayNext();
        }
    }

    void Update()
    {
        if (audioSource != null && !audioSource.isPlaying && bgmClips.Length > 0
            && Application.isFocused)
        {
            PlayNext();
        }
    }

    void PlayNext()
    {
        currentIndex++;
        if (currentIndex >= shuffledOrder.Length)
        {
            ShuffleOrder();
            currentIndex = 0;
        }

        audioSource.clip = bgmClips[shuffledOrder[currentIndex]];
        audioSource.Play();
    }

    void ShuffleOrder()
    {
        shuffledOrder = new int[bgmClips.Length];
        for (int i = 0; i < shuffledOrder.Length; i++)
            shuffledOrder[i] = i;

        // Fisher-Yates shuffle
        for (int i = shuffledOrder.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (shuffledOrder[i], shuffledOrder[j]) = (shuffledOrder[j], shuffledOrder[i]);
        }
    }

    public void SetVolume(float vol)
    {
        volume = Mathf.Clamp01(vol);
        if (audioSource != null)
            audioSource.volume = volume;
    }

    public string CurrentClipName =>
        audioSource != null && audioSource.clip != null ? audioSource.clip.name : null;

    public float RemainingTime =>
        audioSource != null && audioSource.clip != null
            ? audioSource.clip.length - audioSource.time
            : 0f;

    public void Pause() => audioSource?.Pause();
    public void Resume() => audioSource?.UnPause();
    public void Skip() => PlayNext();
}
