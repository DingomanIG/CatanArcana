#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 디버그 치트 패널 — F9로 토글, 호스트 전용
/// M(발전카드 엣지케이스), N(건물 재고 한도) 등 테스트용
/// </summary>
public class DebugCheatPanel : MonoBehaviour
{
    bool showPanel;
    Vector2 scrollPos;
    Rect windowRect = new(10, 10, 340, 520);

    // 선택 상태
    int selectedPlayer;
    int selectedResource; // 0~4: Wood,Brick,Wool,Wheat,Ore
    int resourceAmount = 5;
    int selectedCardType; // 0~4: Knight,VP,RoadBuilding,YearOfPlenty,Monopoly
    int cheatRoads = 2;
    int cheatSettlements = 1;
    int cheatCities = 1;
    int robberQ, robberR;
    int selectedPhase;

    static readonly string[] resourceNames = { "Wood", "Brick", "Wool", "Wheat", "Ore" };
    static readonly string[] cardNames = { "Knight", "VP", "RoadBuilding", "YearOfPlenty", "Monopoly" };
    static readonly string[] phaseNames = { "WaitingForPlayers", "InitialPlacement", "RollDice", "Action", "MoveRobber", "StealResource", "GameOver" };

    IGameManager GM => GameServices.GameManager;

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame)
            showPanel = !showPanel;
    }

    void OnGUI()
    {
        if (!showPanel || GM == null) return;

        windowRect = GUI.Window(9999, windowRect, DrawWindow, "Debug Cheat Panel (F9)");
    }

    void DrawWindow(int id)
    {
        scrollPos = GUILayout.BeginScrollView(scrollPos);

        // 상태 표시
        GUILayout.Label($"<b>Turn {GM.TurnNumber} | P{GM.CurrentPlayerIndex} | {GM.CurrentPhase}</b>");
        GUILayout.Label($"Local: P{GM.LocalPlayerIndex} | Host: {GM.IsHost}");
        GUILayout.Space(4);

        // 플레이어 선택
        GUILayout.Label("<b>--- Target Player ---</b>");
        GUILayout.BeginHorizontal();
        for (int i = 0; i < GM.PlayerCount; i++)
        {
            string label = $"P{i}{(i == GM.LocalPlayerIndex ? "*" : "")}";
            if (GUILayout.Toggle(selectedPlayer == i, label, "Button", GUILayout.Width(60)))
                selectedPlayer = i;
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(8);

        // === 자원 조작 ===
        GUILayout.Label("<b>--- Resources ---</b>");
        DrawPlayerResources();
        GUILayout.BeginHorizontal();
        selectedResource = GUILayout.SelectionGrid(selectedResource, resourceNames, 5);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Amount:", GUILayout.Width(55));
        string amountStr = GUILayout.TextField(resourceAmount.ToString(), GUILayout.Width(40));
        int.TryParse(amountStr, out resourceAmount);
        if (GUILayout.Button($"+{resourceAmount}", GUILayout.Width(50)))
            CheatResource(resourceAmount);
        if (GUILayout.Button($"-{resourceAmount}", GUILayout.Width(50)))
            CheatResource(-resourceAmount);
        if (GUILayout.Button("MAX", GUILayout.Width(45)))
            CheatSetAllResources(19);
        if (GUILayout.Button("CLR", GUILayout.Width(40)))
            CheatClearResources();
        GUILayout.EndHorizontal();
        GUILayout.Space(8);

        // === 발전카드 ===
        GUILayout.Label("<b>--- Dev Cards ---</b>");
        DrawPlayerDevCards();
        selectedCardType = GUILayout.SelectionGrid(selectedCardType, cardNames, 5);
        if (GUILayout.Button($"+ Add {cardNames[selectedCardType]}"))
            CheatAddCard();
        GUILayout.Space(8);

        // === 건물 재고 ===
        GUILayout.Label("<b>--- Building Stock ---</b>");
        DrawBuildingStock();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Road:", GUILayout.Width(40));
        string rStr = GUILayout.TextField(cheatRoads.ToString(), GUILayout.Width(30));
        int.TryParse(rStr, out cheatRoads);
        GUILayout.Label("Settle:", GUILayout.Width(45));
        string sStr = GUILayout.TextField(cheatSettlements.ToString(), GUILayout.Width(30));
        int.TryParse(sStr, out cheatSettlements);
        GUILayout.Label("City:", GUILayout.Width(35));
        string cStr = GUILayout.TextField(cheatCities.ToString(), GUILayout.Width(30));
        int.TryParse(cStr, out cheatCities);
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Set Building Stock"))
            CheatSetStock();
        GUILayout.Space(8);

        // === 강도 이동 ===
        GUILayout.Label("<b>--- Robber ---</b>");
        GUILayout.BeginHorizontal();
        GUILayout.Label("Q:", GUILayout.Width(20));
        string qStr = GUILayout.TextField(robberQ.ToString(), GUILayout.Width(40));
        int.TryParse(qStr, out robberQ);
        GUILayout.Label("R:", GUILayout.Width(20));
        string rrStr = GUILayout.TextField(robberR.ToString(), GUILayout.Width(40));
        int.TryParse(rrStr, out robberR);
        if (GUILayout.Button("Move Robber"))
            CheatRobber();
        GUILayout.EndHorizontal();
        GUILayout.Space(8);

        // === 게임 컨트롤 ===
        GUILayout.Label("<b>--- Game Control ---</b>");
        selectedPhase = GUILayout.SelectionGrid(selectedPhase, phaseNames, 4);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Set Phase"))
            CheatPhase();
        if (GUILayout.Button($"Set Turn → P{selectedPlayer}"))
            CheatTurn();
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Force Victory"))
            CheatVictory();
        GUILayout.EndHorizontal();

        GUILayout.EndScrollView();
        GUI.DragWindow();
    }

    // === 상태 표시 헬퍼 ===

    void DrawPlayerResources()
    {
        var ps = GM.GetPlayerState(selectedPlayer);
        if (ps == null) return;
        GUILayout.BeginHorizontal();
        GUILayout.Label($"W:{ps.Resources[ResourceType.Wood]}", GUILayout.Width(40));
        GUILayout.Label($"B:{ps.Resources[ResourceType.Brick]}", GUILayout.Width(40));
        GUILayout.Label($"L:{ps.Resources[ResourceType.Wool]}", GUILayout.Width(40));
        GUILayout.Label($"G:{ps.Resources[ResourceType.Wheat]}", GUILayout.Width(40));
        GUILayout.Label($"O:{ps.Resources[ResourceType.Ore]}", GUILayout.Width(40));
        GUILayout.Label($"(Total:{ps.TotalResourceCount})");
        GUILayout.EndHorizontal();
    }

    void DrawPlayerDevCards()
    {
        var ps = GM.GetPlayerState(selectedPlayer);
        if (ps == null) return;
        int k = 0, v = 0, rb = 0, yp = 0, m = 0;
        foreach (var card in ps.DevCards)
        {
            if (card.IsUsed) continue;
            switch (card.Type)
            {
                case DevCardType.Knight: k++; break;
                case DevCardType.VictoryPoint: v++; break;
                case DevCardType.RoadBuilding: rb++; break;
                case DevCardType.YearOfPlenty: yp++; break;
                case DevCardType.Monopoly: m++; break;
            }
        }
        GUILayout.Label($"  Knight:{k} VP:{v} Road:{rb} Plenty:{yp} Monopoly:{m}");
    }

    void DrawBuildingStock()
    {
        var ps = GM.GetPlayerState(selectedPlayer);
        if (ps == null) return;
        GUILayout.Label($"  Roads:{ps.RoadsRemaining}/15  Settlements:{ps.SettlementsRemaining}/5  Cities:{ps.CitiesRemaining}/4");
    }

    // === 치트 실행 ===

    void CheatResource(int amount)
    {
        var rt = (ResourceType)(selectedResource + 1); // ResourceType: None=0, Wood=1, ...
        if (GM is NetworkGameManager ngm)
            ngm.CheatAddResourceServerRpc(selectedPlayer, selectedResource + 1, amount);
        else if (GM is LocalGameManager lgm)
            lgm.CheatAddResource(selectedPlayer, rt, amount);
    }

    void CheatSetAllResources(int amount)
    {
        for (int i = 0; i < 5; i++)
        {
            var rt = (ResourceType)(i + 1);
            if (GM is NetworkGameManager ngm)
                ngm.CheatAddResourceServerRpc(selectedPlayer, i + 1, amount);
            else if (GM is LocalGameManager lgm)
            {
                int current = lgm.GetPlayerState(selectedPlayer).Resources[rt];
                lgm.CheatAddResource(selectedPlayer, rt, amount - current);
            }
        }
    }

    void CheatClearResources()
    {
        for (int i = 0; i < 5; i++)
        {
            var rt = (ResourceType)(i + 1);
            if (GM is NetworkGameManager ngm)
                ngm.CheatAddResourceServerRpc(selectedPlayer, i + 1, -99);
            else if (GM is LocalGameManager lgm)
            {
                int current = lgm.GetPlayerState(selectedPlayer).Resources[rt];
                lgm.CheatAddResource(selectedPlayer, rt, -current);
            }
        }
    }

    void CheatAddCard()
    {
        var cardType = (DevCardType)selectedCardType;
        if (GM is NetworkGameManager ngm)
            ngm.CheatAddDevCardServerRpc(selectedPlayer, selectedCardType);
        else if (GM is LocalGameManager lgm)
            lgm.CheatAddDevCard(selectedPlayer, cardType);
    }

    void CheatSetStock()
    {
        if (GM is NetworkGameManager ngm)
            ngm.CheatSetBuildingStockServerRpc(selectedPlayer, cheatRoads, cheatSettlements, cheatCities);
        else if (GM is LocalGameManager lgm)
            lgm.CheatSetBuildingStock(selectedPlayer, cheatRoads, cheatSettlements, cheatCities);
    }

    void CheatRobber()
    {
        if (GM is NetworkGameManager ngm)
            ngm.CheatMoveRobberServerRpc(robberQ, robberR);
        else if (GM is LocalGameManager lgm)
            lgm.CheatMoveRobber(new HexCoord(robberQ, robberR));
    }

    void CheatPhase()
    {
        if (GM is NetworkGameManager ngm)
            ngm.CheatSetPhaseServerRpc(selectedPhase);
        else if (GM is LocalGameManager lgm)
            lgm.CheatSetPhase((GamePhase)selectedPhase);
    }

    void CheatTurn()
    {
        if (GM is NetworkGameManager ngm)
            ngm.CheatSetCurrentPlayerServerRpc(selectedPlayer);
        else if (GM is LocalGameManager lgm)
            lgm.CheatSetCurrentPlayer(selectedPlayer);
    }

    void CheatVictory()
    {
        if (GM is LocalGameManager lgm)
            lgm.CheatForceVictory();
        // 네트워크에서도 호스트의 LGM 호출
        else if (GM is NetworkGameManager ngm)
            ngm.CheatSetPhaseServerRpc((int)GamePhase.GameOver);
    }
}
#endif
