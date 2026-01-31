using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewDialogueCharacter", menuName = "GatsuQuest/Dialogue/Character")]
public class DialogueCharacter : ScriptableObject
{
    // Yarn Spinnerで使用するキャラクター名（例: "Villager"）
    public string characterName;
    
    // 基本の顔画像
    public Sprite defaultPortrait;

    // 会話時の効果音（設定がない場合は鳴らさない）
    public AudioClip voiceSound;

    // 表情差分（タグ名 -> 画像）
    // Inspectorで設定しやすいように独自のクラス定義
    [System.Serializable]
    public class Expression
    {
        public string name; // 例: "happy", "angry" (Yarnタグ: #portrait:happy)
        public Sprite portrait;
    }

    public List<Expression> expressions;

    // 画像を取得するメソッド
    public Sprite GetPortrait(string expressionName = "")
    {
        if (string.IsNullOrEmpty(expressionName))
            return defaultPortrait;

        foreach (var expression in expressions)
        {
            if (expression.name == expressionName)
                return expression.portrait;
        }

        // 見つからなければデフォルトを返す
        return defaultPortrait;
    }
}
