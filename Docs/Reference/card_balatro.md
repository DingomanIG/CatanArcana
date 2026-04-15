---
tags:
  - type/레퍼런스
  - domain/카드
  - status/완료
created: 2026-03-01
updated: 2026-04-15
---
# Balatro 스타일 카드 핸드 — 구현 레퍼런스

> Mix and Jam 채널 구현 방식 기반. 참고 레포: https://github.com/mixandjam/balatro-feel

---

## 핵심 아키텍처

### 1. Base Card / Card Visual 분리

| 레이어 | 역할 | 비고 |
|--------|------|------|
| **Base Card** | UI Canvas 위 Selectable, 마우스 인터랙션 처리 | IPointerEnter, IPointerDown, IDrag 등 |
| **Card Visual** | 별도 3D 프리팹, 매 프레임 Base Card 위치를 Lerp로 추적 | 로직은 즉각, 비주얼은 부드럽게 |

### 2. 슬롯 기반 카드 배치

- `PlayingCardGroup` 부모 컨테이너에 `HorizontalLayoutGroup` 사용
- 각 `CardSlot`이 자식으로 존재, 카드는 슬롯 안에 배치
- 드래그 시 x좌표를 인접 카드와 비교 → 자동 스왑 (sibling index 교환)
- 놓으면 원래 슬롯 위치로 복귀

### 3. 부채꼴(Hand Curve) 배열

- `AnimationCurve`로 각 카드의 Y 위치 오프셋 + Z 회전 오프셋 결정
- 카드 인덱스를 0~1로 정규화 → `curve.Evaluate()`로 값 산출
- 양쪽 끝 카드: 아래로 처지고 약간 기울어짐
- 가운데 카드: 높고 수직에 가까움

### 4. 선택 / 호버 인터랙션

| 상태 | 동작 |
|------|------|
| 호버 | Selection Offset만큼 위로 올라감 |
| 드래그 | 마우스 추적 + 그림자(shadow image) Y 오프셋으로 깊이감 |
| 애니메이션 | DOTween — shake on hover, position punch on selection |

### 5. 3D 회전 효과

| 상태 | 기법 |
|------|------|
| **Idle** | `sin(time)` X축 + `cos(time)` Y축 → 원형 경로 미세 흔들림 |
| **Hover** | 마우스↔카드 중심 거리로 X/Y 회전값 결정 → 마우스 방향으로 기울어짐 |

> 회전은 비주얼 오브젝트의 별도 자식에 적용 (부모 transform과 간섭 방지)

---

## 기술 스택

- Unity UI (Canvas + Selectable)
- HorizontalLayoutGroup (슬롯 자동 정렬)
- DOTween (보간 애니메이션)
- AnimationCurve (부채꼴 커브)
- Vector3.Lerp / Quaternion 회전

---

## 구현 순서

1. Canvas에 HorizontalLayoutGroup 컨테이너 + CardSlot 프리팹 생성
2. Card 스크립트: 마우스 인터페이스 구현, 드래그/스왑 로직
3. CardVisual 스크립트: Lerp 추적, idle/hover 회전, DOTween 이벤트 애니메이션
4. Hand Curve: AnimationCurve로 Y오프셋/Z회전 적용
5. (선택) 셰이더/폴리시 — polychrome twirl 효과 등

---

## 관련 문서
- [[CARD_HAND_SYSTEM]] — 카드 핸드 시스템 설계
