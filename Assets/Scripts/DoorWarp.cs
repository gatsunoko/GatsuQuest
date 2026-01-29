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

    [Tooltip("ワープ後に表示するマップ名（空欄なら表示しない）")]
    public string locationName;

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

    [Header("マップ切り替え")]
    [Tooltip("ワープ前にアクティブにするオブジェクト（移動先のマップなど）")]
    public GameObject[] objectsToActivate;

    [Tooltip("ワープ後に非アクティブにするオブジェクト（移動元のマップなど）")]
    public GameObject[] objectsToDeactivate;

    // ワープ中かどうかのフラグ
    private bool isWarping = false;

    // プレイヤーがトリガーに入った時の処理
    private void OnTriggerEnter(Collider other)
    {
        if (isWarping) return;

        // タグがPlayerの場合のみ実行
        if (other.CompareTag("Player"))
        {
            // コルーチンをScreenFader（常駐オブジェクト）で実行することで、
            // このオブジェクト自身がWarpSequence内で無効化されても処理が止まらないようにする
            if (ScreenFader.Instance != null)
            {
                ScreenFader.Instance.StartCoroutine(WarpSequence(other.gameObject));
            }
            else
            {
                Debug.LogWarning("ScreenFaderが見つからないため、このオブジェクトでコルーチンを実行します。親オブジェクトを無効化すると処理が止まる可能性があります。");
                StartCoroutine(WarpSequence(other.gameObject));
            }
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

        // 2.5 マップの有効化（ワープ先に足場があるように先に行う）
        if (objectsToActivate != null)
        {
            foreach (var obj in objectsToActivate)
            {
                if (obj != null) obj.SetActive(true);
            }
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

        // 5.5 古いマップの無効化（ライト切り替えの後に行う）
        if (objectsToDeactivate != null)
        {
            foreach (var obj in objectsToDeactivate)
            {
                // 自分自身が含まれている場合も、ScreenFader上で実行していれば問題ない
                if (obj != null) obj.SetActive(false);
            }
        }

        // 処理が安定するまで1フレーム待つ
        yield return null;

        // 6. フェードイン（画面を明るくする）
        if (ScreenFader.Instance != null)
        {
            ScreenFader.Instance.FadeIn(fadeDuration);
            
            // マップ名が設定されていれば表示 (3秒間)
            if (!string.IsNullOrEmpty(locationName))
            {
                ScreenFader.Instance.ShowLocationName(locationName, 3.0f);
            }
            
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
