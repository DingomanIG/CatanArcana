using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 네트워크 디버그 로그 유틸리티
/// - 카테고리별 레이트 리밋 (같은 메시지 반복 억제)
/// - 호스트/클라이언트 구분 태그
/// - 빌드에서 끄기 쉽게 중앙 관리
/// </summary>
public static class NetLog
{
    public static bool Enabled = true;

    // 같은 키의 로그는 최소 interval(초) 간격으로만 출력
    const float DEFAULT_INTERVAL = 0.5f;
    static readonly Dictionary<string, float> lastLogTime = new();

    /// <summary>네트워크 경계 로그 (레이트 리밋 적용)</summary>
    public static void Log(string category, string message, float interval = DEFAULT_INTERVAL)
    {
        if (!Enabled) return;

        string key = $"{category}:{message}";
        float now = Time.time;

        if (lastLogTime.TryGetValue(key, out float last) && now - last < interval)
            return;

        lastLogTime[key] = now;
        Debug.Log($"[NET:{category}] {message}");
    }

    /// <summary>ServerRpc 수신 로그 (호스트에서 호출)</summary>
    public static void ServerRpc(string rpcName, int playerIndex, string detail = null)
    {
        if (!Enabled) return;
        string msg = detail != null
            ? $"P{playerIndex} → {rpcName} ({detail})"
            : $"P{playerIndex} → {rpcName}";
        Log("RPC↑", msg);
    }

    /// <summary>ClientRpc 발신 로그 (호스트에서 호출)</summary>
    public static void ClientRpc(string rpcName, string detail = null)
    {
        if (!Enabled) return;
        string msg = detail != null
            ? $"{rpcName} → ALL ({detail})"
            : $"{rpcName} → ALL";
        Log("RPC↓", msg);
    }

    /// <summary>페이즈/턴 전환 로그 (레이트 리밋 없음)</summary>
    public static void Phase(string message)
    {
        if (!Enabled) return;
        Debug.Log($"[NET:PHASE] {message}");
    }

    /// <summary>동기화 이슈 경고 (레이트 리밋 없음)</summary>
    public static void Warn(string category, string message)
    {
        if (!Enabled) return;
        Debug.LogWarning($"[NET:{category}] {message}");
    }
}
