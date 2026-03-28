using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 네트워크 테스트용 임시 UI
/// 호스트/참가/주사위/턴종료 버튼
/// </summary>
public class NetworkTestUI : MonoBehaviour
{
    string joinCodeInput = "";
    string playerName = "Player";
    string statusMessage = "대기 중...";
    Vector2 scrollPos;

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 320, 500));
        GUILayout.Label("=== ArcanaCatan Network Test ===");
        GUILayout.Space(5);

        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            DrawConnectionUI();
        }
        else
        {
            DrawGameUI();
        }

        GUILayout.Space(10);
        GUILayout.Label($"Status: {statusMessage}");
        GUILayout.EndArea();
    }

    void DrawConnectionUI()
    {
        GUILayout.Label("이름:");
        playerName = GUILayout.TextField(playerName, 20);
        GUILayout.Space(5);

        if (GUILayout.Button("방 만들기 (Host)", GUILayout.Height(40)))
        {
            CreateRoom();
        }

        GUILayout.Space(10);
        GUILayout.Label("참가 코드:");
        joinCodeInput = GUILayout.TextField(joinCodeInput, 6);

        if (GUILayout.Button("참가하기 (Client)", GUILayout.Height(40)))
        {
            JoinRoom();
        }
    }

    void DrawGameUI()
    {
        // 접속 정보
        string role = NetworkManager.Singleton.IsHost ? "Host" : "Client";
        GUILayout.Label($"역할: {role} | ID: {NetworkManager.Singleton.LocalClientId}");
        GUILayout.Label($"접속자: {NetworkManager.Singleton.ConnectedClientsIds.Count}명");

        if (GameNetworkManager.Instance.JoinCode != null)
        {
            GUILayout.Label($"Join Code: {GameNetworkManager.Instance.JoinCode}");
        }

        GUILayout.Space(10);

        // 턴 정보
        if (TurnManager.Instance != null)
        {
            var phase = TurnManager.Instance.CurrentPhase.Value;
            GUILayout.Label($"턴: {TurnManager.Instance.TurnNumber.Value} | 페이즈: {phase}");
            GUILayout.Label($"현재 플레이어: {TurnManager.Instance.CurrentTurnPlayerId.Value}");

            bool isMyTurn = TurnManager.Instance.IsMyTurn();
            GUILayout.Label(isMyTurn ? ">> 내 턴! <<" : "상대 턴...");

            if (TurnManager.Instance.DiceResult.Value > 0)
            {
                GUILayout.Label($"주사위: {TurnManager.Instance.DiceResult.Value}");
            }

            GUILayout.Space(5);

            if (phase == GamePhase.WaitingForPlayers && NetworkManager.Singleton.IsHost)
            {
                if (GUILayout.Button("게임 시작", GUILayout.Height(35)))
                {
                    TurnManager.Instance.StartGame();
                    statusMessage = "게임 시작!";
                }
            }

            if (isMyTurn && phase == GamePhase.RollDice)
            {
                if (GUILayout.Button("주사위 굴리기", GUILayout.Height(35)))
                {
                    TurnManager.Instance.RollDiceServerRpc();
                }
            }

            if (isMyTurn && phase == GamePhase.Action)
            {
                if (GUILayout.Button("턴 종료", GUILayout.Height(35)))
                {
                    TurnManager.Instance.EndTurnServerRpc();
                }
            }
        }

        GUILayout.Space(10);
        if (GUILayout.Button("연결 해제", GUILayout.Height(30)))
        {
            LobbyManager.Instance.LeaveLobby();
            statusMessage = "연결 해제됨";
        }
    }

    async void CreateRoom()
    {
        statusMessage = "방 생성 중...";
        var lobby = await LobbyManager.Instance.CreateLobby(playerName);
        if (lobby != null)
        {
            statusMessage = $"방 생성 완료! Code: {lobby.LobbyCode}";
        }
        else
        {
            statusMessage = "방 생성 실패";
        }
    }

    async void JoinRoom()
    {
        if (string.IsNullOrEmpty(joinCodeInput))
        {
            statusMessage = "참가 코드를 입력하세요";
            return;
        }

        statusMessage = "접속 중...";
        bool success = await LobbyManager.Instance.JoinLobbyByCode(joinCodeInput, playerName);
        statusMessage = success ? "접속 성공!" : "접속 실패";
    }
}
