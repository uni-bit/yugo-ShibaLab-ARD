using System.Collections;
using UnityEngine;

/// <summary>
/// Stage 4 の自動演出シーケンス。
/// <para>
/// Stage 4 Root が有効化されると <see cref="holdBeforeReturnSeconds"/> 秒後に
/// <see cref="StageSequenceController.FadeToStageWithOptions"/> を呼び出し Stage 1 へ戻る。
/// </para>
/// </summary>
[AddComponentMenu("Stages/Stage 4 Sequence Controller")]
public class Stage4SequenceController : MonoBehaviour
{
    [SerializeField] private float holdBeforeReturnSeconds = 10f;
    [SerializeField] private float returnFadeOutDuration = 2.5f;
    [SerializeField] private float returnFadeInDuration = 2.5f;
    [SerializeField] private int returnStageIndex = 0;

    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            StartCoroutine(AutoReturnCoroutine());
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    private IEnumerator AutoReturnCoroutine()
    {
        yield return new WaitForSeconds(holdBeforeReturnSeconds);

        StageSequenceController controller = FindFirstObjectByType<StageSequenceController>();
        if (controller != null)
        {
            controller.FadeToStageWithOptions(returnStageIndex, returnFadeOutDuration, returnFadeInDuration, Color.black);
        }
    }
}
