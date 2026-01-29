using UnityEngine;
using UnityEngine.UI;
using TMPro; // TMPを使うために追加
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

    [Header("UI References")]
    [Tooltip("マップ名表示用の親オブジェクト（ImageやTextをまとめたもの）")]
    public GameObject locationUiObject;
    
    [Tooltip("マップ名を表示するTextコンポーネント")]
    public TextMeshProUGUI locationText; // Legacy TextからTextMeshProUGUIに変更

    // マップ名表示UIのCanvasGroup（フェード用）
    private CanvasGroup locationCanvasGroup;

    // 現在実行中のマップ名表示コルーチン
    private Coroutine currentLocationCoroutine;

    // マップ名を表示するコルーチン
    public void ShowLocationName(string name, float duration)
    {
        // UIオブジェクトが割り当てられていない、またはTextコンポーネントがない場合は処理しない
        if (locationUiObject == null) return;
        
        // CanvasGroupを取得（なければ追加）
        if (locationCanvasGroup == null)
        {
            locationCanvasGroup = locationUiObject.GetComponent<CanvasGroup>();
            if (locationCanvasGroup == null)
            {
                locationCanvasGroup = locationUiObject.AddComponent<CanvasGroup>();
            }
        }

        // 実行中のコルーチンがあれば止める（表示切り替えのため）
        if (currentLocationCoroutine != null)
        {
            StopCoroutine(currentLocationCoroutine);
        }

        // テキスト更新
        if (locationText != null)
        {
            locationText.text = name;
        }

        currentLocationCoroutine = StartCoroutine(ShowLocationNameRoutine(duration));
    }

    private IEnumerator ShowLocationNameRoutine(float duration)
    {
        locationUiObject.SetActive(true);
        // フェードインなしで最初から表示
        locationCanvasGroup.alpha = 1f;

        // 表示維持
        yield return new WaitForSeconds(duration);

        // フェードアウト (0.5秒に短縮)
        float fadeTime = 0.5f;
        float timer = 0f;
        while (timer < fadeTime)
        {
            timer += Time.deltaTime;
            locationCanvasGroup.alpha = Mathf.Lerp(1f, 0f, timer / fadeTime);
            yield return null;
        }
        locationCanvasGroup.alpha = 0f;
        locationUiObject.SetActive(false);
        
        currentLocationCoroutine = null;
    }

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

        // Textの作成 (マップ名表示用)
        // 手動で設定されていない場合のみ作成
        if (locationUiObject == null)
        {
            // まずコンテナ（親オブジェクト）を作成
            locationUiObject = new GameObject("LocationInfoPanel");
            locationUiObject.transform.SetParent(canvasObj.transform, false);
            
            // コンテナの配置
            RectTransform panelRt = locationUiObject.AddComponent<RectTransform>();
            panelRt.anchorMin = Vector2.zero; 
            panelRt.anchorMax = Vector2.one;
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;
            
            // CanvasGroupの追加
            locationCanvasGroup = locationUiObject.AddComponent<CanvasGroup>();
            locationCanvasGroup.alpha = 0f;
            
            // 初期状態は非表示
            locationUiObject.SetActive(false);
        }

        if (locationText == null)
        {
            // その中にTextを作成
            GameObject textObj = new GameObject("LocationText");
            // locationUiObjectがインスペクタで設定されているがTextがない場合、その下につくる
            textObj.transform.SetParent(locationUiObject.transform, false);
            
            locationText = textObj.AddComponent<TextMeshProUGUI>();
            
            // TMPの設定
            if (locationText != null)
            {
                locationText.alignment = TextAlignmentOptions.Center;
                locationText.fontSize = 40;
                locationText.color = Color.white; 
                locationText.enableWordWrapping = false;
            }
            
            RectTransform textRt = locationText.rectTransform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
        }
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
