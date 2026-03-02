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

    [Header("Typewriter Settings")]
    public float typeWriterSpeed = 0.1f; // 1文字の表示間隔
    public AudioSource audioSource; // 効果音再生用

    // キャラクターデータベース（ScriptableObjectのリスト）
    public List<DialogueCharacter> characterDatabase;

    [Header("Option Settings")]
    public AudioClip optionsAppearSound; // 選択肢出現時の効果音
    public AudioClip optionChangeSound; // 選択肢移動時の効果音

    // プレイヤーの参照
    private PlayerScript playerScript;

    // 行を進めるための完了通知用 Action (Yarn v3 ではTaskCompletionSourceなどを使うが、ここではクリック待ち用)
    private System.Action onUserRequestAdvance = null;

    // スキップリクエスト用フラグ
    private bool skipInputRequested = false;

    public void Awake()
    {
        // 初期状態は非表示
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (namePanel != null) namePanel.SetActive(false);
        if (portraitImage != null) portraitImage.gameObject.SetActive(false);
    }

    private void Update()
    {
        // 会話中（パネルが出ている時）のみ入力を監視
        if (dialoguePanel != null && dialoguePanel.activeSelf)
        {
             if ((Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) ||
                (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame) ||
                (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame))
            {
                skipInputRequested = true;
                
                // 入力待ちの状態であれば、進める処理も呼ぶ
                UserRequestedViewAdvancement();
            }
            
            // ルビの位置更新
            UpdateRubiesPosition();
        }
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
        // 2. キャラクター名と画像の解決
        string characterName = dialogueLine.CharacterName;
        Sprite spriteToDisplay = null;

        // データベースからキャラクターを探す
        DialogueCharacter character = null;
        if (characterDatabase != null && !string.IsNullOrEmpty(characterName))
        {
            character = characterDatabase.Find(c => c.characterName == characterName);
        }

        // 表示用の名前を決定（デフォルトはキャラクター名）
        string displayCharacterName = characterName;
        
        // メタデータから #name タグを探して上書き
        string nameTag = GetNameTag(dialogueLine.Metadata);
        if (!string.IsNullOrEmpty(nameTag))
        {
            displayCharacterName = nameTag;
        }

        // 名前表示
        if (nameText != null)
        {
            if (string.IsNullOrEmpty(displayCharacterName))
            {
                nameText.text = "";
                nameText.gameObject.SetActive(false);
                if (namePanel != null) namePanel.SetActive(false);
            }
            else
            {
                nameText.text = displayCharacterName;
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
            }
            else
            {
                portraitImage.gameObject.SetActive(false);
            }
        }

        // 1. テキスト表示 (Typewriter Effect)
        if (dialogueText != null)
        {
            // まず空にする
            dialogueText.text = "";
            
            // 古いルビを削除
            foreach(var ruby in activeRubies)
            {
                if(ruby != null) Destroy(ruby);
            }
            activeRubies.Clear();
            activeRubyList.Clear();

            string rawText = dialogueLine.TextWithoutCharacterName.Text;
            
            // ルビ解析 (タグを除去したテキストを取得)
            List<RubyData> rubies;
            string fullText = ParseRubies(rawText, out rubies);
            
            // 効果音の準備
            AudioClip currentClip = null;
            if (character != null)
            {
                currentClip = character.voiceSound;
            }

            // スピードの上書きチェック
            float currentSpeed = typeWriterSpeed;
            float speedOverride = GetSpeedTag(dialogueLine.Metadata);
            if (speedOverride > 0f)
            {
                currentSpeed = speedOverride;
            }

            // フラグをリセット
            skipInputRequested = false;

            // 1文字ずつ表示
            int i = 0;
            while (i < fullText.Length)
            {
                // キャンセル（スキップ）チェック (Yarnの標準機能 + 手動入力フラグ)
                if (token.IsNextContentRequested || skipInputRequested)
                {
                    dialogueText.text = fullText;
                    
                    // 残りのルビも全て生成
                    foreach(var ruby in rubies)
                    {
                        if (!ruby.isSpawned) // まだ生成されていないもの
                        {
                            // インデックスのみ計算（スキップ時は全表示されるので単純）
                            int visibleIndex = GetVisibleLength(fullText.Substring(0, ruby.startIndex));
                            SpawnRuby(ruby, visibleIndex);
                            ruby.isSpawned = true;
                        }
                    }

                    skipInputRequested = false; 
                    break;
                }

                // ルビ生成チェック
                // 文字が表示されきったタイミング（漢字の最後の文字に到達した時）で生成
                foreach(var ruby in rubies)
                {
                    if (!ruby.isSpawned)
                    {
                        // startIndex + kanjiLength でルビ対象部分の終了インデックス（排他）
                        // i は現在処理中の文字インデックス。 
                        // 例: "漢字" (len=2, start=2). chars: 2,3. last char index = 3.
                        // i == 3 の時、 "字" を処理中。
                        // この文字を表示した後にルビを出したい。
                        
                        // ただし、iは0から増えていく。
                        // fullTextにはタグは含まれていない（ParseRubiesで除去済みだがHTMLタグはあるかもしれない）
                        // visibleLengthを使うのが確実だが、fullText上でのインデックス計算でいく。
                        
                        // ruby.kanji には HTMLタグが含まれている可能性がある ("<b>漢</b>"など)
                        // ParseRubiesのロジックでは kanji はタグの中身そのまま。
                        
                        if (i >= ruby.startIndex + ruby.kanji.Length - 1)
                        {
                            // 現在の表示済み文字数が、このルビがかかる漢字の開始位置
                            // ここでの GetVisibleLength は stripTags したもの
                            int visibleIndex = GetVisibleLength(fullText.Substring(0, ruby.startIndex));
                            SpawnRuby(ruby, visibleIndex);
                            ruby.isSpawned = true;
                        }
                    }
                }

                // タグ検知 (< から始まり > で終わる箇所)
                if (fullText[i] == '<')
                {
                    int closingIndex = fullText.IndexOf('>', i);
                    if (closingIndex != -1)
                    {
                        // タグ部分をまとめて追加 (<b>, <br>, <color=...> など)
                        string tag = fullText.Substring(i, closingIndex - i + 1);
                        dialogueText.text += tag;
                        
                        // インデックスを進める
                        i = closingIndex + 1;
                        
                        // タグ表示時は待機時間を入れずに次の文字へ
                        continue;
                    }
                }

                // 通常の文字を追加
                dialogueText.text += fullText[i];

                // 音を鳴らす (空白以外)
                if (currentClip != null && audioSource != null && !string.IsNullOrWhiteSpace(fullText[i].ToString()))
                {
                    audioSource.PlayOneShot(currentClip);
                }

                // インデックスを進める
                i++;

                // 待機
                await YarnTask.Delay((int)(currentSpeed * 1000));
            }
            // 念の為最後に全文確実に入れる
            dialogueText.text = fullText;
        }

        // ユーザーの入力待ちをする
        // #autoタグがある場合は待機せずに進む
        if (GetAutoTag(dialogueLine.Metadata))
        {
             // 何もしない（Task.CompletedTaskと同じ扱い）
             // ただし、タイプライター後の若干の余韻が必要ならここにDelayを入れてもいい
             // 今回は「自動で選択肢」とのことなので待たない
             return; 
        }

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

    // メタデータ（タグ）から name タグの値を取得
    private string GetNameTag(string[] metadata)
    {
        if (metadata == null) return "";
        foreach (string data in metadata)
        {
            if (data.StartsWith("name:"))
            {
                return data.Substring("name:".Length).Trim();
            }
        }
        return "";
    }

    // ルビを生成するメソッド
    private void SpawnRuby(RubyData ruby, int visibleIndex)
    {
        if (rubyTextPrefab == null || rubyContainer == null) return;

        GameObject obj = Instantiate(rubyTextPrefab, rubyContainer);
        TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
        
        if (text != null)
        {
            text.text = ruby.reading;
            // フォントや色はメインテキストに合わせるか、プレハブの設定に従う
        }

        // データを紐付けるためにコンポーネントを追加せずにリストで管理
        // ただし、位置更新のためにvisibleIndexやオブジェクト参照が必要
        
        // 簡易的な管理クラスを作らず、Dictionaryなどで管理も可能だが
        // ここではRubyDataにGameObject参照を持たせるか、別リストにする。
        
        activeRubies.Add(obj);
        
        // 位置更新用の管理リストに追加する情報を保持したいが、
        // activeRubiesはGameObjectのリストなので、データとの紐付けが弱い。
        // ここでRubyDataにGameObjectの参照を持たせるのが良さそうだが、RubyDataはstruct的。
        // 
        // 解決策: Update内で activeRubies と rubies を回すのは非効率かつ紐付け不可。
        // -> 新しい内部クラス ActiveRuby を作る。
        
        ActiveRuby active = new ActiveRuby();
        active.rubyObject = obj;
        active.tmp = text;
        active.targetVisibleIndex = visibleIndex;
        active.targetLength = ruby.visibleLength;
        
        activeRubyList.Add(active);
    }
    
    // 実行中のルビ管理用リスト
    private List<ActiveRuby> activeRubyList = new List<ActiveRuby>();

    private class ActiveRuby
    {
        public GameObject rubyObject;
        public TextMeshProUGUI tmp;
        public int targetVisibleIndex;
        public int targetLength;
    }

    // Updateでルビの位置を更新
    private void UpdateRubiesPosition()
    {
        if (dialogueText == null || activeRubyList.Count == 0) return;

        // 文字情報が更新されているか確認
        TMP_TextInfo textInfo = dialogueText.textInfo;
        if (textInfo == null || textInfo.characterCount == 0) return;

        foreach (var active in activeRubyList)
        {
            if (active.rubyObject == null) continue;

            // 対象の文字インデックスの情報を取得
            int charIndex = active.targetVisibleIndex;
            
            // 安全策: インデックスが範囲外ならスキップ (まだ描画されていないなど)
            if (charIndex >= textInfo.characterCount) continue;
            
            // 漢字の開始文字と終了文字の情報を取得して、中心を求める
            TMP_CharacterInfo firstChar = textInfo.characterInfo[charIndex];
            
            // 最後の文字 (長さが1なら開始と同じ)
            int lastIndex = charIndex + active.targetLength - 1;
            if (lastIndex >= textInfo.characterCount) lastIndex = charIndex; // 念の為
            TMP_CharacterInfo lastChar = textInfo.characterInfo[lastIndex];

            // どちらかが不可視（改行やスペース）だと座標がおかしい場合があるが、
            // 漢字なので基本は可視文字のはず。
            
            if (!firstChar.isVisible) continue;

            // 座標計算 (ローカル座標系ではなく、ワールド座標系で合わせるか、親が同じならローカルで)
            // TextMeshProの文字座標はTransformのローカル座標。
            // rubyContainerがdialogueTextと同じ親、あるいは同じCanvas内にあれば座標変換が必要。
            // 一番確実なのはワールド座標。
            
            Vector3 worldBottomLeft = dialogueText.transform.TransformPoint(firstChar.bottomLeft);
            Vector3 worldTopRight = dialogueText.transform.TransformPoint(lastChar.topRight);
            
            // 中心X
            float centerX = (worldBottomLeft.x + worldTopRight.x) / 2f;
            // 上端Y (漢字の一番上)
            float topY = worldTopRight.y;
            
            // ルビの位置を設定
            // 少し上にずらす + インスペクターでの設定値を加算
            active.rubyObject.transform.position = new Vector3(centerX + rubyOffset.x, topY + rubyOffset.y, 0);
            
            // 回転を合わせる (もし文字が回転していたら)
            active.rubyObject.transform.rotation = dialogueText.transform.rotation;
        }
    }

    // メタデータから speed タグの値を取得（なければ -1 を返す）
    private float GetSpeedTag(string[] metadata)
    {
        if (metadata == null) return -1f;
        foreach (string data in metadata)
        {
            if (data.StartsWith("speed:"))
            {
                if (float.TryParse(data.Substring("speed:".Length).Trim(), out float speed))
                {
                    return speed;
                }
            }
        }
        return -1f;
    }

    // メタデータから #auto タグの有無を取得
    private bool GetAutoTag(string[] metadata)
    {
        if (metadata == null) return false;
        foreach (string data in metadata)
        {
            if (data == "auto")
            {
                return true;
            }
        }
        return false;
    }

    // Rubyタグをパースしてリストアップし、テキストからタグを除去して返す
    private string ParseRubies(string rawText, out List<RubyData> rubies)
    {
        rubies = new List<RubyData>();
        
        // 正規表現: <ruby=reading>kanji</ruby>
        // 非貪欲マッチ(.*?)を使って入れ子や複数マッチに対応
        string pattern = @"<ruby=(.*?)>(.*?)</ruby>";
        
        // 処理中のテキスト（ここからマッチ部分を置換していく）
        // ただし、前から順に処理しないとインデックスがずれる。
        // Regex.Matchesで全マッチを取得してから、後ろから置換するか、
        // StringBuilderで再構築する。
        
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        int currentIndex = 0;
        
        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(rawText, pattern))
        {
            // マッチ前の通常テキストを追加
            sb.Append(rawText.Substring(currentIndex, match.Index - currentIndex));
            
            // マッチ部分の処理
            string reading = match.Groups[1].Value; // =の後の文字
            string kanji = match.Groups[2].Value;   // タグの中身
            
            // 現在のSBの長さ = 置換後のテキストでの開始インデックス
            int startIndex = sb.Length;
            
            // 漢字部分を追加
            sb.Append(kanji);
            
            // データを記録
            RubyData data = new RubyData();
            data.startIndex = startIndex;
            data.kanji = kanji;
            data.reading = reading;
            
            // 表示文字数の長さ計算（HTMLタグを除く文字数）
            data.visibleLength = GetVisibleLength(kanji);
            
            rubies.Add(data);
             
            currentIndex = match.Index + match.Length;
        }
        
        // 残りのテキストを追加
        if (currentIndex < rawText.Length)
        {
            sb.Append(rawText.Substring(currentIndex));
        }
        
        return sb.ToString();
    }
    
    // HTMLタグを除いた文字数をカウントする簡易ヘルパー
    private int GetVisibleLength(string text)
    {
        bool inTag = false;
        int length = 0;
        for(int i=0; i<text.Length; i++)
        {
            if (text[i] == '<')
            {
                // タグ開始
                inTag = true;
                continue;
            }
            
            if (inTag)
            {
                if (text[i] == '>')
                {
                    inTag = false;
                }
                continue;
            }
            
            length++;
        }
        return length;
    }

    // 現在表示中のルビオブジェクト
    private List<GameObject> activeRubies = new List<GameObject>();

    // 選択肢のコンテナ（ボタンを並べる親オブジェクト）
    public Transform optionButtonContainer;
    // 選択肢ボタンのプレハブ
    public GameObject optionButtonPrefab;

    [Header("Furigana Settings")]
    public GameObject rubyTextPrefab; // ふりがな用のプレハブ (TextMeshProUGUI)
    public Transform rubyContainer;   // ふりがな生成場所 (DialoguePanel内など、rubyTextPrefabの親になるオブジェクト)
    public Vector2 rubyOffset = new Vector2(0f, 5f); // 位置調整用オフセット

    // ふりがなデータ
    private class RubyData
    {
        public int startIndex; // fullText内での開始インデックス
        public string kanji;   // 対象の漢字（表示される文字列）
        public string reading; // ふりがな
        public int visibleStartIndex; // 表示文字数カウントでの開始位置 (後で計算)
        public int visibleLength;     // 表示文字数での長さ
        public bool isSpawned;        // 生成済みフラグ
    }

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

        // Skipフラグをリセット（前の入力が残らないように）
        skipInputRequested = false;

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
                    // 入力待ち期間中は反応させない（簡易チェック）
                    // ただしこの非同期メソッドの構成上、Delay中はここがクリックされてもselectedOptionIndexが変わるだけで
                    // main loopには到達していないため、ループに入った瞬間に反応してしまう可能性がある。
                    // 厳密には「inputEnabled」フラグなどが必要だが、
                    // YarnTask.Delayでメインスレッドのこのメソッド実行が止まっている間も、OnClickイベントは発火する。
                    // したがって、selectedOptionIndexが変わってしまう。
                    // click listener登録自体をDelay後にするか、フラグで弾く必要がある。
                    // ここではとりあえず単純化のため、このまま進める（ユーザー要望は主にキー入力連打と想定）。
                    // もしマウスクリックも即座に反応してほしくないなら、Listener登録をDelay後に移動するのがベスト。
                    
                    // Listener登録は後回しにするのが難しい（ローカル変数indexのキャプチャ問題など）ので
                    // Click側でフラグチェック等はせず、Delay後にListenerを有効化するか、
                    // あるいはDelayが終わるまでLoopに入らないので、
                    // selectedOptionIndexが変更されても、Loopに入るまではbreakしない...
                    // いや、selectedOptionIndexが変わると、Loopの条件 `while (selectedOptionIndex == -1)` が
                    // Loop開始直後にfalseになり、即終了してしまう。
                    // なので、クリックも無効化したい場合は対策が必要。
                    
                    // 今回は「ボタンを押して選択されないように」とのことなので、クリックも防ぐべき。
                    // → リスナー登録自体を維持しつつ、selectedOptionIndexへの代入を遅らせるか？
                    // 一番簡単なのは、Loop開始前に selectedOptionIndex = -1 を再度セットすることだが、
                    // それだとDelay中にクリックしたことが「無かったこと」になる（それは望ましい挙動かもしれない）。
                    
                    selectedOptionIndex = index;
                });
            }
        }

        // --- ここで効果音とウェイトを入れる ---

        // 効果音再生
        if (optionsAppearSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(optionsAppearSound);
        }

        // 初期選択位置 (0番目)
        int currentSelection = 0;
        int maxSelection = buttonTexts.Count - 1;

        // 初期カーソル表示（非表示のままDelayに入ると選択肢が出ているのにカーソルがない状態になるかもしれないので、ここで一旦更新推奨だが
        // 下のループ内の更新ロジックと重複する。
        // とりあえず初期状態(0番目)のカーソルだけONにしておくか、あるいはループに入るまでカーソルなしにするか。
        // 要望は「選択肢が表示される」なので、カーソルもあったほうが自然。
        for (int i = 0; i < cursors.Count; i++)
        {
            if (cursors[i] != null) cursors[i].SetActive(i == currentSelection);
        }

        // 入力の誤爆を防ぐために少しだけ待つ (1秒)
        // この間に入力を受け付けたくない
        
        // クリック対策：Delay前にクリックされても selectedOptionIndex が変わってしまうので、
        // Delay後にリセットする。これによりDelay中のクリックは無視される。
        selectedOptionIndex = -1; 
        
        await YarnTask.Delay(1000);

        // もう一度リセット（念のため）
        selectedOptionIndex = -1;

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



            // 選択肢移動音
            if (upPressed || downPressed)
            {
                if (optionChangeSound != null && audioSource != null)
                {
                    audioSource.PlayOneShot(optionChangeSound);
                }
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

            // --- カーソル表示更新 (常時表示) ---
            for (int i = 0; i < cursors.Count; i++)
            {
                GameObject cursor = cursors[i];
                if (cursor == null) continue;

                if (i == currentSelection)
                {
                    // 選択中は表示
                    cursor.SetActive(true);
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
}
