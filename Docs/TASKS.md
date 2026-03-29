# ArcanaCatan - 작업 현황

## 코어 시스템
- [x] 헥스 그리드 생성 (19타일, 큐브 좌표)
- [x] 교차점/변 데이터 구조 (HexVertex, HexEdge)
- [x] 타일 자원 타입/숫자 토큰 할당
- [x] 헥스 메시 생성 + 뷰 (HexMeshGenerator, HexGridView)
- [x] 카메라 세팅 (2.5D 오소그래픽, 45° + 팬/줌)
- [x] 바다 타일 + 씬 구성
- [x] 씬 분리 (MainMenu, Lobby, Game) + SceneFlowManager
- [x] 씬 플로우 연결 (MainMenu → Lobby → Game, 로컬 플레이 지원)

## 네트워크
- [x] Unity Authentication 연동
- [x] Relay 연결 (P2P)
- [x] Lobby 방 생성/검색/참가
- [x] Netcode 기본 동기화
- [ ] 호스트 마이그레이션 / 재접속

## 아키텍처
- [x] IGameManager 인터페이스 (로컬/네트워크 공통)
- [x] LocalGameManager 로컬 전용 구현
- [x] GameHUDController → IGameManager 기반 리팩토링
- [x] PlayerState 순수 C# 클래스 (자원/건물/VP)
- [x] BuildingCosts 정적 비용 데이터
- [ ] TurnManager → IGameManager 구현 (네트워크 모드)

## 턴 시스템
- [x] 턴 매니저 (턴 순서, 페이즈 전환)
- [x] 주사위 굴림 (2d6)
- [x] 주사위 개별값 동기화 (Die1/Die2 NetworkVariable)
- [x] 자원 분배 (주사위 결과 → 인접 건물 → 자원 지급)

## UI (UI Toolkit)
- [x] 인게임 HUD 레이아웃 (UXML)
- [x] 인게임 HUD 스타일 (USS, 카탄 다크 테마)
- [x] HUD 컨트롤러 (턴/페이즈/주사위 연동)
- [x] 주사위 UI (2d6 개별 주사위 눈 표시, 도트 패턴)
- [x] 건설 메뉴 오버레이 → 건설 로직 연결
- [x] 거래창 오버레이 (placeholder)
- [x] 인게임 규칙 뷰어 (스크롤 오버레이)
- [x] 자원 UI ↔ 게임 로직 연동 (OnResourceChanged)
- [x] VP UI ↔ 게임 로직 연동 (OnVPChanged)
- [x] 메인 메뉴 UI (MainMenuController + UXML/USS)
- [x] 로비 / 대기실 UI (LobbyController + UXML/USS)
- [x] 공통 스타일시트 (Common.uss)
- [x] 발전카드 목록 UI
- [x] 결과 화면 UI (승자/순위/VP 상세 + 메뉴/리매치 버튼)
- [x] 유틸리티 바 (옵션/규칙/음량 버튼, 화면 왼쪽)
- [x] 음량 설정 오버레이 (BGM/SFX 슬라이더)
- [x] 옵션 오버레이 (규칙보기/기권/메인메뉴)
- [x] NetworkTestUI → GameHUD 전환 (씬 설정, 로컬 플레이 + 온라인 분기)
- [ ] 한글 폰트 적용 (Noto Sans KR 등)

## 건설 시스템
- [x] 자원 매니저 (PlayerState: 플레이어별 자원 보유량)
- [x] 도로 건설 + 배치 규칙 (BuildingSystem + BuildModeController)
- [x] 마을 건설 + 거리 규칙
- [x] 도시 업그레이드
- [x] 건설 비용 차감 + UI 연동
- [x] 건설 하이라이트 + 레이캐스트 배치 (BuildingVisuals)

## 거래 시스템
- [x] 은행 거래 (4:1)
- [x] 항구 거래 (3:1, 2:1) + 항구 비주얼
- [x] 플레이어 간 거래 (제안/수락/거절)
- [x] 거래 UI (탭 전환, 자원 선택, 수량 조절)

## 특수 요소
- [x] 도적 이동 (7 굴림 → MoveRobber 페이즈)
- [x] 자원 7장 초과 시 반납 (자동 폐기)
- [x] 승리 조건 체크 (10점)
- [x] 도적 자원 약탈 (인접 플레이어에서 도둑질, StealResource 페이즈 + UI)
- [x] 발전카드 구매/사용 (기사/독점/풍년/도로건설/승점)
- [x] 발전카드 덱 (DevCardDeck)
- [x] 최장교역로 판정 (LongestRoadCalculator, 2점)
- [x] 최대기사단 판정 (2점)

## 초기 배치
- [x] 순서/역순 배치 흐름
- [x] 마을 + 도로 배치 UI/로직
- [x] 두 번째 마을 자원 지급

## AI
- [x] AIController 시스템 (MonoBehaviour, 코루틴 기반 턴 처리)
- [x] AIBoardEvaluator 보드 평가 (교차점/도로/도적 타겟 스코어링)
- [x] AIDifficulty 3단계 (Easy=랜덤, Medium=확률, Hard=전략)
- [x] IGameManager에 AI 쿼리 메서드 추가 (유효 배치 위치)
- [x] LocalGameManager AI 모드 (humanPlayerIndex, IsMyTurn 제어)
- [x] AI 초기 배치 (마을+도로 자동 배치)
- [x] AI 주사위 → 건설 → 발전카드 → 거래 → 턴종료 흐름
- [x] AI 도적 배치 (상대 VP/타일 가치 기반)
- [x] AI 약탈 대상 선택 (VP+자원 높은 상대)
- [x] AI 발전카드 사용 (기사/도로건설/풍년/독점)
- [x] AI 은행 거래 (잉여→부족 자원 교환)
- [ ] AI 플레이어 간 거래 (제안/수락)

## 아트 & 폴리싱
- [ ] 3D 타일/건물 모델
- [ ] 머티리얼 / 텍스처
- [ ] 파티클 이펙트
- [ ] BGM / SFX

## 빌드 & 배포
- [ ] WebGL 빌드 설정
- [ ] 빌드 최적화
- [ ] 배포 및 테스트
