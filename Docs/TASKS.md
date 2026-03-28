# ArcanaCatan - 작업 현황

## 코어 시스템
- [x] 헥스 그리드 생성 (19타일, 큐브 좌표)
- [x] 교차점/변 데이터 구조 (HexVertex, HexEdge)
- [x] 타일 자원 타입/숫자 토큰 할당
- [x] 헥스 메시 생성 + 뷰 (HexMeshGenerator, HexGridView)
- [x] 카메라 세팅 (2.5D 오소그래픽, 45°)
- [ ] 씬 분리 (MainMenu, Lobby, Game)

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
- [ ] 메인 메뉴 UI
- [ ] 로비 / 대기실 UI
- [ ] 발전카드 목록 UI
- [ ] 결과 화면 UI
- [ ] NetworkTestUI → GameHUD 전환 (씬 설정)
- [ ] 한글 폰트 적용 (Noto Sans KR 등)

## 건설 시스템
- [x] 자원 매니저 (PlayerState: 플레이어별 자원 보유량)
- [x] 도로 건설 + 배치 규칙 (BuildingSystem + BuildModeController)
- [x] 마을 건설 + 거리 규칙
- [x] 도시 업그레이드
- [x] 건설 비용 차감 + UI 연동
- [x] 건설 하이라이트 + 레이캐스트 배치 (BuildingVisuals)

## 거래 시스템
- [ ] 은행 거래 (4:1)
- [ ] 항구 거래 (3:1, 2:1)
- [ ] 플레이어 간 거래 (제안/수락/거절)

## 특수 요소
- [x] 도적 이동 (7 굴림 → MoveRobber 페이즈)
- [x] 자원 7장 초과 시 반납 (자동 폐기)
- [x] 승리 조건 체크 (10점)
- [ ] 도적 자원 약탈 (인접 플레이어에서 도둑질)
- [ ] 발전카드 구매/사용
- [ ] 최장교역로 판정 (2점)
- [ ] 최대기사단 판정 (2점)

## 초기 배치
- [ ] 순서/역순 배치 흐름
- [ ] 마을 + 도로 배치 UI/로직
- [ ] 두 번째 마을 자원 지급

## AI
- [ ] IPlayerController 인터페이스
- [ ] AI 쉬움 (랜덤)
- [ ] AI 보통 (확률 기반)
- [ ] AI 어려움 (전략적)
- [ ] AI 거래/도적 로직

## 아트 & 폴리싱
- [ ] 3D 타일/건물 모델
- [ ] 머티리얼 / 텍스처
- [ ] 파티클 이펙트
- [ ] BGM / SFX

## 빌드 & 배포
- [ ] WebGL 빌드 설정
- [ ] 빌드 최적화
- [ ] 배포 및 테스트
