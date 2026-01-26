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

    // 選択肢のコンテナ（ボタンを並べる親オブジェクト）
    public Transform optionButtonContainer;
    // 選択肢ボタンのプレハブ
    public GameObject optionButtonPrefab;

    // 最後に選ばれた選択肢のID
    private int selectedOptionIndex = -1;

    // ... (既存のコード)

    // 選択肢の表示
    public override async YarnTask<DialogueOption?> RunOptionsAsync(DialogueOption[] dialogueOptions, LineCancellationToken token)
    {
        // 選択肢コンテナとプレハブがない場合はエラー
        if (optionButtonContainer == null || optionButtonPrefab == null)
        {
            Debug.LogError("Option Button Container or Prefab is not assigned in DialogueManager!");
            return null; // 何も選ばずに終了
        }

        // 既存のボタンを削除
        foreach (Transform child in optionButtonContainer)
        {
            Destroy(child.gameObject);
        }

        optionButtonContainer.gameObject.SetActive(true);
        selectedOptionIndex = -1;

        // ボタンのリスト
        List<TextMeshProUGUI> buttonTexts = new List<TextMeshProUGUI>();
        List<Button> buttons = new List<Button>(); // マウス操作用
        List<GameObject> cursors = new List<GameObject>(); // カーソル画像用

        // 選択肢ボタンを作成
        for (int i = 0; i < dialogueOptions.Length; i++)
        {
            DialogueOption option = dialogueOptions[i];
            
            // 選択不可の選択肢はスキップ
            if (option.IsAvailable == false) continue;

            GameObject buttonObj = Instantiate(optionButtonPrefab, optionButtonContainer);
            TextMeshProUGUI buttonText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
            Button button = buttonObj.GetComponent<Button>();

            // カーソル画像を検索 (名前が "Cursor" のオブジェクトを探す)
            Transform cursorTrans = buttonObj.transform.Find("Cursor");
            GameObject cursorObj = (cursorTrans != null) ? cursorTrans.gameObject : null;
            
            if (cursorObj != null)
            {
                cursorObj.SetActive(false); // 最初は非表示
                cursors.Add(cursorObj);
            }
            else
            {
                // カーソルが見つからない場合のダミー（null除け）
                cursors.Add(null);
            }

            if (buttonText != null)
            {
                // テキストを設定 (純粋なテキスト)
                buttonText.text = option.Line.Text.Text;
                buttonTexts.Add(buttonText);
            }

            buttons.Add(button);

            // 何番目の選択肢かをキャプチャ
            int index = i;
            
            // マウスクリック時の動作
            if (button != null)
            {
                button.onClick.AddListener(() => 
                {
                    selectedOptionIndex = index;
                });
            }
        }

        // 初期選択位置 (0番目)
        int currentSelection = 0;
        int maxSelection = buttonTexts.Count - 1;
        
        // 最後にカーソルを動かした時間 (点滅リセット用)
        float lastSelectionChangeTime = Time.unscaledTime;

        // 入力待機ループ
        while (selectedOptionIndex == -1)
        {
            if (token.IsNextContentRequested)
            {
                break;
            }

            // --- キーボード操作 (New Input System) ---
            bool upPressed = false;
            bool downPressed = false;
            bool submitPressed = false;

            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame) upPressed = true;
                if (Keyboard.current.sKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame) downPressed = true;
                if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame) submitPressed = true;
            }

            // 入力があったら点滅タイマーをリセット(即時表示)
            if (upPressed || downPressed)
            {
                lastSelectionChangeTime = Time.unscaledTime;
            }

            if (upPressed)
            {
                currentSelection--;
                if (currentSelection < 0) currentSelection = maxSelection;
            }
            if (downPressed)
            {
                currentSelection++;
                if (currentSelection > maxSelection) currentSelection = 0;
            }

            // --- カーソル表示更新 (画像点滅) ---
            // 点滅判定 (0.5秒ごとに切り替え)
            // 操作直後は (Time - lastSelectionChangeTime) が 0 になるため (< 0.5f) で必ず表示される
            bool showCursor = (Mathf.Repeat(Time.unscaledTime - lastSelectionChangeTime, 1.0f) < 0.5f);

            for (int i = 0; i < cursors.Count; i++)
            {
                GameObject cursor = cursors[i];
                if (cursor == null) continue;

                if (i == currentSelection)
                {
                    // 選択中は点滅
                    cursor.SetActive(showCursor);
                }
                else
                {
                    // 非選択時は非表示
                    cursor.SetActive(false);
                }
            }



            // 決定キー
            if (submitPressed)
            {
                // activeOptionsを再生成 (コンパイルエラー修正)
                List<DialogueOption> activeOptions = new List<DialogueOption>();
                foreach (var op in dialogueOptions)
                {
                    if (op.IsAvailable) activeOptions.Add(op);
                }

                // activeOptions[currentSelection] に対応するIDを知る必要がある
                // DialogueOption オブジェクト自体を返せばいいので、インデックスではなくオブジェクトで管理してもいいが
                // 返り値は DialogueOption なので、元の配列の中のどれかを探す。
                // activeOptions[currentSelection] が正解。
                
                DialogueOption selectedOption = activeOptions[currentSelection];
                
                // 元の配列(dialogueOptions)の中でのインデックスを探す (ID)
                for(int k=0; k<dialogueOptions.Length; k++)
                {
                    if (dialogueOptions[k].DialogueOptionID == selectedOption.DialogueOptionID)
                    {
                        selectedOptionIndex = k;
                        break;
                    }
                }
            }

            await YarnTask.Yield();
        }

        // UIを隠す
        optionButtonContainer.gameObject.SetActive(false);
        foreach (Transform child in optionButtonContainer)
        {
            Destroy(child.gameObject);
        }

        // 選ばれた選択肢を返す
        if (selectedOptionIndex != -1)
        {
            return dialogueOptions[selectedOptionIndex];
        }

        return null;
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
