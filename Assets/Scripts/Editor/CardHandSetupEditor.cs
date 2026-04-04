using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;
using UnityEditor;

namespace ArcanaCatan.Editor
{
    /// <summary>
    /// Balatro 스타일 카드 핸드 프리팹 + 씬 오브젝트 자동 생성.
    /// Menu: ArcanaCatan > Setup Card Hand
    /// </summary>
    public static class CardHandSetupEditor
    {
        private static readonly Color CardBgColor = new Color(0.15f, 0.15f, 0.2f, 1f);
        private static readonly Color CardBorderColor = new Color(0.9f, 0.75f, 0.3f, 1f);
        private static readonly Color ShadowColor = new Color(0, 0, 0, 0.4f);
        private static readonly Color TextColor = new Color(0.95f, 0.95f, 0.9f, 1f);

        [MenuItem("ArcanaCatan/Setup Card Hand")]
        public static void SetupCardHand()
        {
            // 1. 프리팹 생성
            string prefabPath = "Assets/Prefabs/UI/CardPrefab.prefab";
            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existingPrefab != null)
            {
                if (!EditorUtility.DisplayDialog("카드 프리팹 덮어쓰기",
                    $"{prefabPath} 가 이미 존재합니다.\n덮어쓰시겠습니까?", "덮어쓰기", "취소"))
                    return;
            }

            // 프리팹 폴더 확인
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI"))
                AssetDatabase.CreateFolder("Assets/Prefabs", "UI");

            // 2. Card 프리팹 조립
            GameObject cardPrefab = CreateCardPrefab();

            // 3. 프리팹 저장
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(cardPrefab, prefabPath);
            Object.DestroyImmediate(cardPrefab);

            // 4. 씬에 CardHand 셋업
            SetupSceneObjects(savedPrefab);

            Debug.Log("[CardHandSetup] 완료! Card 프리팹 + 씬 오브젝트 생성됨.");
            EditorUtility.DisplayDialog("Setup Complete",
                "카드 핸드 셋업 완료!\n\n" +
                "• 프리팹: Assets/Prefabs/UI/CardPrefab.prefab\n" +
                "• 씬: CardHandCanvas 오브젝트 생성됨\n\n" +
                "테스트: Play 후 Space로 카드 추가", "확인");
        }

        private static GameObject CreateCardPrefab()
        {
            // Root: CardSlot (BaseCard 붙는 곳)
            GameObject root = new GameObject("CardSlot");
            RectTransform rootRT = root.AddComponent<RectTransform>();
            rootRT.sizeDelta = new Vector2(120, 180);
            root.AddComponent<CanvasGroup>();

            // Raycast 전용 투명 Image (이벤트 수신용 — 이것만 raycastTarget)
            Image raycastImage = root.AddComponent<Image>();
            raycastImage.color = Color.clear;
            raycastImage.raycastTarget = true;

            // BaseCard 컴포넌트
            var baseCard = root.AddComponent<UI.CardHand.BaseCard>();

            // VisualContainer (회전 효과용 자식)
            GameObject visualContainer = new GameObject("VisualContainer");
            visualContainer.transform.SetParent(root.transform, false);
            RectTransform vcRT = visualContainer.AddComponent<RectTransform>();
            vcRT.anchorMin = Vector2.zero;
            vcRT.anchorMax = Vector2.one;
            vcRT.sizeDelta = Vector2.zero;
            vcRT.offsetMin = Vector2.zero;
            vcRT.offsetMax = Vector2.zero;

            // Card Border (테두리 — 골드)
            GameObject border = CreateImageChild(visualContainer, "Border",
                new Vector2(124, 184), CardBorderColor);

            // Card Background (어두운 배경)
            GameObject bg = CreateImageChild(visualContainer, "Background",
                new Vector2(116, 176), CardBgColor);

            // Card Icon Area (카드 타입 아이콘 영역)
            GameObject iconArea = CreateImageChild(visualContainer, "IconArea",
                new Vector2(100, 90), new Color(0.2f, 0.2f, 0.28f, 1f));
            iconArea.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 20f);

            // Card Title Text
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(visualContainer.transform, false);
            RectTransform titleRT = titleObj.AddComponent<RectTransform>();
            titleRT.sizeDelta = new Vector2(100, 30);
            titleRT.anchoredPosition = new Vector2(0, -50f);
            Text titleText = titleObj.AddComponent<Text>();
            titleText.text = "CARD";
            titleText.fontSize = 14;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = TextColor;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.raycastTarget = false;

            // Card Type Text (작은 서브텍스트)
            GameObject typeObj = new GameObject("TypeText");
            typeObj.transform.SetParent(visualContainer.transform, false);
            RectTransform typeRT = typeObj.AddComponent<RectTransform>();
            typeRT.sizeDelta = new Vector2(100, 20);
            typeRT.anchoredPosition = new Vector2(0, -70f);
            Text typeText = typeObj.AddComponent<Text>();
            typeText.text = "Knight";
            typeText.fontSize = 11;
            typeText.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            typeText.alignment = TextAnchor.MiddleCenter;
            typeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            typeText.raycastTarget = false;

            // Shadow Image
            GameObject shadow = CreateImageChild(root, "Shadow",
                new Vector2(120, 180), ShadowColor);
            shadow.GetComponent<RectTransform>().anchoredPosition = new Vector2(5, -10);
            shadow.transform.SetAsFirstSibling(); // 가장 뒤에 렌더링

            // CardVisual 컴포넌트
            var cardVisual = root.AddComponent<UI.CardHand.CardVisual>();

            // SerializedObject로 private 필드 할당
            SerializedObject so = new SerializedObject(cardVisual);
            so.FindProperty("baseCard").objectReferenceValue = baseCard;
            so.FindProperty("cardImage").objectReferenceValue = bg.GetComponent<Image>();
            so.FindProperty("shadowImage").objectReferenceValue = shadow.GetComponent<Image>();
            so.FindProperty("visualContainer").objectReferenceValue = vcRT;
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        private static GameObject CreateImageChild(GameObject parent, string name,
            Vector2 size, Color color)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent.transform, false);
            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.sizeDelta = size;
            Image img = obj.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false; // 루트의 투명 Image만 raycast 받음
            return obj;
        }

        private static void SetupSceneObjects(GameObject cardPrefab)
        {
            // 기존 CardHandCanvas 체크
            var existing = GameObject.Find("CardHandCanvas");
            if (existing != null)
            {
                if (EditorUtility.DisplayDialog("씬 오브젝트 덮어쓰기",
                    "CardHandCanvas가 이미 존재합니다.\n삭제하고 새로 생성할까요?",
                    "새로 생성", "유지"))
                {
                    Undo.DestroyObjectImmediate(existing);
                }
                else return;
            }

            // Canvas 생성
            GameObject canvasObj = new GameObject("CardHandCanvas");
            Undo.RegisterCreatedObjectUndo(canvasObj, "Create CardHandCanvas");

            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10; // HUD 위에 렌더링

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            // CardHand 컨테이너
            GameObject handObj = new GameObject("CardHand");
            handObj.transform.SetParent(canvasObj.transform, false);
            RectTransform handRT = handObj.AddComponent<RectTransform>();

            // 하단 중앙에 배치
            handRT.anchorMin = new Vector2(0.5f, 0f);
            handRT.anchorMax = new Vector2(0.5f, 0f);
            handRT.pivot = new Vector2(0.5f, 0f);
            handRT.anchoredPosition = new Vector2(0, 30f);
            handRT.sizeDelta = new Vector2(800, 200);

            // HorizontalLayoutGroup
            HorizontalLayoutGroup hlg = handObj.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = -30f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;

            // ContentSizeFitter (카드 수에 따라 컨테이너 크기 조절)
            ContentSizeFitter csf = handObj.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            // CardHandManager
            var manager = handObj.AddComponent<UI.CardHand.CardHandManager>();
            SerializedObject managerSO = new SerializedObject(manager);
            managerSO.FindProperty("cardPrefab").objectReferenceValue = cardPrefab;
            managerSO.FindProperty("cardContainer").objectReferenceValue = handRT;
            managerSO.FindProperty("layoutGroup").objectReferenceValue = hlg;
            managerSO.ApplyModifiedPropertiesWithoutUndo();

            // CardHandTester
            var tester = handObj.AddComponent<UI.CardHand.CardHandTester>();
            SerializedObject testerSO = new SerializedObject(tester);
            testerSO.FindProperty("handManager").objectReferenceValue = manager;
            testerSO.ApplyModifiedPropertiesWithoutUndo();

            // EventSystem 체크
            if (GameObject.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.transform.SetParent(canvasObj.transform);
                eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystem.AddComponent<InputSystemUIInputModule>();
                Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
            }

            Selection.activeGameObject = handObj;
        }
    }
}
