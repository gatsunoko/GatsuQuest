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

    // 初期化時
    private void Awake() 
    {
        // DialogueRunnerに手動で登録（確実性のため）
        var runner = FindAnyObjectByType<DialogueRunner>();
        if (runner != null)
        {
            runner.AddCommandHandler<string>("camera", SwitchCamera);
            Debug.Log("[DialogueCameraSwitcher] Command 'camera' registered successfully.");
        }
        else
        {
            Debug.LogError("[DialogueCameraSwitcher] DialogueRunner not found in the scene!");
        }
    }

    // Yarn Spinnerから呼び出すコマンド
    // 使い方: <<camera "CameraName">>
    public void SwitchCamera(string name)
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
