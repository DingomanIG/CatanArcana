---
tags:
  - type/설계
  - domain/UI
  - status/진행중
created: 2026-04-18
updated: 2026-04-18
---
# UI Toolkit → UGUI 마이그레이션 계획

> GameHUD를 중심으로 UGUI(Canvas) 전환. MainMenu/Lobby는 단순 재구축.

---

## 전환 방침

| 항목 | 방침 |
|------|------|
| GameHUD | UI Toolkit → UGUI 마이그레이션 (레이아웃 개편 포함) |
| MainMenu | 새로 만들기 (단순, 중요도 낮음) |
| Lobby | 새로 만들기 (단순, 중요도 낮음) |
| 오버레이 10개 | 전부 유지, UGUI 프리팹으로 전환 |
| 핸드 카드 | 이미 UGUI — 변경 없음 |

---

## GameHUD 레이아웃 개편

### 제거 항목
- ~~상단바~~ (턴번호 / 현재플레이어 / 페이즈 표시) — 전부 제거
- ~~퀵슬롯~~ — 핸드 카드가 이미 기능
- ~~페이즈 표시~~ — 액션 버튼이 대체

### 새 레이아웃

```
┌──────────────────────────────────────────────────────────┐
│ [⚙][?][♪]                          [상대1][상대2][상대3] │
│  ♪ BGM名                                      [은행카드] │
│                                                          │
│ ┌──────────┐                                             │
│ │이벤트로그│                                             │
│ │          │         [ 토스트 알림 ]                      │
│ │          │            (정중앙)                          │
│ │          │                                             │
│ │          │                          ┌──────────┐       │
│ │          │                          │ 주사위   │       │
│ │          │                          │ 액션버튼 │       │
│ │          │                          │ 건설버튼 │       │
│ └──────────┘                          │ 거래     │       │
│                                       └──────────┘       │
│ [내 정보 카드]                 VP | 도로 | 기사 | 발전 | 자원│
└──────────────────────────────────────────────────────────┘
              ~~~ 핸드 카드 (기존 UGUI 유지) ~~~
```

### 영역별 상세

#### 1. 유틸리티 버튼 — 좌상단
- 위치: 좌상단 구석
- 버튼: ⚙ 옵션, ? 규칙, ♪ 음량 (추후 추가 가능)
- 바로 아래에 `♪ BGM名` 라벨

#### 2. 상대방 카드 — 우상단
- 기존 디자인 유지
- 우상단으로 이동 (가로 정렬)
- **현재 턴 플레이어 하이라이트** 추가 (테두리 강조)
- 턴 전환 시 토스트 알림과 함께 하이라이트 전환

#### 3. 은행 카드 — 상대방 카드 우측
- 상대카드 옆에 배치
- 기존 기능 유지 (자원 6종 + 발전카드 잔량)

#### 4. 이벤트 로그 — 좌측
- 위치: 좌측 (유틸 버튼 아래)
- 접기/펼치기 불필요
- 기존 색상 코드 유지

#### 5. 액션 패널 — 우측 하단
- 기존 구성 유지
  - 주사위 표시 (pip 3x3 도트)
  - 게임시작 / 주사위굴리기 / 턴종료
  - 도로건설 / 마을건설 / 도시건설 / 발전카드구매
  - 거래 버튼
  - 초기배치 / 도적이동 안내 라벨
- 페이즈별 버튼 표시/숨김 로직 유지

#### 6. 하단바 — 내 정보
- **상대카드와 유사한 디자인**으로 통일
- 표시 항목: VP, 도로, 기사, 발전카드, 자원 총합
- ~~퀵슬롯 삭제~~
- 턴 번호: 유저가 직접 위치 결정 (프리팹에 포함하되 위치 조정 가능하게)

#### 7. 토스트 알림 — 화면 정중앙
- 기존 유형 유지: 도적, 기사, 교역로, 군대, 거래
- **현재 턴 플레이어 전환 시 토스트 표시** 추가
- 위치를 화면 정중앙으로 (핸드카드에 가리지 않게)
- 자동 페이드아웃 유지

#### 8. 오버레이 — 전부 유지
| 오버레이 | 비고 |
|---------|------|
| 거래 (은행+플레이어) | 기존 기능 그대로 |
| 자원 선택 | 풍년/독점용 |
| 약탈 | 도적 → 플레이어 선택 |
| 자원 버리기 | 7 나왔을 때 |
| 규칙 | 정적 텍스트 |
| 음량 | BGM/SFX 슬라이더 |
| 옵션 | 규칙/기권/메인메뉴 |
| 턴 순서 | 게임 시작 시 |
| 수신 거래 | AI/상대 제안 수락/거절 |
| 결과 | 게임 종료 순위 |

---

## 작업 단계

### Phase 0 — 기반 세팅
- [ ] Canvas Scaler 설정 (Scale With Screen Size, 1920x1080, Match 0.5)
- [ ] EventSystem 확인/추가
- [ ] 공통 색상/스타일 ScriptableObject 생성 (다크 테마 팔레트)
- [ ] TextMeshPro FontAsset 설정 (NotoSansKR + WOODSTAMP)
- [ ] 공통 프리팹 제작: 버튼(4종), 오버레이 배경, 패널 베이스

### Phase 1 — GameHUD 프리팹 구축
- [ ] **1-a**: 유틸 버튼 영역 (좌상단) + BGM 라벨
- [ ] **1-b**: 상대방 카드 영역 (우상단) + 은행 카드
- [ ] **1-c**: 이벤트 로그 (좌측)
- [ ] **1-d**: 액션 패널 (우측 하단) — 주사위 + 버튼들
- [ ] **1-e**: 하단 내 정보 카드 (상대카드 스타일)
- [ ] **1-f**: 토스트 컨테이너 (정중앙)

### Phase 2 — GameHUD 오버레이 프리팹
- [ ] **2-a**: 거래 오버레이 (은행 + 플레이어 거래)
- [ ] **2-b**: 자원 선택 / 약탈 / 자원 버리기
- [ ] **2-c**: 규칙 / 음량 / 옵션
- [ ] **2-d**: 턴 순서 / 수신 거래 / 결과

### Phase 3 — GameHUDController 전환
- [ ] UIDocument → Canvas 참조 전환
- [ ] `Q<T>(name)` → `[SerializeField]` 바인딩 전환
- [ ] `VisualElement` → `GameObject`/`RectTransform` 전환
- [ ] `display: none/flex` → `SetActive(true/false)` 전환
- [ ] USS 클래스 토글 → `Image.color` / `CanvasGroup` 전환
- [ ] 이벤트 구독/해제 로직 유지 (OnEnable/OnDisable)
- [ ] 상대카드 동적 생성 → Prefab Instantiate 방식
- [ ] 턴 전환 하이라이트 + 토스트 추가
- [ ] 컨트롤러 분리 검토 (2661줄 → 기능별 분리)

### Phase 4 — MainMenu / Lobby 재구축
- [ ] MainMenu: Canvas + 탭 UI (단순)
- [ ] Lobby: Canvas + 4 슬롯 (단순)
- [ ] SceneFlowManager 연동 확인

### Phase 5 — 정리
- [ ] 기존 UXML / USS 파일 제거
- [ ] UIDocument 컴포넌트 제거
- [ ] 전체 플레이 테스트
- [ ] DOTween/Feel 연동 확인

---

## 컨트롤러 분리 방안

현재 `GameHUDController.cs` (2661줄)을 기능별로 분리:

| 컨트롤러 | 담당 | 예상 규모 |
|---------|------|----------|
| `GameHUDManager` | 전체 조율, IGameManager 이벤트 라우팅 | ~200줄 |
| `ActionPanelController` | 주사위/건설/거래 버튼 + 페이즈 전환 | ~400줄 |
| `OpponentBarController` | 상대카드 생성/갱신 + 턴 하이라이트 | ~200줄 |
| `PlayerInfoController` | 하단 내 정보 (VP/도로/기사/자원) | ~150줄 |
| `TradeOverlayController` | 거래 오버레이 (은행+플레이어) | ~400줄 |
| `OverlayManager` | 나머지 오버레이 9개 관리 | ~300줄 |
| `EventLogController` | 이벤트 로그 + 토스트 | ~250줄 |
| `DiceDisplayController` | 주사위 pip 표시 | ~100줄 |

---

## 기술 매핑 참조

| UI Toolkit | UGUI |
|------------|------|
| `VisualElement` | `RectTransform` (GameObject) |
| `Label` | `TextMeshProUGUI` |
| `Button` | `Button` + `TMP_Text` |
| `TextField` | `TMP_InputField` |
| `ScrollView` | `ScrollRect` + Content + Scrollbar |
| `Slider` | `Slider` |
| `DropdownField` | `TMP_Dropdown` |
| `Image` (USS) | `Image` (UnityEngine.UI) |
| `Q<T>(name)` | `[SerializeField]` 직접 바인딩 |
| `display: none` | `gameObject.SetActive(false)` |
| `AddToClassList` | `Image.color` 변경 or Component 토글 |
| `picking-mode: ignore` | `CanvasGroup.blocksRaycasts = false` |
| USS transition | DOTween 애니메이션 |

---

## 주의사항

- **DOTween/Feel**: UGUI는 Transform 기반이라 DOTween과 더 자연스럽게 호환
- **Canvas 순서**: 핸드카드 Canvas와 HUD Canvas의 Sort Order 관리
- **해상도**: Canvas Scaler (1920x1080 기준, 16:9~19.5:9 대응)
- **EventSystem**: 씬당 1개 필수
- **.meta 파일**: 프리팹 생성 시 meta 파일 커밋 포함
