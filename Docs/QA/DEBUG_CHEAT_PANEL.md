# Debug Cheat Panel 사용법

> **토글**: `F9` | **조건**: `UNITY_EDITOR` 또는 `DEVELOPMENT_BUILD`에서만 활성화  
> **위치**: GameHUDController가 Start()에서 자동 부착

---

## 기능 요약

| 섹션 | 기능 | 비고 |
|------|------|------|
| **Target Player** | P0~P3 선택 | `*` = 로컬 플레이어 |
| **Resources** | 자원 +/- 추가, MAX(19), CLR(초기화) | 5종 개별 선택 |
| **Dev Cards** | Knight/VP/RoadBuilding/YearOfPlenty/Monopoly 추가 | 즉시 사용 가능 (turnNumber=-1) |
| **Building Stock** | 도로/정착지/도시 남은 수 직접 설정 | 0으로 설정 → 소진 테스트 |
| **Robber** | Q, R 좌표 입력 → 즉시 이동 | |
| **Game Control** | 페이즈 강제 변경, 턴 플레이어 변경, 강제 승리 | |

---

## 테스트 시나리오별 사용법

### M. 발전카드 엣지케이스

**M1 — 도로건설 카드 1개만 놓고 취소**
1. Dev Cards → `RoadBuilding` 선택 → `+ Add` 클릭
2. 카드 사용 → 도로 1개 배치 → 취소 시도
3. 확인: 도로 1개만 배치됐는지, 카드 소모됐는지

**M2 — 풍년 카드, 은행 자원 부족**
1. 모든 플레이어에게 MAX 자원 부여 (은행 고갈)
2. Dev Cards → `YearOfPlenty` 추가 → 사용
3. 확인: 은행에 해당 자원 없을 때 처리

**M3 — 독점 카드, 아무도 해당 자원 없을 때**
1. 모든 플레이어 자원 CLR
2. Dev Cards → `Monopoly` 추가 → 사용
3. 확인: 카드만 소모되고 에러 없는지

**M4 — 턴당 1장 제한**
1. Dev Cards → Knight 2장 추가
2. 1장 사용 후 2장째 사용 시도
3. 확인: `HasUsedDevCardThisTurn` 체크로 거부되는지

### N. 건물 재고 한도

**N1 — 도로 15개 소진**
1. Building Stock → Roads: `0`, Settle/City 그대로 → `Set Building Stock`
2. 자원 충분히 추가 (Wood + Brick)
3. 도로 건설 시도
4. 확인: 거부되는지

**N2 — 정착지 5개 소진 + 도시 업그레이드 회수**
1. Building Stock → Settlements: `0`, Cities: `4` → Set
2. 도시 업그레이드 실행 (Wheat 2 + Ore 3 필요)
3. 확인: 업그레이드 후 SettlementsRemaining이 1로 복구되는지

**N3 — 도시 4개 소진**
1. Building Stock → Cities: `0` → Set
2. 자원 추가 (Wheat 2 + Ore 3)
3. 도시 업그레이드 시도
4. 확인: 거부되는지

---

## 네트워크 모드 참고

- 치트는 **ServerRpc** 경유 → 호스트 LGM에서 실행 → **ClientRpc**로 동기화
- ParrelSync 클론에서도 F9 패널 사용 가능하지만, 실제 조작은 호스트에서 처리됨
- 클라이언트에서 치트 실행 시 호스트 측 상태가 변경되고 양쪽에 반영됨
