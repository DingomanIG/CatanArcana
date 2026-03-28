# ArcanaCatan

## Project
- **Unity 6** (6000.3.11f1) + URP
- 2.5D 탑다운 온라인 카탄 보드게임
- WebGL 빌드 대상
- ArcanaStock 본 프로젝트의 워크플로우 테스트 겸 프로토타입

## Tech Stack
- **Rendering**: URP, Orthographic Camera (45°)
- **Network**: Netcode for GameObjects + Relay + Lobby (P2P)
- **UI**: UI Toolkit
- **Input**: Unity Input System

## Structure
```
Assets/
  Scripts/
    CameraController.cs        # 2.5D 카메라 + 줌
    Network/
      GameNetworkManager.cs     # Relay 연결, 호스트/클라이언트
      LobbyManager.cs           # 방 생성/참가
      TurnManager.cs             # 턴 동기화 (2d6)
      NetworkTestUI.cs           # 테스트용 OnGUI
  Scenes/
    SampleScene.unity
Docs/
  GDD.md                        # 게임 기획서
```

## Rules
- 커밋 시 항상 push까지 수행
- Unity .meta 파일 포함하여 커밋
- Library/, Temp/, Logs/, UserSettings/ 는 gitignore 처리됨

## Related
- ArcanaStock 기획서: `C:\Users\bonek\Documents\AntigravityFolder\ArcanaStock\CLAUDE.md`
