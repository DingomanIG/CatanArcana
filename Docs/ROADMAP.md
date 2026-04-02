# ArcanaCatan → Arcana Board Module 로드맵

> 아르카나스톡의 보드게임 모듈. 카탄을 베이스로 경제 시뮬레이션 요소를 단계적으로 접목.

---

## Phase 1_1: 카탄 프로토타입 ✅

Claude Code + Unity 워크플로우 검증용 기본 카탄 구현.

- [x] 헥스 그리드 (19타일, 큐브 좌표, pointy-top)
- [x] 건설/거래/발전카드/도적 — 카탄 풀 룰셋
- [x] AI 3난이도 (Easy/Medium/Hard) 대전 + 6가지 전략 기반 의사결정
- [x] UI Toolkit 기반 전체 UI (메뉴/로비/HUD/거래/결과)
- [x] Netcode + Relay + Lobby 네트워크 기반 구축

---

## Phase 1_2: WebGL 빌드 & 배포 ✅

로컬 WebGL 빌드 → GitHub Pages 수동 배포. (Unity Personal은 CI 시리얼 미지원)

- [x] GitHub Actions 배포 워크플로우 (workflow_dispatch → deploy-pages)
- [x] WebGL 빌드 설정 (Player Settings, Gzip 압축, 디컴프레션 폴백)
- [x] GitHub Pages 배포 완료 (https://dingomanig.github.io/CatanArcana/)
- [x] 로컬 빌드 → Docs/WebGL/ → push → 수동 배포 플로우 확립

---

## Phase 1_3: 온라인 멀티플레이 ⬅️ NOW

P2P Relay 기반 온라인 대전. 로컬 로직 재활용 (프록시 패턴).

- [ ] NetworkGameManager 셸 생성 (IGameManager + LocalGameManager 위임)
- [ ] 상태 동기화 레이어 (리소스/VP/건물 → NetworkVariable/ClientRpc)
- [ ] 액션 RPC화 (TryBuild/TryTrade/TryUse → ServerRpc)
- [ ] 씬 전환 시 모드 분기 (로컬 vs 온라인)
- [ ] 연결 끊김/재접속 처리
- [ ] 온라인 대전 테스트

---

## Phase 1_4: 1차 아트 & 비주얼

placeholder → 실제 아트 리소스 교체. 2.5D 비주얼 완성.

- [ ] 3D 타일/건물/캐릭터 모델
- [ ] 머티리얼/텍스처/라이팅
- [ ] 파티클 이펙트 (건설, 자원, 거래)
- [x] BGM/SFX (BGMManager + SFXManager, 버튼/주사위/거래 효과음)
- [ ] UI 스킨 (아르카나스톡 세계관 반영)

---

## Phase 2: 경제 시스템 확장

카탄의 고정 교환비율 → **동적 시장 경제**로 전환.

- [ ] 자원/제작품 시세 변동 (수요/공급 기반)
- [ ] 골드(G) 화폐 도입 — 자원 매매의 공통 단위
- [ ] 은행 → 상회 전환 (거래소 개념)
- [ ] 투자/배당 기초 시스템

---

## Phase 3: 제작 시스템 도입

카탄 자원(5종)을 원자재로, **가공품/완성품 제작 체인** 추가.

- [ ] 원자재 → 가공품(1턴) → 완성품(3턴) 제작 파이프라인
- [ ] 제작소 건물 타입 + 배치 시스템
- [ ] 제작품 거래 (플레이어 간 + 은행)
- [ ] 승리 조건 확장 (제작 VP)

---

## Phase 4: 시즌제 & 이벤트

아르카나스톡의 **듀얼 루프** 구조를 보드게임에 적용.

- [ ] 수확턴 (타일 주사위 2d6 → 보너스 수확)
- [ ] 이벤트턴 (섹터 주사위 2d6 → 시세 방향 결정)
- [ ] 시즌 3막 구조 (개척기/성장기/결산기)
- [ ] 시즌 보상 및 리셋 시스템

---

## Phase 5: 2차 아트 & 비주얼

Phase 2~4 콘텐츠에 맞춘 아트 리소스 보강.

- [ ] 제작소/시장 3D 모델
- [ ] 시즌 테마별 이펙트/라이팅
- [ ] 추가 BGM/SFX
- [ ] UI 스킨 확장

---

## Phase 6: 수익화 (WebGL 광고)

WebGL 빌드 특성상 Unity Ads/AdMob 사용 불가 → 웹 기반 광고 연동.

- [ ] Google AdSense 연동 (index.html 배너 삽입)
- [ ] 게임 내 광고 타이밍 설계 (턴 종료, 로비 대기 등)
- [ ] jslib 플러그인으로 Unity↔JS 광고 브릿지 구현
- [ ] 게임 배포 플랫폼 검토 (CrazyGames, Poki 등 — 자체 광고 SDK)

---

## Phase 7: 서버 & 인프라

P2P Relay → 서버 기반 아키텍처 전환.

- [ ] 서버 솔루션 선정 (TBD: PlayFab / 커스텀 / 기타)
- [ ] 계정 시스템 + 데이터 영속성
- [ ] 매칭 + 리더보드
- [ ] 호스트 마이그레이션 / 재접속

---

## Phase 8: 아르카나스톡 통합

독립 모듈 → 아르카나스톡 메인 프로젝트에 병합.

- [ ] 아르카나스톡 프로젝트로 코드 이전
- [ ] 본 게임 경제 시스템과 연동
- [ ] 보드게임 콘텐츠로서 인게임 삽입
- [ ] 공통 UI/UX 통일
