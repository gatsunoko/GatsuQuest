
// NPC等に話しかけられる距離にいる時「話しかける」のUIを表示するスクリプト
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class InteractionPromptUI : MonoBehaviour
{
    [Header("UI 参照")]
    public Canvas uiCanvas;
    public TextMeshProUGUI promptText;
    public Image backgroundImage;
    
    [Header("設定")]
    public Vector3 offset = new Vector3(0, 2.0f, 0); // プレイヤーの頭上オフセット

    // UIを追従させるターゲット（このスクリプトがアタッチされているオブジェクト＝プレイヤー）
    private Transform targetTransform;
    private RectTransform canvasRect;
    private RectTransform uiElementRect; // テキストや背景をまとめた親

    private Camera mainCamera;

    void Start()
    {
        targetTransform = this.transform;
        
        // カメラ初期化
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) mainCamera = FindFirstObjectByType<Camera>();

        // 1. UIの生成・取得
        if (uiCanvas == null)
        {
            SetupDefaultUI();
        }

        // 2. Overlay設定
        if (uiCanvas != null)
        {
            uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            
            // 親子関係を維持したままにする（エラー回避のため）
            // ScreenSpaceOverlayなら親のTransformの影響を受けずに前面に描画されるため、
            // プレイヤーの子オブジェクトのままでも追従ロジック（WorldToScreenPoint）は機能する。
            
            // UI要素のRectTransformを取得（移動させる対象）
            if (promptText != null)
            {
                // テキストの親（背景含む）があればそれがベスト
                uiElementRect = promptText.transform.parent as RectTransform;
                if (uiElementRect == uiCanvas.transform) uiElementRect = promptText.rectTransform; 
            }
        }
        
        // キャンバススケーラー（解像度対応）
        CanvasScaler scaler = uiCanvas.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = uiCanvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }

        Hide();
    }

    void LateUpdate() // カメラの移動後に追従するためLateUpdate
    {
        if (uiCanvas == null || !uiCanvas.gameObject.activeSelf) return;
        
        // カメラ再取得
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }

        // 1. ターゲットのワールド座標を計算
        Vector3 worldPos = targetTransform.position + offset;

        // 2. スクリーン座標に変換
        Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

        // 3. カメラの背後にある場合は隠す、あるいは画面外へ
        if (screenPos.z < 0)
        {
            // 画面外へ飛ばす
            if (uiElementRect != null) uiElementRect.position = new Vector3(-1000, -1000, 0);
        }
        else
        {
            // 4. UIの位置適用
            if (uiElementRect != null)
            {
                uiElementRect.position = screenPos;
            }
        }
    }



    public void Show(string text)
    {
        if (promptText != null) promptText.text = text;
        if (uiCanvas != null) uiCanvas.gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (uiCanvas != null)
        {
            uiCanvas.gameObject.SetActive(false);
        }
    }

    private void SetupDefaultUI()
    {
        // Canvas
        GameObject canvasObj = new GameObject("InteractionCanvas_HUD");
        // Canvasをこのオブジェクト（プレイヤー）の子にする（シーン管理上安全）
        canvasObj.transform.SetParent(this.transform, false);

        uiCanvas = canvasObj.AddComponent<Canvas>();
        
        // Container (これを動かす)
        GameObject container = new GameObject("Container");
        container.transform.SetParent(canvasObj.transform, false);
        uiElementRect = container.AddComponent<RectTransform>();
        
        // Size
        uiElementRect.sizeDelta = new Vector2(300, 60);

        // Background
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(container.transform, false);
        backgroundImage = bgObj.AddComponent<Image>();
        backgroundImage.color = new Color(0, 0, 0, 0.7f);
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        // Text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(container.transform, false);
        promptText = textObj.AddComponent<TextMeshProUGUI>();
        promptText.text = "Interact";
        promptText.fontSize = 28;
        promptText.alignment = TextAlignmentOptions.Center;
        promptText.color = Color.white;
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
    }
}

