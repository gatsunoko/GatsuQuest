using UnityEngine;
using Yarn.Unity; // Yarn SpinnerのNamespace

public class NPCInteractable : MonoBehaviour
{
    // Yarn Spinnerのノード名（Startなど）
    public string yarnNodeName = "Start";

    // インタラクト時に表示するテキスト
    public string interactionText = "話しかける";

    // プレイヤーから呼び出されるインタラクト処理
    public void Interact()
    {
        // シーン内のDialogueRunnerを探す
        DialogueRunner runner = FindFirstObjectByType<DialogueRunner>();
        
        if (runner != null)
        {
            if (runner.IsDialogueRunning)
            {
                Debug.Log("既に会話中です");
                return;
            }

            Debug.Log("Starting Yarn Dialogue: " + yarnNodeName);
            // Yarnのノードを指定して会話開始
            runner.StartDialogue(yarnNodeName);
        }
        else
        {
            Debug.LogError("DialogueRunnerが見つかりません！シーンに配置してください。");
        }
    }
}
