using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro; 
using UnityEngine.UI; 
using Yarn.Unity; 
using System.Threading; // For CancellationToken
using System; 
using UnityEngine.InputSystem; // New Input System 

// Yarn Spinner v3では DialoguePresenterBase を継承する
public class DialogueManager : DialoguePresenterBase
{
    // 会話UIのパネル
    public GameObject dialoguePanel;
    // 会話テキスト
    public TextMeshProUGUI dialogueText;
    
    // 名前パネル
    public GameObject namePanel;
    // 名前テキスト
    public TextMeshProUGUI nameText;
    
    // 顔画像
    public Image portraitImage;

    // キャラクターデータベース（ScriptableObjectのリスト）
    public List<DialogueCharacter> characterDatabase;

    // プレイヤーの参照
    private PlayerScript playerScript;

    // 行を進めるための完了通知用 Action (Yarn v3 ではTaskCompletionSourceなどを使うが、ここではクリック待ち用)
    private System.Action onUserRequestAdvance = null;

    public void Awake()
    {
        // 初期状態は非表示
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (namePanel != null) namePanel.SetActive(false);
        if (portraitImage != null) portraitImage.gameObject.SetActive(false);
    }

    // 会話開始時
    public override YarnTask OnDialogueStartedAsync()
    {
        // プレイヤーの移動を停止
        playerScript = FindFirstObjectByType<PlayerScript>();
        if (playerScript != null)
            playerScript.SetCanMove(false);

        if (dialoguePanel != null) dialoguePanel.SetActive(true);

        return YarnTask.CompletedTask;
    }

    // 会話終了時
    public override YarnTask OnDialogueCompleteAsync()
    {
        // UI非表示
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (namePanel != null) namePanel.SetActive(false);
        if (portraitImage != null) portraitImage.gameObject.SetActive(false);

        // プレイヤー移動再開
        if (playerScript != null)
            playerScript.SetCanMove(true);

        return YarnTask.CompletedTask;
    }

    // 行（セリフ）の表示
    public override async YarnTask RunLineAsync(LocalizedLine dialogueLine, LineCancellationToken token)
    {
        // 1. テキスト表示
        // LocalizedLine.Text.Text だと "Name: Hello" のようになる場合があるため、
        // TextWithoutCharacterName を使うのが適切
        if (dialogueText != null)
            dialogueText.text = dialogueLine.TextWithoutCharacterName.Text;

        // 2. キャラクター名と画像の解決
        string characterName = dialogueLine.CharacterName;
        Sprite spriteToDisplay = null;

        // データベースからキャラクターを探す
        DialogueCharacter character = null;
        if (characterDatabase != null && !string.IsNullOrEmpty(characterName))
        {
            character = characterDatabase.Find(c => c.characterName == characterName);
        }

        // 名前表示
        if (nameText != null)
        {
            if (string.IsNullOrEmpty(characterName))
            {
                nameText.text = "";
                nameText.gameObject.SetActive(false);
                if (namePanel != null) namePanel.SetActive(false);
            }
            else
            {
                nameText.text = characterName;
                nameText.gameObject.SetActive(true);
                if (namePanel != null) namePanel.SetActive(true);
            }
        }

        // 画像表示（タグ #portrait:expression を解析）
        string portraitTag = GetPortraitTag(dialogueLine.Metadata);

        if (character != null)
        {
            spriteToDisplay = character.GetPortrait(portraitTag);
        }

        // --- 画像処理・レイアウト調整 ---
        if (portraitImage != null)
        {
            if (spriteToDisplay != null)
            {
                portraitImage.sprite = spriteToDisplay;
                portraitImage.gameObject.SetActive(true);
                AdjustLayout(true);
            }
            else
            {
                portraitImage.gameObject.SetActive(false);
                AdjustLayout(false);
            }
        }

        // ユーザーの入力待ちをする
        // UserRequestedViewAdvancement() が呼ばれるまで待機
        await WaitForInputAsync(token);
    }

    // 入力待ち処理
    private async YarnTask WaitForInputAsync(LineCancellationToken token)
    {
        // 重要: 会話開始時の入力（クリック等）がそのまま「次へ」の判定に使われないように、
        // 最初の1フレームだけ待機して、入力判定を次のフレームから開始する
        await YarnTask.Yield();

        bool hasAdvanced = false;

        // クリックなどのActionが呼ばれたら hasAdvanced を true にする
        System.Action advanceAction = () => { hasAdvanced = true; };
        
        // コールバック登録
        onUserRequestAdvance = advanceAction;

        while (!hasAdvanced)
        {
            // キャンセル（強制終了や次の行へのスキップ要求）をチェック
            if (token.IsNextContentRequested)
            {
                break;
            }

            // シンプルな入力チェックを追加 (New Input System対応)
            if ((Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) ||
                (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame) ||
                (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame))
            {
                hasAdvanced = true;
            }

            // 1フレーム待機
            await YarnTask.Yield();
        }

        // コールバック解除
        onUserRequestAdvance = null;
    }


    // ユーザーが入力をした時（クリックやスペース）に呼ぶ
    public void UserRequestedViewAdvancement()
    {
        if (onUserRequestAdvance != null)
        {
            onUserRequestAdvance();
        }
    }
    
    // UIボタン等から呼ぶ用
    public void OnContinueClicked()
    {
        UserRequestedViewAdvancement();
    }

    // メタデータ（タグ）から portrait タグの値を取得
    private string GetPortraitTag(string[] metadata)
    {
        if (metadata == null) return "";
        foreach (string data in metadata)
        {
            if (data.StartsWith("portrait:"))
            {
                return data.Substring("portrait:".Length).Trim();
            }
        }
        return "";
    }

    // レイアウト調整
    void AdjustLayout(bool showPortrait)
    {
        if (dialoguePanel != null)
        {
            RectTransform panelRect = dialoguePanel.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                float padding = showPortrait ? 27f : 95f;
                Vector2 newMin = panelRect.offsetMin;
                newMin.x = padding;
                panelRect.offsetMin = newMin;

                Vector2 newMax = panelRect.offsetMax;
                newMax.x = -padding; 
                panelRect.offsetMax = newMax;
            }
        }

        if (dialogueText != null)
        {
            RectTransform textRect = dialogueText.rectTransform;
            if (textRect != null)
            {
                float xPos = showPortrait ? 0f : -83f;
                Vector2 newPos = textRect.anchoredPosition;
                newPos.x = xPos;
                textRect.anchoredPosition = newPos;
            }
        }
    }
}
