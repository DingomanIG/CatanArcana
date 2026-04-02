# 온라인 멀티플레이 설계서

> Phase 1_3: P2P Relay 기반 온라인 대전. 로컬 로직 재활용 (프록시 패턴).

---

## 1. 아키텍처 다이어그램

```
┌─────────────────────────────────────────────────────────────┐
│                        UI Layer                             │
│  GameHUDController / BuildModeController / TileFlashEffect  │
│              (IGameManager만 참조 - 변경 없음)                │
└──────────────────────┬──────────────────────────────────────┘
                       │ GameServices.GameManager
                       ▼
              ┌─────────────────┐
              │  IGameManager   │  (인터페이스)
              └────┬───────┬────┘
                   │       │
        ┌──────────┘       └──────────┐
        ▼                             ▼
┌───────────────┐          ┌────────────────────────┐
│LocalGameManager│         │ NetworkGameManager      │
│ (MonoBehaviour)│         │ (NetworkBehaviour)      │
│  로컬 전용     │         │  IGameManager 구현       │
└───────────────┘         │                          │
                          │  [호스트 측]              │
                          │  ┌──────────────────┐    │
                          │  │ LocalGameManager  │    │
                          │  │ (내부 인스턴스)     │    │
                          │  └──────────────────┘    │
                          │                          │
                          │  [클라이언트 측]           │
                          │  → ServerRpc 요청         │
                          │  ← ClientRpc 결과 수신     │
                          │  ← NetworkVariable 동기화  │
                          └────────────────────────┘
                                    │
                   ┌────────────────┼────────────────┐
                   ▼                ▼                ▼
           ┌─────────────┐ ┌─────────────┐ ┌──────────────┐
           │ TurnManager  │ │GameNetwork  │ │LobbyManager  │
           │(→ NGM에 흡수)│ │Manager      │ │(Relay+Lobby) │
           └─────────────┘ │(Relay 연결) │ │              │
                           └─────────────┘ └──────────────┘
```

### 핵심 원칙

- **호스트**: 내부에 LocalGameManager 인스턴스를 보유하고 실제 게임 로직 실행. 결과를 NetworkVariable과 ClientRpc로 브로드캐스트.
- **클라이언트**: 모든 액션을 ServerRpc로 호스트에 요청. 결과는 NetworkVariable 변경 콜백과 ClientRpc로 수신.
- **UI 무변경**: 기존 UI 코드는 IGameManager만 참조하므로 변경 불필요.

---

## 2. NetworkGameManager 클래스 설계

### 2.1 클래스 구조

```csharp
public class NetworkGameManager : NetworkBehaviour, IGameManager
```

### 2.2 주요 필드

```
[호스트 전용]
- LocalGameManager hostGameManager       // 실제 게임 로직 (호스트에서만 non-null)

[모든 클라이언트 공통]
- int localPlayerIndex                   // 이 클라이언트의 플레이어 인덱스
- ulong[] playerClientIds                // clientId → playerIndex 매핑

[NetworkVariable - 게임 핵심 상태]
- NetworkVariable<int> netTurnNumber
- NetworkVariable<int> netCurrentPlayerIndex
- NetworkVariable<int> netFirstPlayerIndex
- NetworkVariable<GamePhase> netCurrentPhase
- NetworkVariable<BuildMode> netCurrentBuildMode
- NetworkVariable<DevCardUseState> netDevCardState
- NetworkVariable<int> netDevCardDeckRemaining
- NetworkVariable<bool> netIsWaitingForDiscard
- NetworkVariable<int> netLongestRoadHolder
- NetworkVariable<int> netLargestArmyHolder
- NetworkVariable<HexCoordSerialized> netRobberPosition

[NetworkVariable - 플레이어별]
- NetworkList<int> netPlayerVP               // 플레이어별 승점
- NetworkList<int> netPlayerTotalResCount     // 플레이어별 총 자원 수 (공개)
- NetworkList<int> netPlayerKnightsPlayed     // 플레이어별 기사 수
```

### 2.3 자원 정보 은닉

카탄 규칙상 다른 플레이어의 정확한 자원 구성은 비공개:
- **자기 자원**: TargetedClientRpc로 5종 전부 상세 수신
- **타인 자원**: 총 자원 수만 NetworkVariable로 동기화

---

## 3. RPC 목록

### 3.1 ServerRpc (클라이언트 → 호스트)

| RPC | 파라미터 | 설명 |
|-----|----------|------|
| `RequestRollDiceServerRpc` | (ServerRpcParams) | 주사위 굴리기 |
| `RequestEndTurnServerRpc` | (ServerRpcParams) | 턴 종료 |
| `RequestBuildSettlementServerRpc` | (int vertexId) | 마을 건설 |
| `RequestBuildCityServerRpc` | (int vertexId) | 도시 건설 |
| `RequestBuildRoadServerRpc` | (int edgeId) | 도로 건설 |
| `RequestEnterBuildModeServerRpc` | (BuildMode mode) | 건설 모드 진입 |
| `RequestCancelBuildModeServerRpc` | () | 건설 모드 취소 |
| `RequestBuyDevCardServerRpc` | () | 발전카드 구매 |
| `RequestUseKnightServerRpc` | (HexCoordSerialized) | 기사 카드 |
| `RequestUseRoadBuildingServerRpc` | () | 도로건설 카드 |
| `RequestUseYearOfPlentyServerRpc` | (ResourceType r1, r2) | 풍년 카드 |
| `RequestUseMonopolyServerRpc` | (ResourceType) | 독점 카드 |
| `RequestBankTradeServerRpc` | (ResourceType give, recv) | 은행 거래 |
| `RequestPlayerTradeServerRpc` | (int other, ResArray offer, request) | 플레이어 거래 |
| `RequestRespondTradeServerRpc` | (bool accept) | 거래 응답 |
| `RequestMoveRobberServerRpc` | (HexCoordSerialized) | 도적 이동 |
| `RequestStealFromPlayerServerRpc` | (int victimIndex) | 약탈 대상 선택 |
| `RequestConfirmDiscardServerRpc` | (ResArray toDiscard) | 디스카드 확인 |

### 3.2 ClientRpc (호스트 → 클라이언트)

| RPC | 파라미터 | 설명 |
|-----|----------|------|
| `NotifyDiceRolledClientRpc` | (int die1, die2, total) | 주사위 결과 |
| `NotifyBuildingPlacedClientRpc` | (int player, vertexId, BuildingType) | 건물 배치 |
| `NotifyRoadPlacedClientRpc` | (int player, edgeId) | 도로 배치 |
| `NotifyRobberMovedClientRpc` | (HexCoordSerialized) | 도적 이동 |
| `NotifyRobberStealClientRpc` | (int thief, victim, ResourceType) | 약탈 결과 |
| `NotifyDevCardPurchasedClientRpc` | (int player, DevCardType) | 발전카드 구매 |
| `NotifyDevCardUsedClientRpc` | (int player, DevCardType) | 발전카드 사용 |
| `NotifyLongestRoadChangedClientRpc` | (int player, bool gained) | 최장교역로 |
| `NotifyLargestArmyChangedClientRpc` | (int player, bool gained) | 최대기사단 |
| `NotifyBankTradeClientRpc` | (int player, ResourceType gave, recv, rate) | 은행 거래 |
| `NotifyPlayerTradeClientRpc` | (int p1, p2) | 플레이어 거래 |
| `NotifyResourceUpdateClientRpc` | (int player, ResArray, ClientRpcParams) | 자원 상세 (대상만) |
| `NotifyPublicResourceCountClientRpc` | (int player, totalCount) | 공개 자원 수 |
| `NotifyVPChangedClientRpc` | (int player, vp) | 승점 변경 |
| `NotifyDiscardRequiredClientRpc` | (int player, count, ClientRpcParams) | 디스카드 요구 (대상만) |
| `NotifyIncomingTradeProposalClientRpc` | (int proposer, ResArray, ClientRpcParams) | 거래 제안 (대상만) |
| `NotifyStealCandidatesClientRpc` | (int[], ClientRpcParams) | 약탈 후보 (턴 플레이어만) |
| `SyncFullBoardStateClientRpc` | (BoardSnapshot) | 전체 보드 동기화 (접속/재접속) |

---

## 4. 커스텀 직렬화 타입

```csharp
HexCoordSerialized : INetworkSerializable   // HexCoord의 q, r 직렬화
ResArray : INetworkSerializable              // ResourceType별 int[5] 고정 배열
BoardSnapshot : INetworkSerializable         // 타일, 건물, 도로, 도적 위치 전체
PlayerSnapshot : INetworkSerializable        // 플레이어 상태 스냅샷
```

---

## 5. 상태 동기화 전략

### 5.1 NetworkVariable (지속 상태 — 새 접속자 자동 수신)

| 데이터 | 이유 |
|--------|------|
| TurnNumber, CurrentPlayerIndex, FirstPlayerIndex | 턴 상태 즉시 필요 |
| CurrentPhase, CurrentBuildMode | UI 상태 결정 |
| DevCardState, DevCardDeckRemaining | 발전카드 UI |
| IsWaitingForDiscard | 디스카드 대기 |
| LongestRoadHolder, LargestArmyHolder | 보너스 표시 |
| PlayerVP, PlayerTotalResCount, PlayerKnightsPlayed | 플레이어 공개 정보 |

### 5.2 ClientRpc (일회성 이벤트)

| 데이터 | 이유 |
|--------|------|
| 주사위 결과, 건물/도로 배치, 도적 이동 | 애니메이션/비주얼 트리거 |
| 약탈 결과, 발전카드 구매/사용 | 로그/알림 표시 |
| 자원 변경 (TargetedClientRpc) | 본인에게만 상세 자원 |
| 거래 제안/취소, 디스카드 요구 | 대상 플레이어에게만 |

### 5.3 보드 상태 동기화

1. **개별 변경**: ClientRpc (NotifyBuildingPlaced, NotifyRoadPlaced, NotifyRobberMoved)
2. **전체 동기화**: 게임 시작/재접속 시 `SyncFullBoardStateClientRpc`
3. **클라이언트 보드 미러**: HexGrid 인스턴스 보유, 호스트 명령에 따라서만 변경

---

## 6. 모드 분기 플로우

### 6.1 씬 전환

```
MainMenu
  ├── "로컬 플레이" → IsLocalPlay = true → Lobby (로컬 설정)
  └── "온라인 플레이" → IsLocalPlay = false → Lobby (네트워크)
                          ├── "방 만들기" → IsHosting = true
                          └── "참가하기" → IsHosting = false
```

### 6.2 GameBootstrapper (게임 씬 진입 시)

```csharp
// GameBootstrapper.cs — 씬에 배치, DefaultExecutionOrder(-200)
void Awake()
{
    if (SceneFlowManager.Instance.IsLocalPlay)
    {
        var go = new GameObject("LocalGameManager");
        var mgr = go.AddComponent<LocalGameManager>();
        GameServices.GameManager = mgr;
    }
    else
    {
        // 호스트: NetworkGameManager 프리팹 스폰
        // 클라이언트: NetworkObject 자동 수신
        if (NetworkManager.Singleton.IsServer)
        {
            var instance = Instantiate(networkGameManagerPrefab);
            instance.GetComponent<NetworkObject>().Spawn();
        }
    }
}
```

### 6.3 플레이어 인덱스 매핑

```
호스트:
1. ConnectedClientsIds 수집 → 순서 셔플
2. clientId → playerIndex 매핑 테이블 NetworkList 동기화
3. 각 클라이언트에 본인 playerIndex를 TargetedClientRpc 알림

클라이언트:
- 수신한 playerIndex를 LocalPlayerIndex로 저장
```

---

## 7. 핵심 흐름 예시: TryBuildSettlement

```
[클라이언트]
1. UI → NetworkGameManager.TryBuildSettlement(vertexId) 호출
2. if (!IsServer) → RequestBuildSettlementServerRpc(vertexId), return false

[호스트]
3. ServerRpc 수신 → senderClientId로 playerIndex 확인
4. hostGameManager.TryBuildSettlement(vertexId) 실행
5. 성공 시:
   - NotifyBuildingPlacedClientRpc(playerIndex, vertexId, Settlement)
   - NotifyResourceUpdateClientRpc(해당 플레이어만)
   - NotifyPublicResourceCountClientRpc(해당 플레이어)
   - NotifyVPChangedClientRpc(playerIndex, vp)
   - NetworkVariable 갱신 (phase, buildMode 등)

[클라이언트]
6. ClientRpc 수신 → 이벤트 발행 → UI 자동 갱신
```

---

## 8. 구현 순서

### Phase 1: 기반 인프라

1. NetworkSerializable 타입 정의 (HexCoordSerialized, ResArray, BoardSnapshot)
2. GameBootstrapper 작성 (모드별 GameManager 생성)
3. LocalGameManager 최소 리팩토링 (Awake에서 GameServices 등록 제거)

### Phase 2: NetworkGameManager 코어

4. NGM 스켈레톤 (NetworkBehaviour + IGameManager, NetworkVariable, 이벤트)
5. 호스트 측 로직 (내부 LGM 인스턴스 + 이벤트 → NetworkVariable/ClientRpc)
6. ServerRpc 구현 (턴 검증 + LGM 호출 + 결과 브로드캐스트)
7. ClientRpc 구현 (이벤트 발행 + 보드 미러 갱신)

### Phase 3: 보드 동기화

8. 보드 초기 동기화 (SyncFullBoardState)
9. 초기 배치 동기화 (스네이크 드래프트)

### Phase 4: 발전카드/거래/도적

10. 발전카드 동기화
11. 거래 동기화 (은행 + 플레이어)
12. 도적/디스카드 동기화

### Phase 5: 로비 통합

13. LobbyController 확장 (NetworkManager.SceneManager 씬 전환)
14. 연결/재접속 처리
15. 플레이어 이름 동기화

### Phase 6: 테스트

16. 로컬 회귀 테스트
17. 네트워크 2인 테스트 (ParrelSync)

---

## 9. 파일 목록

### 신규 파일

| 파일 | 설명 |
|------|------|
| `Assets/Scripts/Network/NetworkGameManager.cs` | IGameManager 네트워크 프록시 (핵심) |
| `Assets/Scripts/Network/NetworkSerializables.cs` | 직렬화 타입 |
| `Assets/Scripts/Network/GameBootstrapper.cs` | 모드별 GameManager 생성 |
| `Assets/Scripts/Network/PlayerIndexMapper.cs` | clientId ↔ playerIndex 매핑 |
| `Assets/Scripts/Network/BoardSynchronizer.cs` | 보드 상태 직렬화/역직렬화 |
| `Assets/Prefabs/Network/NetworkGameManager.prefab` | NetworkObject 프리팹 |

### 수정 파일

| 파일 | 변경 |
|------|------|
| `LocalGameManager.cs` | Awake GameServices 등록 제거, hexGridView 외부 주입 |
| `SceneFlowManager.cs` | 네트워크 씬 전환 분기 |
| `LobbyController.cs` | 네트워크 게임 시작 |
| `TurnManager.cs` | NetworkGameManager에 흡수 (제거 후보) |
| `GameNetworkManager.cs` | 재접속 핸들링 추가 |
| `HexGridView.cs` | 외부 Grid 데이터 주입 지원 |

---

## 10. TurnManager 처리 방침

기존 TurnManager.cs는 NetworkGameManager에 흡수한다:
- 턴 관리와 게임 로직 분리 시 상태 불일치 위험
- NetworkGameManager가 동일한 NetworkVariable 관리
- LocalGameManager도 턴을 내부 관리 → 구조 일관성
