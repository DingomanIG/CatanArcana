# ArcanaCatan

## Project
- **Unity 6** (6000.3.11f1) + URP
- 2.5D 탑다운 온라인 카탄 보드게임
- WebGL 빌드 대상
- ArcanaStock 본 프로젝트의 워크플로우 테스트 겸 프로토타입

## Tech Stack
- **Rendering**: URP, Orthographic Camera (45°)
- **Network**: Netcode for GameObjects + Relay + Lobby (P2P)
- **UI**: UI Toolkit (UXML + USS + C# Controller)
- **Input**: Unity Input System
- **IDE 연동**: Claude Code ↔ Unity Editor (씬/USS/UXML 직접 편집 가능)

## Architecture Decisions
- **좌표계**: 큐브 좌표 (q+r+s=0), flat-top 헥스
- **데이터/뷰 분리**: HexGrid(순수 C#) + HexGridView(MonoBehaviour)
- **확장성**: radius 파라미터로 보드 크기 조절, 임의 형태 지원
- **토폴로지**: 위치 기반 키로 교차점/변 중복 제거
- **게임 매니저 추상화**: IGameManager 인터페이스로 로컬/네트워크 분리
  - LocalGameManager: 로컬 전용 (네트워크 불필요)
  - TurnManager: 네트워크 모드 (향후 IGameManager 구현)

## Structure
```
Assets/
  Scenes/
    MainMenu.unity              # 메인 메뉴 씬
    Lobby.unity                 # 로비 대기실 씬
    Game.unity                  # 인게임 씬 (기존 SampleScene)
  Scripts/
    IGameManager.cs             # 게임 매니저 인터페이스 + GamePhase
    LocalGameManager.cs         # 로컬 전용 게임 매니저
    CameraController.cs
    PlayerState.cs, BuildingCosts.cs
    Network/
      GameNetworkManager.cs     # DontDestroyOnLoad 싱글톤
      LobbyManager.cs           # DontDestroyOnLoad 싱글톤
      TurnManager.cs            # 네트워크 턴 매니저 (향후 IGameManager 구현)
      NetworkTestUI.cs          # 임시 OnGUI (비활성화 예정)
    HexGrid/
      HexCoord.cs, HexTile.cs, HexVertex.cs, HexEdge.cs
      HexGrid.cs, HexBoardSetup.cs
      HexMeshGenerator.cs, HexGridView.cs
    Building/
      BuildingType.cs, BuildingSystem.cs
      BuildingVisuals.cs, BuildModeController.cs
    DevCard/
      DevelopmentCard.cs, DevCardDeck.cs
      LongestRoadCalculator.cs
    UI/
      SceneFlowManager.cs       # 씬 전환 관리 (DontDestroyOnLoad)
      MainMenuController.cs     # 메인 메뉴 UI
      LobbyController.cs        # 로비 대기실 UI
      GameHUDController.cs      # 인게임 HUD (IGameManager 기반)
  UI/
    UXML/
      MainMenu.uxml, LobbyScreen.uxml, GameHUD.uxml
    USS/
      Common.uss                # 공통 스타일 (카탄 다크 테마)
      MainMenu.uss, LobbyScreen.uss, GameHUD.uss
    Fonts/                      # 폰트 에셋
Docs/
  GDD.md, ROADMAP.md, TASKS.md
```

## Tone
- 대답은 매우 간략하게, 위트 있는 대화 스타일로
- 일은 즐겁게!

## Rules
- 커밋 시 항상 push까지 수행
- Unity .meta 파일 포함하여 커밋
- Library/, Temp/, Logs/, UserSettings/ 는 gitignore 처리됨
- 작업 완료 시 `Docs/ROADMAP.md`, `Docs/TASKS.md` 체크박스 업데이트

## Resources
- 구현 중 필요한 리소스(아이콘, 이미지, 3D 모델링 등)는 유저에게 요청할 것
- 임시 placeholder 사용 가능하나, 최종 에셋은 별도 제공 필요

## Agents
- 3에이전트 병렬 개발: 기획 / UI / 구현
- 상세: `Docs/AGENTS.md`
- 기획 선행 → UI·구현 병렬 (기획 간단하면 3개 동시)
- 폴더 경계 엄수 (충돌 방지)

## Related
- ArcanaStock 기획서: `C:\Users\bonek\Documents\AntigravityFolder\ArcanaStock\CLAUDE.md`
