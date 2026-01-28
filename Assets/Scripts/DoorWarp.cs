using UnityEngine;
using System.Collections;
using Unity.Cinemachine; // Cinemachine v3を使用

/// <summary>
/// プレイヤーが接触した際に別の場所にワープさせるスクリプト
/// 画面のフェード、ライトの切り替え、カメラのワープ処理も行います。
/// </summary>
public class DoorWarp : MonoBehaviour
{
    [Header("ワープ設定")]
    [Tooltip("プレイヤーのワープ先のTransform（位置と回転）")]
    public Transform targetDestination;

    [Tooltip("フェードイン・アウトにかかる時間（秒）")]
    public float fadeDuration = 0.5f;

    [Header("回転設定")]
    [Tooltip("ターゲット（ワープ先）の回転をそのまま使用するか")]
    public bool useTargetRotation = true;

    [Tooltip("ターゲットの回転を使わない場合の、ワープ後のプレイヤーの向き（Y軸角度）")]
    public float customYRotation = 0f;

    [Header("ライト設定")]
    [Tooltip("ワープ時にONにするライトのリスト（屋内ライトなど）")]
    public GameObject[] lightsToTurnOn;
    
    [Tooltip("ワープ時にOFFにするライトのリスト（屋外ディレクショナルライトなど）")]
    public GameObject[] lightsToTurnOff;

    // ワープ中かどうかのフラグ
    private bool isWarping = false;

    // プレイヤーがトリガーに入った時の処理
    private void OnTriggerEnter(Collider other)
    {
        if (isWarping) return;

        // タグがPlayerの場合のみ実行
        if (other.CompareTag("Player"))
        {
            StartCoroutine(WarpSequence(other.gameObject));
        }
    }

    // ワープの一連の流れを実行するコルーチン
    private IEnumerator WarpSequence(GameObject player)
    {
        isWarping = true;

        // 1. プレイヤーの操作を無効化
        PlayerScript playerScript = player.GetComponent<PlayerScript>();
        if (playerScript != null)
        {
            playerScript.SetCanMove(false);
        }

        // 2. フェードアウト（画面を暗くする）
        if (ScreenFader.Instance != null)
        {
            ScreenFader.Instance.FadeOut(fadeDuration);
            yield return new WaitForSeconds(fadeDuration); // フェードが終わるまで待機
        }
        else
        {
            Debug.LogWarning("ScreenFaderのインスタンスが見つかりません！フェードなしでワープします。");
        }

        // 3. プレイヤーの位置移動
        Vector3 oldPosition = player.transform.position;
        
        // Transformの位置と回転を更新
        player.transform.position = targetDestination.position;
        
        // 回転はY軸（向き）のみ適用し、X/Z軸の回転（転倒）を防ぐ
        float targetYAngle = 0f;
        if (useTargetRotation)
        {
            targetYAngle = targetDestination.rotation.eulerAngles.y;
        }
        else
        {
            targetYAngle = customYRotation;
        }

        player.transform.rotation = Quaternion.Euler(0, targetYAngle, 0);

        // 4. カメラのワープ処理 (Cinemachineが滑らかに移動してしまうのを防ぐ)
        var brain = CinemachineBrain.GetActiveBrain(0);
        if (brain != null)
        {
             // プレイヤーを追従しているアクティブな仮想カメラを探す
             var vcam = brain.ActiveVirtualCamera as CinemachineCamera;
             
             // フォローまたはルックアット対象がプレイヤーの場合
             if (vcam != null && (vcam.Follow == player.transform || vcam.LookAt == player.transform))
             {
                 // Cinemachineに「ターゲットがワープした」と通知して、即座にカットさせる
                 // 以前の位置との差分（Delta）を渡す必要がある
                 vcam.OnTargetObjectWarped(player.transform, targetDestination.position - oldPosition);
             }
        }
        
        // 5. ライトの切り替え
        foreach (var light in lightsToTurnOn)
        {
            if (light != null) light.SetActive(true);
        }

        foreach (var light in lightsToTurnOff)
        {
            if (light != null) light.SetActive(false);
        }

        // 処理が安定するまで1フレーム待つ
        yield return null;

        // 6. フェードイン（画面を明るくする）
        if (ScreenFader.Instance != null)
        {
            ScreenFader.Instance.FadeIn(fadeDuration);
            yield return new WaitForSeconds(fadeDuration);
        }

        // 7. プレイヤーの操作を有効化
        if (playerScript != null)
        {
            playerScript.SetCanMove(true);
        }

        isWarping = false;
    }
}
