// [LEGACY] このスクリプトはクリック操作ベースの旧 Stage1 実装です。
// 現行の Stage 1 ギミックは Assets/Scripts/Stages/StageLightOrderedPuzzle.cs を使用してください。
// このファイルは参照用として残してありますが、通常は使用しません。
using UnityEngine;
using System.Collections;

[AddComponentMenu("_Legacy/Stage1Manager (Deprecated)")]
public class Stage1Manager : MonoBehaviour
{
    [Header("生き物オブジェクト")]
    public GameObject fishIcon;
    public GameObject rabbitIcon;
    public GameObject deerIcon;

    [Header("動く木（門）")]
    public GameObject leftTree;
    public GameObject rightTree;
    public GameObject winEffectPrefab;

    [Header("演出・ルール設定")]
    public float jumpHeight = 2.0f;
    public float jumpDuration = 0.4f;
    public float openDuration = 5.0f;
    public float resetThreshold = 10.0f;
    public AudioClip surprisedSound;
    public AudioClip clearSound;

    int progress = 0;
    bool isCleared = false;
    float lastInputTime;
    AudioSource audioSource;
    Vector3 fishStartPos, rabbitStartPos, deerStartPos;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        lastInputTime = Time.time;
        if (fishIcon) fishStartPos = fishIcon.transform.position;
        if (rabbitIcon) rabbitStartPos = rabbitIcon.transform.position;
        if (deerIcon) deerStartPos = deerIcon.transform.position;
    }

    void Update()
    {
        if (isCleared) return;
        if (progress > 0 && Time.time - lastInputTime > resetThreshold) ResetProgress("タイムアウト");

        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit)) OnLightHit(hit.collider.gameObject.name);
        }
    }

    public void OnLightHit(string animalId)
    {
        if (isCleared) return;
        lastInputTime = Time.time;

        if (animalId == "fish") StartCoroutine(FishHideAction());
        if (animalId == "rabbit") StartCoroutine(RabbitJumpAction());
        if (animalId == "deer") StartCoroutine(DeerSurpriseAction());

        CheckSequence(animalId);
    }

    // --- 【演出】ウサギ：ぴょんぴょん跳ねる ---
    IEnumerator RabbitJumpAction()
    {
        for (int i = 0; i < 2; i++)
        {   if (surprisedSound) audioSource.PlayOneShot(surprisedSound);
            float elapsed = 0;
            while (elapsed < jumpDuration)
            {
                float t = elapsed / jumpDuration;
                float y = 4 * jumpHeight * t * (1 - t);
                rabbitIcon.transform.position = rabbitStartPos + Vector3.up * y;
                elapsed += Time.deltaTime;
                yield return null;
            }
            rabbitIcon.transform.position = rabbitStartPos;
            yield return new WaitForSeconds(0.05f);
        }
    }

    // --- 【演出】魚：ゆっくり沈む ---
    IEnumerator FishHideAction()
    {
        if (surprisedSound) audioSource.PlayOneShot(surprisedSound);
        float elapsed = 0;
        float slowDuration = 2.0f;
        Vector3 targetPos = fishStartPos + Vector3.down * 2.5f;
        while (elapsed < slowDuration)
        {
            fishIcon.transform.position = Vector3.Lerp(fishStartPos, targetPos, elapsed / slowDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    // --- 【演出】シカ：驚く ---
    IEnumerator DeerSurpriseAction()
    {
        if (surprisedSound) audioSource.PlayOneShot(surprisedSound);
        Vector3 startScale = deerIcon.transform.localScale;
        float elapsed = 0;
        float duration = 0.5f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float scalePop = 1.0f + Mathf.Sin(t * Mathf.PI) * 0.3f;
            deerIcon.transform.localScale = startScale * scalePop;
            deerIcon.transform.position = deerStartPos + new Vector3(Mathf.Sin(Time.time * 60) * 0.15f, 0, 0);
            elapsed += Time.deltaTime;
            yield return null;
        }
        deerIcon.transform.localScale = startScale;
        deerIcon.transform.position = deerStartPos;
    }

    void CheckSequence(string id)
    {
        if (id == "fish" && progress == 0)
        {
            progress = 1;
            SetIconColor(fishIcon, Color.yellow);
        }
        else if (id == "rabbit" && progress == 1)
        {
            progress = 2;
            SetIconColor(rabbitIcon, Color.yellow);
        }
        else if (id == "deer" && progress == 2)
        {
            progress = 3;
            isCleared = true;
            SetIconColor(deerIcon, Color.yellow);
            if (clearSound) audioSource.PlayOneShot(clearSound);
            StartCoroutine(OpenPathAnimation());
        }
        else
        {
            // ★演出が終わるまで待ってからリセットする
            float waitTime = 0.5f; // デフォルト
            if (id == "fish") waitTime = 2.0f; // 魚は2秒待つ
            else if (id == "rabbit") waitTime = (jumpDuration * 2) + 0.1f; // ウサギは2回飛ぶ時間待つ
            else if (id == "deer") waitTime = 0.5f; // シカは0.5秒待つ

            StartCoroutine(WaitAndReset(waitTime, "順番が違うよ！"));
        }
    }

    // ちょっと待ってからリセット
    IEnumerator WaitAndReset(float duration, string message)
    {
        yield return new WaitForSeconds(duration); // 指定された時間（演出分）だけ待機
        ResetProgress(message);
    }

    void ResetProgress(string message)
    {
        // 今動いている全ての演出（Coroutine）を強制終了する
        StopAllCoroutines();

        progress = 0;
        // 改めて元の位置と色に戻す
        fishIcon.transform.position = fishStartPos;
        rabbitIcon.transform.position = rabbitStartPos;
        deerIcon.transform.position = deerStartPos;
        // 大きさも念のため戻す
        deerIcon.transform.localScale = Vector3.one;

        SetIconColor(fishIcon, Color.white);
        SetIconColor(rabbitIcon, Color.white);
        SetIconColor(deerIcon, Color.white);
        Debug.Log(message);
    }

    void SetIconColor(GameObject obj, Color color) { if (obj != null) obj.GetComponent<Renderer>().material.color = color; }

    IEnumerator OpenPathAnimation()
    {
        if (winEffectPrefab) Instantiate(winEffectPrefab, Vector3.zero, Quaternion.identity);
        float time = 0;
        Vector3 leftStart = leftTree.transform.position; Vector3 rightStart = rightTree.transform.position;
        Vector3 leftEnd = leftStart + new Vector3(-10, 0, 0); Vector3 rightEnd = rightStart + new Vector3(10, 0, 0);
        while (time < openDuration)
        {
            float t = time / openDuration;
            leftTree.transform.position = Vector3.Lerp(leftStart, leftEnd, t);
            rightTree.transform.position = Vector3.Lerp(rightStart, rightEnd, t);
            time += Time.deltaTime; yield return null;
        }
    }
}