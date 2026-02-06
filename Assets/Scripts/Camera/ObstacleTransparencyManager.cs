using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// カメラとプレイヤーの間にある障害物を検出し、透明化するマネージャー
/// メインカメラまたは空のGameObjectにアタッチしてください。
/// </summary>
public class ObstacleTransparencyManager : MonoBehaviour
{
    [Header("対象設定")]
    [Tooltip("プレイヤーのTransform（空の場合はタグ'Player'から自動取得）")]
    public Transform playerTransform;

    [Tooltip("プレイヤーのターゲット位置オフセット (足元からの高さ調整など)")]
    public Vector3 playerOffset = new Vector3(0, 1.0f, 0);
    
    [Tooltip("障害物とみなすレイヤー")]
    public LayerMask obstacleLayerMask; // デフォルトでは 'Wall' などを指定

    [Header("検出設定")]
    [Tooltip("障害物判定を行う球の半径（レイの太さ）")]
    public float sphereCastRadius = 0.5f;

    [Header("透明化設定")]
    [Tooltip("透明化時のアルファ値 (0.0=完全透明, 1.0=不透明)")]
    [Range(0f, 1f)]
    public float transparencyAlpha = 0.3f;
    
    [Tooltip("フェードにかかる速度")]
    public float fadeSpeed = 5.0f;

    // 現在透明化しているオブジェクトのリスト
    private List<ObstacleFader> currentFaders = new List<ObstacleFader>();
    private Transform cameraTransform;

    private void Start()
    {
        cameraTransform = Camera.main.transform;

        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
            else
            {
                Debug.LogWarning("[ObstacleTransparencyManager] Player not found. Please assign Player Transform or ensure Player tag is set.");
            }
        }
    }

    private void LateUpdate()
    {
        if (playerTransform == null || cameraTransform == null) return;

        // カメラからプレイヤーへの方向と距離を計算
        Vector3 targetPosition = playerTransform.position + playerOffset;
        Vector3 direction = targetPosition - cameraTransform.position;
        float distance = direction.magnitude;
        
        // 方向を正規化
        Vector3 normalizedDirection = direction.normalized;

        // デバッグ表示 (Sceneビューで見えるように)
        Debug.DrawLine(cameraTransform.position, targetPosition, Color.red);

        // レイキャスト（SphereCast）を実行
        // プレイヤー自身にヒットしないように、距離を少し短くする（0.5f手前まで）
        RaycastHit[] hits = Physics.SphereCastAll(cameraTransform.position, sphereCastRadius, normalizedDirection, distance - 0.5f, obstacleLayerMask);

        // 今回のフレームでヒットしたFaderのリスト
        List<ObstacleFader> hitFaders = new List<ObstacleFader>();

        foreach (var hit in hits)
        {
            Renderer r = hit.collider.GetComponent<Renderer>();
            if (r == null) continue;

            // ObstacleFaderコンポーネントを取得、なければ追加
            ObstacleFader fader = r.GetComponent<ObstacleFader>();
            if (fader == null)
            {
                fader = r.gameObject.AddComponent<ObstacleFader>();
            }

            // まだヒットリストに入っていなければ追加してフェードアウト
            if (!hitFaders.Contains(fader))
            {
                hitFaders.Add(fader);
                fader.FadeOut(transparencyAlpha, fadeSpeed);
            }
        }

        // 以前透明化していたが、今はヒットしていないオブジェクトを元に戻す
        for (int i = currentFaders.Count - 1; i >= 0; i--)
        {
            ObstacleFader fader = currentFaders[i];
            if (!hitFaders.Contains(fader))
            {
                if (fader != null)
                {
                    fader.FadeIn(fadeSpeed);
                }
                currentFaders.RemoveAt(i);
            }
        }

        // 現在のリストを更新（新規追加分）
        foreach (var fader in hitFaders)
        {
            if (!currentFaders.Contains(fader))
            {
                currentFaders.Add(fader);
            }
        }
    }
    
    // デバッグ用のギズモ描画
    private void OnDrawGizmosSelected()
    {
        if (playerTransform != null && Camera.main != null)
        {
            Gizmos.color = Color.cyan;
            Vector3 targetPosition = playerTransform.position + playerOffset;
            Vector3 direction = targetPosition - Camera.main.transform.position;
            Gizmos.DrawRay(Camera.main.transform.position, direction);
        }
    }
}
