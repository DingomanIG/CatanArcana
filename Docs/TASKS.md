# ArcanaCatan - 작업 현황

## 우선순위 기준
- **P0**: 현재 Phase 필수 — 이거 없으면 Phase 완료 불가
- **P1**: 현재 Phase 권장 — 있으면 좋고, Phase 내 구현 목표
- **P2**: 다음 Phase 준비 또는 Phase 1 잔여

---

## Phase 1: 카탄 프로토타입 ✅

<details>
<summary>완료된 항목 (접기)</summary>

### 코어 시스템
- [x] 헥스 그리드 (19타일, 큐브 좌표, pointy-top)
- [x] 교차점/변 데이터 구조 (HexVertex, HexEdge)
- [x] 타일 자원 타입/숫자 토큰 할당
- [x] 헥스 메시 생성 + 뷰 (HexMeshGenerator, HexGridView)
- [x] 카메라 (2.5D 오소그래픽, 45° + 팬/줌)
- [x] 바다 타일 + 씬 구성
- [x] 씬 분리 (MainMenu/Lobby/Game) + SceneFlowManager

### 게임 로직
- [x] 턴 매니저 (턴 순서, 페이즈 전환)
- [x] 주사위 굴림 (2d6 개별 주사위 눈, 도트 패턴)
- [x] 자원 분배 (주사위 → 인접 건물 → 자원)
- [x] 건설 시스템 (도로/마을/도시 + 배치 규칙 + 하이라이트)
- [x] 거래 (은행 4:1, 항구 3:1/2:1, 플레이어 간)
- [x] 발전카드 (기사/독점/풍년/도로건설/승점)
- [x] 도적 (7 굴림 → 이동 → 약탈)
- [x] 최장교역로 / 최대기사단 판정
- [x] 승리 조건 (10점)
- [x] 초기 배치 (순서/역순 + 두 번째 마을 자원 지급)
- [x] 랜덤 선플레이어 + 턴 순서 알림

### UI
- [x] 메인 메뉴 / 로비 / 인게임 HUD
- [x] 거래창 / 건설 메뉴 / 발전카드 목록
- [x] 결과 화면 / 유틸리티 바 / 음량 설정 / 옵션
- [x] 인게임 규칙 뷰어
- [x] 이벤트 로그 + 토스트 알림
- [x] BGM/SFX 시스템 (BGMManager + SFXManager, 현재 곡 표시)
- [x] 발전카드 퀵슬롯 바 (하단 HUD VP 오른쪽 인라인, 클릭 즉시 사용)

### AI
- [x] AIController + AIBoardEvaluator (Lv1~9 난이도 시스템)
- [x] 초기 배치 / 일반 턴 / 도적 / 발전카드 / 은행 거래
- [x] AI 전략 시스템 (6가지 전략 기반 의사결정: FullOWS/RoadBuilder/FiveResource/CityRoad/Port/HybridOWS)
- [x] AI 난이도 1~9 레벨 시스템 (AIDifficultySettings 헬퍼, 레벨별 세분화 파라미터)

### 네트워크 기반
- [x] Authentication + Relay + Lobby + Netcode 기본 동기화

</details>

---

## Phase 1_2: WebGL 빌드 & 배포 ✅

### CI/CD 파이프라인
- [x] P0: GitHub Actions 배포 워크플로우 (workflow_dispatch → deploy-pages)
- [x] P0: WebGL Player Settings (Gzip 압축, 디컴프레션 폴백)
- [x] P0: GitHub Pages 배포 완료 (https://dingomanig.github.io/CatanArcana/)
- [x] P1: 로컬 빌드 → Docs/WebGL/ → push → 수동 배포 플로우 확립
- [x] P1: WebGL 텍스트 입력 수정 (네이티브 HTML input overlay — IME/한글 지원)
- ~~P0: CI 빌드 (Unity Personal 시리얼 미지원으로 로컬 빌드 전환)~~

---

## Phase 1_3: 온라인 멀티플레이 ⬅️ NOW

> 설계서: `Docs/Design/MULTIPLAYER_DESIGN.md`

### 1단계: 기반 인프라
- [x] P0: 멀티플레이 설계서 작성
- [x] P0: NetworkSerializables.cs (HexCoordNet, ResArray, TileSnapshot, VertexSnapshot, EdgeSnapshot, BoardSnapshot)
- [x] P0: GameBootstrapper.cs (모드별 GameManager 생성/분기)
- [x] P0: LocalGameManager 리팩토링 (GameServices 조건부 등록, InitializePlayers 공개, HexGridView 외부 주입)
- [x] P0: ParrelSync 패키지 설치 (에디터 2인 테스트용)

### 2단계: NetworkGameManager 코어
- [x] P0: NGM 스켈레톤 (NetworkBehaviour + IGameManager, NetworkVariable, 이벤트)
- [x] P0: 호스트 로직 (내부 LGM + 이벤트 → NetworkVariable/ClientRpc)
- [x] P0: ServerRpc 구현 (전체 액션: 주사위/턴/건설/발전카드/거래/도적 — 턴 검증 포함)
- [x] P0: ClientRpc 구현 (이벤트 발행 + 보드 미러 갱신 + TargetedClientRpc 자원 은닉)
- [x] P0: PlayerIndex 매핑 (NGM 내부 통합 — 셔플 + TargetedClientRpc 할당)

### 3단계: 보드 동기화
- [x] P0: 보드 초기 동기화 (SyncFullBoardState — BoardSnapshot 직렬화/역직렬화)
- [x] P0: 초기 배치 동기화 (스네이크 드래프트 네트워크 플로우)
  - LGM.OnInitialPlacementTurn 이벤트 → NGM ClientRpc로 릴레이
  - SuppressUICommands 플래그로 hostLGM의 BuildModeController 직접호출 억제
  - NotifyInitialPlacementBuildModeClientRpc → 해당 클라이언트 BuildMode 진입
  - NotifyInitialPlacementFinishedClientRpc → 초기 배치 완료 알림

### 4단계: 발전카드/거래/도적
- [x] P1: 발전카드 RPC (구매/기사/도로건설/풍년/독점)
- [x] P1: 거래 RPC (은행 + 플레이어 + 거래 응답)
- [x] P1: 도적/디스카드 RPC (이동/약탈후보/약탈/디스카드확인)

### 5단계: 로비 통합
- [x] P1: SceneFlowManager 네트워크 씬 전환 (NetworkManager.SceneManager, 호스트만 호출)
- [x] P1: LobbyController 네트워크 게임 시작 (동기화된 씬 전환)
- [x] P1: 플레이어 이름 동기화 (NetworkList<FixedString64Bytes> + RegisterPlayerNameServerRpc)
- [x] P1: 연결 해제/퇴장 처리 (OnClientDisconnectCallback, GoToMainMenu 네트워크 정리)
- [ ] P2: 재접속 처리 (SyncFullBoardState + 자원 재전송)

### 5.5단계: 네트워크 AI 버그 수정
- [x] 로비 AI 난이도 선택 기능 추가 (Lv1→Lv3→Lv5→Lv7→Lv9 순환)
- [x] 네트워크 AI 자동 준비(ready) 처리 (AI가 readyPlayers에 미포함 → 게임 시작 불가 수정)
- [x] AIController.SetDifficulties 동적 배열 크기 지원 (네트워크 인덱스 매핑 불일치 수정)

### 5.6단계: 디버그 치트 패널
- [x] P1: DebugCheatPanel (F9 토글 OnGUI — 자원/발전카드/건물재고/강도/페이즈 조작)
- [x] P1: LocalGameManager 치트 메서드 (CheatAddResource/DevCard/BuildingStock/Robber/Phase/Turn)
- [x] P1: NetworkGameManager 치트 ServerRpc + ClientRpc 동기화

### 6단계: 테스트
- [ ] P1: 로컬 회귀 테스트 (기존 AI 대전)
- [ ] P1: 네트워크 2인 테스트 (ParrelSync)

---

## Phase 2: 제작 시스템 🔄

### 데이터 설계
- [ ] P0: 아이템 등급 체계 정의 (원자재 / 가공품 / 완성품)
- [ ] P0: 제작 레시피 데이터 구조 (순수 C# or ScriptableObject)
  - 입력: 원자재 조합, 출력: 가공품/완성품, 소요턴, 제작소 요구
- [ ] P1: 아르카나스톡 `balance/items.js` 참조하여 아이템 목록 초안
- [ ] P1: 제작 시간 파라미터 (가공품=1턴, 완성품=3턴)
- [ ] P1: 완성품 VP 가치 정의

### 제작 로직
- [ ] P0: CraftingSystem 코어 (레시피 검증 → 자원 소모 → 대기열 등록)
- [ ] P0: PlayerState 인벤토리 확장 (기존 자원 5종 + 가공품 + 완성품 슬롯)
- [ ] P0: 턴 경과 시 제작 완료 처리 (IGameManager 턴 콜백 연동)
- [ ] P1: 제작 대기열 (큐 시스템, 동시 제작 제한)
- [ ] P1: IGameManager에 제작 메서드 추가 (StartCraft, GetCraftQueue 등)
- [ ] P1: 제작품 → VP 변환 로직 (승리 조건 확장)

### 제작소 건물
- [ ] P0: BuildingType에 제작소(Workshop) 추가
- [ ] P0: 제작소 배치 규칙 정의 (교차점 배치? 마을 인접?)
- [ ] P0: 제작소 건설 비용 + BuildingCosts 확장
- [ ] P1: 제작소 레벨업 (기본→전문: 특정 레시피 해금 or 속도 보너스)
- [ ] P1: 제작소 비주얼 (BuildingVisuals 확장)

### 제작 거래
- [ ] P1: 가공품/완성품을 거래 시스템에 통합
- [ ] P1: 거래 UI에 가공품/완성품 탭 추가
- [ ] P2: 완성품 전용 시장 (고급 거래)

### 제작 UI
- [ ] P0: 제작 메뉴 오버레이 (UXML + USS)
- [ ] P0: 레시피 리스트 (재료 아이콘 + 수량 + 제작 버튼)
- [ ] P0: 재료 부족 시 비활성 표시
- [ ] P1: 제작 대기열 상태 표시 (진행 바 or 턴 카운트)
- [ ] P1: 인벤토리 패널 확장 (원자재/가공품/완성품 분류 탭)
- [ ] P1: HUD에 제작 버튼 추가 (GameHUD.uxml 확장)

### AI 제작
- [ ] P1: AI 제작 의사결정 (보유 자원 → 가능한 레시피 → VP 효율 판단)
- [ ] P1: AIBoardEvaluator에 제작소 배치 평가 추가
- [ ] P2: 난이도별 제작 전략 분화 (Easy=랜덤, Hard=VP 최적화)

---

## 미완료: Phase 1 잔여

- [ ] P2: AI 플레이어 간 거래 (제안/수락 로직)
- [ ] P2: 한글 폰트 적용 (Noto Sans KR)
- [ ] P2: TurnManager → IGameManager 구현 (네트워크 모드) → Phase 1_3으로 이동
- [ ] P2: 호스트 마이그레이션 / 재접속 → Phase 1_3으로 이동

---

## Phase 3 예고: 경제 시스템

> Phase 3 진입 시 상세 태스크 작성

- 자원/제작품 시세 변동 엔진 (수요/공급 알고리즘)
- 골드(G) 화폐 도입 — 거래의 공통 단위
- 은행 → 상회(거래소) 전환
- 투자/배당 기초 시스템
- 시세 차트 UI

## Phase 4 예고: 시즌제 & 이벤트

> Phase 4 진입 시 상세 태스크 작성

- 수확턴 (타일 2d6) / 이벤트턴 (섹터 2d6) 이중 루프
- 시즌 3막 구조 (개척기/성장기/결산기)
- 시즌 보상 + 리셋
- 랜덤 이벤트 시스템

## Phase 5 예고: 아트 & 비주얼

> Phase 5 진입 시 상세 태스크 작성

- 3D 모델 (타일/건물/캐릭터)
- 머티리얼 / 텍스처 / 라이팅
- 파티클 이펙트
- BGM / SFX
- UI 스킨 (아르카나스톡 세계관)
