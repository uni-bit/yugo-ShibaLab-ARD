using System.Collections;
using UnityEngine;

/// <summary>
/// Stage 4 の自動演出シーケンス。
/// <para>
/// Stage 4 Root が有効化されると <see cref="holdBeforeReturnSeconds"/> 秒後に
/// <see cref="StageSequenceController.FadeToStageWithOptions"/> を呼び出し Stage 2 へ戻る。
/// </para>
/// <para>
/// ステージ遷移フェードイン開始と同時にアンビエントライトを <see cref="stage4AmbientColor"/> へ
/// <see cref="ambientFadeDuration"/> 秒かけてフェードさせる。
/// </para>
/// </summary>
[AddComponentMenu("Stages/Stage 4 Sequence Controller")]
public class Stage4SequenceController : MonoBehaviour
{
    [SerializeField] private float holdBeforeReturnSeconds = 10f;
    [SerializeField] private float returnFadeOutDuration = 2.5f;
    [SerializeField] private float returnFadeInDuration = 2.5f;
    [SerializeField] private Color returnFadeColor = new Color(0.04f, 0.04f, 0.05f, 1f);
    [SerializeField] private Color stage4AmbientColor = new Color(0.85f, 0.82f, 0.72f, 1f);
    [SerializeField] private float ambientFadeDuration = 2.0f;
    // Stage 4 returns to Stage 2 (index 1) for looped playback.
    private const int returnStageIndex = 1;
    // Stage 4 is always index 3 in the StageSequenceController roots array.
    private const int stage4StageIndex = 3;

    private StageSequenceController cachedController;

    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            cachedController = FindFirstObjectByType<StageSequenceController>();
            StartCoroutine(FadeAmbientOnStageActiveCoroutine());
            StartCoroutine(AutoReturnCoroutine());
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    /// <summary>
    /// Stage4 が正式にアクティブステージになった（switch callback 完了、Stage3 の OnDisable 済み）後に
    /// アンビエントライトを stage4AmbientColor へフェードさせる。
    /// PreloadStageWhenOverlayOpaque による先行 OnEnable 時は CurrentStageIndex がまだ 2 のため、
    /// WaitUntil で switch 完了を待ってから開始することで Stage3.OnDisable の上書きを回避する。
    /// </summary>
    private IEnumerator FadeAmbientOnStageActiveCoroutine()
    {
        if (cachedController != null)
        {
            yield return new WaitUntil(() =>
                cachedController == null ||
                cachedController.CurrentStageIndex == stage4StageIndex);
        }

        Color startColor = RenderSettings.ambientLight;
        float elapsed = 0f;
        while (elapsed < ambientFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, ambientFadeDuration));
            RenderSettings.ambientLight = Color.Lerp(startColor, stage4AmbientColor, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        RenderSettings.ambientLight = stage4AmbientColor;
    }

    private IEnumerator AutoReturnCoroutine()
    {
        yield return new WaitForSeconds(holdBeforeReturnSeconds);

        StageSequenceController controller = cachedController != null
            ? cachedController
            : FindFirstObjectByType<StageSequenceController>();
        if (controller != null)
        {
            controller.FadeToStageWithOptions(returnStageIndex, returnFadeOutDuration, returnFadeInDuration, Color.black, false);
        }
    }
}
