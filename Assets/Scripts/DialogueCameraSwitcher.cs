using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Yarn.Unity;
using Unity.Cinemachine; // Cinemachine v3

public class DialogueCameraSwitcher : MonoBehaviour
{
    [System.Serializable]
    public class CameraEntry
    {
        public string cameraName;
        // GameObjectではなくCinemachineCamera自体を登録
        public CinemachineCamera cinemachineCamera;
    }

    // カメラのリスト
    public List<CameraEntry> cameras;

    // 現在のアクティブカメラの名前（デバッグ用）
    [SerializeField]
    private string currentCameraName;

    private CinemachineBrain brain;
    private CinemachineBlendDefinition defaultBlendDefinition; // 初期設定を保存

    // 初期化時
    private void Awake() 
    {
        // DialogueRunnerに手動で登録
        var runner = FindAnyObjectByType<DialogueRunner>();
        if (runner != null)
        {
            // 文字列配列で受け取るハンドラとして登録
            runner.AddCommandHandler<string[]>("camera", SwitchCamera);
            Debug.Log("[DialogueCameraSwitcher] Command 'camera' registered successfully.");
            
            // Brainを探しておく
            brain = FindAnyObjectByType<CinemachineBrain>();
            if (brain != null)
            {
                defaultBlendDefinition = brain.DefaultBlend; // 元の設定を保存
            }
        }
        else
        {
            Debug.LogError("[DialogueCameraSwitcher] DialogueRunner not found in the scene!");
        }
    }

    // Yarn Spinnerから呼び出すコマンド
    // 使い方: <<camera "CameraName">> または <<camera "CameraName" 2.0>>
    public void SwitchCamera(string[] args)
    {
        if (args.Length == 0)
        {
            Debug.LogError("[DialogueCameraSwitcher] 'camera' command requires at least 1 argument (camera name).");
            return;
        }

        string cameraName = args[0];
        
        if (brain != null)
        {
            // 第2引数があればその時間を使う
            if (args.Length >= 2 && float.TryParse(args[1], out float duration))
            {
                var blend = brain.DefaultBlend;
                blend.Time = duration;
                brain.DefaultBlend = blend;
            }
            else
            {
                // ない場合は初期設定に戻す（重要：前のコマンドで変更されたままにしない）
                brain.DefaultBlend = defaultBlendDefinition;
            }
        }

        SwitchCameraInternal(cameraName);
    }
    
    // SwitchCameraWithDuration は削除（統合したため）

    private void SwitchCameraInternal(string name)
    {
        bool found = false;

        foreach (var entry in cameras)
        {
            if (entry.cinemachineCamera == null) continue;

            if (entry.cameraName == name)
            {
                // 対象のカメラのPriorityを上げて有効にする
                entry.cinemachineCamera.Priority = 20;
                currentCameraName = name;
                found = true;
                Debug.Log($"[DialogueCameraSwitcher] Switched to camera: {name} (Priority 20)");
            }
            else
            {
                // それ以外はPriorityを下げる
                entry.cinemachineCamera.Priority = 10;
            }
        }

        if (!found)
        {
            Debug.LogWarning($"[DialogueCameraSwitcher] Camera not found: {name}");
        }
    }
}
