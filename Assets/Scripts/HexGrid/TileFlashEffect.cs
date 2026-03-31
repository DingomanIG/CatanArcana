using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;

/// <summary>
/// 주사위 결과에 해당하는 타일을 흰색으로 플래시 + 스케일 펀치
/// Feel 플러그인의 MMF_Player + MMF_Scale 사용
/// </summary>
public class TileFlashEffect : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] HexGridView hexGridView;

    [Header("플래시 설정")]
    [SerializeField] float flashDuration = 0.4f;
    [SerializeField] AnimationCurve flashCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("스케일 펀치 설정")]
    [SerializeField] float punchScale = 1.15f;
    [SerializeField] float punchDuration = 0.3f;

    IGameManager gameManager;
    Dictionary<HexCoord, MMF_Player> tilePlayers = new();

    void Start()
    {
        if (hexGridView == null)
            hexGridView = FindAnyObjectByType<HexGridView>();

        gameManager = GameServices.GameManager;
        if (gameManager != null)
            gameManager.OnDiceRolled += HandleDiceRolled;
    }

    void OnDestroy()
    {
        if (gameManager != null)
            gameManager.OnDiceRolled -= HandleDiceRolled;
    }

    void HandleDiceRolled(int die1, int die2, int total)
    {
        if (total == 7) return; // 7은 도적 → 플래시 없음

        var grid = hexGridView.Grid;
        var matchingTiles = grid.GetTilesWithNumber(total);

        foreach (var tile in matchingTiles)
        {
            if (!tile.ProducesResource) continue;
            if (tile.HasRobber) continue;

            var tileGo = hexGridView.GetTileGameObject(tile.Coord);
            if (tileGo == null) continue;

            // 색상 플래시 (코루틴)
            StartCoroutine(FlashTile(tileGo));

            // Feel 스케일 펀치
            PlayScalePunch(tile.Coord, tileGo);
        }
    }

    IEnumerator FlashTile(GameObject tileGo)
    {
        var mr = tileGo.GetComponent<MeshRenderer>();
        if (mr == null) yield break;

        var mat = mr.material;
        Color originalColor = mat.color;

        float elapsed = 0f;
        while (elapsed < flashDuration)
        {
            elapsed += Time.deltaTime;
            float t = flashCurve.Evaluate(elapsed / flashDuration);
            mat.color = Color.Lerp(originalColor, Color.white, t);
            yield return null;
        }

        mat.color = originalColor;
    }

    void PlayScalePunch(HexCoord coord, GameObject tileGo)
    {
        if (!tilePlayers.TryGetValue(coord, out var player))
        {
            player = tileGo.AddComponent<MMF_Player>();
            player.InitializationMode = MMFeedbacks.InitializationModes.Script;

            var scaleFeedback = (MMF_Scale)player.AddFeedback(typeof(MMF_Scale));
            scaleFeedback.AnimateScaleTarget = tileGo.transform;
            scaleFeedback.Mode = MMF_Scale.Modes.Absolute;
            scaleFeedback.AnimateScaleDuration = punchDuration;
            scaleFeedback.RemapCurveZero = 1f;
            scaleFeedback.RemapCurveOne = punchScale;
            scaleFeedback.AllowAdditivePlays = true;

            // 펀치 커브: 빠르게 확대 → 천천히 복귀
            var punchCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.25f, 1f),
                new Keyframe(1f, 0f)
            );
            scaleFeedback.AnimateScaleTweenX = new MMTweenType(punchCurve);
            scaleFeedback.AnimateScaleTweenY = new MMTweenType(punchCurve);
            scaleFeedback.AnimateScaleTweenZ = new MMTweenType(punchCurve);

            player.Initialization();
            tilePlayers[coord] = player;
        }

        player.PlayFeedbacks();
    }
}
