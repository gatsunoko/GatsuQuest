using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 画面のフェードイン・フェードアウトを管理するクラス
/// シングルトンとして実装され、どこからでもアクセス可能です。
/// </summary>
public class ScreenFader : MonoBehaviour
{
    // シングルトンインスタンス
    public static ScreenFader Instance { get; private set; }

    [Header("Settings")]
    [Tooltip("フェード時の色（通常は黒）")]
    public Color fadeColor = Color.black;
    [Tooltip("デフォルトのフェード時間（秒）")]
    public float defaultDuration = 1.0f;

    private Canvas fadeCanvas;
    private Image fadeImage;

    private void Awake()
    {
        // シングルトンの初期化
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // シーン遷移しても破壊されないようにする

        // UIが手動で設定されていない場合、プログラムで生成する
        SetupFadeUI();
    }

    // フェード用のUI（CanvasとImage）を生成する
    private void SetupFadeUI()
    {
        // すでにImageがある場合は何もしない
        if (fadeImage != null) return;

        // Canvasの作成
        GameObject canvasObj = new GameObject("FadeCanvas");
        canvasObj.transform.SetParent(transform);
        fadeCanvas = canvasObj.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay; // 画面全体に表示
        fadeCanvas.sortingOrder = 999; // 最前面に表示

        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        // Imageの作成
        GameObject imageObj = new GameObject("FadeImage");
        imageObj.transform.SetParent(canvasObj.transform, false);
        
        fadeImage = imageObj.AddComponent<Image>();
        fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f); //最初は透明にしておく
        
        // 全画面にストレッチさせる
        RectTransform rt = fadeImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// 画面をフェードアウト（暗く）させます
    /// </summary>
    /// <param name="duration">所要時間（秒）。-1の場合はデフォルト値を使用</param>
    public void FadeOut(float duration = -1f)
    {
        StartCoroutine(FadeRoutine(1f, duration));
    }

    /// <summary>
    /// 画面をフェードイン（明るく）させます
    /// </summary>
    /// <param name="duration">所要時間（秒）。-1の場合はデフォルト値を使用</param>
    public void FadeIn(float duration = -1f)
    {
        StartCoroutine(FadeRoutine(0f, duration));
    }

    // フェード処理のコルーチン
    private IEnumerator FadeRoutine(float targetAlpha, float duration)
    {
        if (duration < 0) duration = defaultDuration;

        float startAlpha = fadeImage.color.a;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float newAlpha = Mathf.Lerp(startAlpha, targetAlpha, timer / duration);
            fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, newAlpha);
            yield return null;
        }

        // 最終的なアルファ値をセット
        fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, targetAlpha);
    }
}
