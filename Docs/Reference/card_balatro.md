유니티에서 Balatro 스타일의 카드 핸드(손패) 배열 시스템을 구현해줘.
"Mix and Jam" 채널의 구현 방식을 기반으로 아래 구조와 기법을 적용해야 해.

참고 레포: https://github.com/mixandjam/balatro-feel

## 핵심 아키텍처

1. **Base Card (로직) / Card Visual (비주얼) 분리 구조**
   - Base Card: UI Canvas 위의 Selectable 컴포넌트가 붙은 2D 오브젝트. 마우스 인터랙션(IPointerEnter, IPointerDown, IDrag 등) 처리 담당
   - Card Visual: 별도의 3D 프리팹. 매 프레임 Base Card의 위치를 Vector3.Lerp로 부드럽게 따라감
   - 이 분리 덕분에 로직은 즉각 반응하되, 비주얼은 부드러운 보간 이동 가능

2. **슬롯 기반 카드 배치**
   - PlayingCardGroup이라는 부모 컨테이너에 HorizontalLayoutGroup 사용
   - 각 CardSlot이 자식으로 존재하고, 카드는 슬롯 안에 배치
   - 카드 드래그 시 x좌표를 인접 카드와 비교하여 자동 스왑 (sibling index 교환)
   - 놓으면 원래 슬롯 위치로 복귀

3. **부채꼴(Hand Curve) 배열**
   - AnimationCurve를 사용하여 각 카드의 Y 위치 오프셋과 Z 회전 오프셋을 결정
   - 카드 인덱스를 0~1로 정규화하여 curve.Evaluate()로 값 산출
   - 양쪽 끝 카드는 아래로 처지고 약간 기울어지며, 가운데 카드는 높고 수직에 가까움

4. **선택/호버 인터랙션**
   - 호버 시: 카드가 Selection Offset만큼 위로 올라감
   - 드래그 시: 마우스를 따라다니되, 그림자(shadow image)가 Y 오프셋으로 깊이감 표현
   - 카드 비주얼은 Base Card의 이벤트를 리스닝하여 DOTween으로 애니메이션 (shake on hover, position punch on selection 등)

5. **3D 회전 효과 (Rotation Details)**
   - Idle 상태: sin(time)으로 X축, cos(time)으로 Y축 회전 → 원형 경로로 미세하게 흔들림
   - Hover 상태: 마우스와 카드 중심 간 거리로 X/Y 회전값 결정 → 카드가 마우스 방향으로 기울어짐
   - 회전은 비주얼 오브젝트의 별도 자식에 적용하여 부모 transform과 간섭 방지

## 기술 스택
- Unity UI (Canvas + Selectable)
- HorizontalLayoutGroup (슬롯 자동 정렬)
- DOTween (보간 애니메이션)
- AnimationCurve (부채꼴 커브)
- Vector3.Lerp / Quaternion 회전

## 구현 순서
1. Canvas에 HorizontalLayoutGroup 컨테이너 + CardSlot 프리팹 생성
2. Card 스크립트: 마우스 인터페이스 구현, 드래그/스왑 로직
3. CardVisual 스크립트: Lerp 추적, idle/hover 회전, DOTween 이벤트 애니메이션
4. Hand Curve: AnimationCurve로 Y오프셋/Z회전 적용
5. 셰이더/폴리시는 선택사항 (polychrome twirl 효과 등)