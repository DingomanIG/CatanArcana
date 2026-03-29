# ArcanaCatan - 개발 로드맵

## Phase 1: 코어 시스템 ✅

- [x] 헥스 그리드 생성 (19타일 배치, 큐브 좌표)
- [x] 교차점/변 데이터 구조 (HexVertex, HexEdge)
- [x] 타일에 자원 타입/숫자 토큰 할당
- [x] 헥스 메시 생성 + 뷰 (HexMeshGenerator, HexGridView)
- [x] 카메라 세팅 (2.5D 오소그래픽, 45° + 팬/줌)
- [x] 바다 타일 + 씬 구성
- [x] 씬 분리 (MainMenu, Lobby, Game) + SceneFlowManager

## Phase 2: 게임 로직

- [x] 턴 매니저 (턴 순서, 페이즈 전환)
- [x] 주사위 굴림 UI (2d6 개별 주사위 눈, 도트 패턴)
- [x] 자원 분배 (주사위 결과 → 인접 건물 → 자원 지급)
- [x] 건설 시스템 (도로, 마을, 도시 + BuildModeController)
- [x] 배치 규칙 검증 (인접 조건, 거리 규칙)
- [x] 건설 하이라이트 + 레이캐스트 배치 (BuildingVisuals)
- [x] 발전카드 구매/사용 (기사/독점/풍년/도로건설/승점)
- [x] 도적 이동 (7 굴림 → MoveRobber 페이즈)
- [x] 도적 자원 약탈 (인접 플레이어에서 도둑질)
- [x] 최장교역로 판정 (LongestRoadCalculator)
- [x] 최대기사단 판정 (2점)
- [x] 승리 조건 체크 (10점)

## Phase 3: 거래 시스템 ✅

- [x] 은행 거래 (4:1)
- [x] 항구 거래 (3:1, 2:1) + 항구 비주얼
- [x] 플레이어 간 거래 (제안/수락/거절)

## Phase 4: 초기 배치 ✅

- [x] 순서/역순 배치 흐름
- [x] 마을 + 도로 배치 UI/로직
- [x] 두 번째 마을 자원 지급
- [x] 랜덤 선플레이어 결정 + 턴 순서 알림 UI

## Phase 5: UI (UI Toolkit)

- [x] 메인 메뉴 (MainMenuController + UXML/USS)
- [x] 로비 / 대기실 (LobbyController + UXML/USS)
- [x] 인게임 HUD (자원, 주사위, 턴/페이즈 정보)
- [x] 자원 UI ↔ 게임 로직 연동 (OnResourceChanged)
- [x] VP UI ↔ 게임 로직 연동 (OnVPChanged)
- [x] 거래창 오버레이 (placeholder)
- [x] 건설 메뉴 (구조 + 로직 연결, 오버플로 수정)
- [x] 인게임 규칙 뷰어 (스크롤 오버레이)
- [x] 발전카드 목록 UI
- [x] 결과 화면 (승자/순위/VP 상세 + 메뉴 복귀)
- [x] 유틸리티 바 (옵션/규칙/음량, 좌측 배치)
- [x] 음량 설정 + 옵션 오버레이
- [ ] 한글 폰트 적용 (Noto Sans KR 등)

## Phase 6: AI

- [x] AI 컨트롤러 시스템 (AIController + AIBoardEvaluator)
- [x] AI 난이도 3단계 (Easy/Medium/Hard)
- [x] AI 초기 배치 (교차점/도로 평가)
- [x] AI 일반 턴 (주사위→건설→카드→거래→종료)
- [x] AI 도적 배치 로직 (타일 가치 + 상대 VP 기반)
- [x] AI 발전카드 사용 (기사/도로건설/풍년/독점)
- [x] AI 은행 거래 로직 (자원 부족 시 자동 교환)
- [ ] AI 플레이어 간 거래 (제안/수락)

## Phase 7: 멀티플레이어

- [x] Unity Authentication 연동
- [x] Lobby 방 생성/검색/참가
- [x] Relay 연결 (P2P)
- [x] Netcode 기본 동기화
- [ ] TurnManager → IGameManager 구현 (네트워크 모드)
- [ ] 호스트 마이그레이션 / 재접속

## Phase 8: 아트 & 폴리싱

- [ ] 3D 타일/건물 모델
- [ ] 머티리얼 / 텍스처
- [ ] 파티클 이펙트 (건설, 자원 획득)
- [ ] BGM / SFX
- [ ] UI 스타일링

## Phase 9: 빌드 & 배포

- [ ] WebGL 빌드 설정
- [ ] 빌드 최적화 (압축, 로딩)
- [ ] 배포 및 테스트
