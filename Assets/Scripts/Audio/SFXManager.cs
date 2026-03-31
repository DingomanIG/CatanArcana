using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SFX 매니저 - 씬 전환 시에도 유지, AudioSource 풀로 동시 재생 지원
/// 사용법: SFXManager.Instance.Play(SFXType.ButtonClick);
/// </summary>
public class SFXManager : MonoBehaviour
{
    public static SFXManager Instance { get; private set; }

    [Serializable]
    public struct SFXEntry
    {
        public SFXType type;
        public AudioClip clip;
    }

    [Header("SFX Clips")]
    public SFXEntry[] entries;

    [Header("Settings")]
    [Range(0f, 1f)]
    public float volume = 1f;

    [Tooltip("동시 재생 가능한 AudioSource 수")]
    public int poolSize = 8;

    Dictionary<SFXType, AudioClip> clipMap;
    AudioSource[] pool;
    int poolIndex;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildClipMap();
        BuildPool();
    }

    void BuildClipMap()
    {
        clipMap = new Dictionary<SFXType, AudioClip>(entries.Length);
        foreach (var e in entries)
        {
            if (e.clip != null)
                clipMap[e.type] = e.clip;
        }
    }

    void BuildPool()
    {
        pool = new AudioSource[poolSize];
        for (int i = 0; i < poolSize; i++)
        {
            pool[i] = gameObject.AddComponent<AudioSource>();
            pool[i].playOnAwake = false;
            pool[i].volume = volume;
        }
    }

    // ── Public API ──────────────────────────────────────────

    public void Play(SFXType type)
    {
        if (!clipMap.TryGetValue(type, out AudioClip clip)) return;

        AudioSource src = GetFreeSource();
        src.clip = clip;
        src.volume = volume;
        src.Play();
    }

    public void Play(SFXType type, float volumeOverride)
    {
        if (!clipMap.TryGetValue(type, out AudioClip clip)) return;

        AudioSource src = GetFreeSource();
        src.clip = clip;
        src.volume = Mathf.Clamp01(volumeOverride);
        src.Play();
    }

    public void SetVolume(float vol)
    {
        volume = Mathf.Clamp01(vol);
        foreach (var src in pool)
            src.volume = volume;
    }

    // ── Internal ─────────────────────────────────────────────

    AudioSource GetFreeSource()
    {
        // 재생 중이 아닌 소스 우선 탐색
        for (int i = 0; i < pool.Length; i++)
        {
            if (!pool[i].isPlaying)
                return pool[i];
        }

        // 모두 사용 중이면 라운드로빈
        AudioSource src = pool[poolIndex];
        poolIndex = (poolIndex + 1) % pool.Length;
        return src;
    }
}
