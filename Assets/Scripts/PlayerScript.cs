using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class PlayerScript : MonoBehaviour
{
    // 移動速度
    public float moveSpeed = 5f;
    // 回転速度
    public float rotationSpeed = 10f;
    // インタラクト可能な距離
    public float interactionDistance = 2.0f;

    // Input Systemのアクション（Inspectorで設定）
    public InputActionProperty interactAction; 
    public InputActionProperty clickAction;

    // 移動可能かどうかのフラグ
    private bool canMove = true;

    // 移動の有効・無効を設定するメソッド
    public void SetCanMove(bool value)
    {
        canMove = value;
    }

    // インタラクションUIスクリプトの参照 (Inspectorで設定可能)
    public InteractionPromptUI interactionUI;
    // 現在フォーカスしているNPC
    private NPCInteractable currentInteractable;

    // 近くにいるNPCのリスト
    private List<NPCInteractable> nearbyNPCs = new List<NPCInteractable>();
    
    // インタラクト可能な視野角（前方何度まで許容するか）
    public float interactionAngle = 60f; 

    private Rigidbody rb;

    void Start()
    {
        // Rigidbodyの取得と回転制限の設定
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            // 物理演算による回転を防ぐ
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        // InteractionPromptUIを取得、なければ追加する
        if (interactionUI == null)
        {
            interactionUI = GetComponent<InteractionPromptUI>();
        }
        
        if (interactionUI == null)
        {
            interactionUI = gameObject.AddComponent<InteractionPromptUI>();
        }

        // アクションのデバッグチェック
        if (interactAction.action == null)
        {
            Debug.LogError("PlayerScript: Interact Action が設定されていません！InspectorでBinding（Spaceキーなど）を設定してください。");
        }
        else
        {
            // Debug.Log("PlayerScript: Interact Action は正常に設定されています: " + interactAction.action.name);
            interactAction.action.Enable();
        }

        if (clickAction.action == null)
        {
            Debug.LogError("PlayerScript: Click Action が設定されていません！InspectorでBinding（Mouse Left Buttonなど）を設定してください。");
        }
        else
        {
             clickAction.action.Enable();
        }
    }

    void OnEnable()
    {
        if (interactAction.action != null) interactAction.action.Enable();
        if (clickAction.action != null) clickAction.action.Enable();
    }

    void OnDisable()
    {
        if (interactAction.action != null) interactAction.action.Disable();
        if (clickAction.action != null) clickAction.action.Disable();
    }

    void Update()
    {
        // 移動が無効化されている場合は何もしない（会話中などはここで止まる）
        if (canMove) 
        {
             DetectInteractable(); // 毎フレーム周囲をスキャンしてUI表示
             HandleInteractionInput(); // 入力があったらインタラクト実行
             HandleMovement();
        }
        else
        {
            // 会話中などはUIを強制的に隠す
            if (interactionUI != null) interactionUI.Hide();
            
            // 会話終了後に再判定されるよう、ターゲットをリセットしておく
            currentInteractable = null;
        }
    }

    // インタラクト対象の検出とUI表示（Triggerエリア内 & 向き判定）
    void DetectInteractable()
    {
        NPCInteractable bestCandidate = null;
        float bestDot = -1.0f; // -1 (反対) ～ 1 (正面)

        // 近くのNPCリストの中から、一番プレイヤーが向いている方向にあるものを探す
        foreach (var npc in nearbyNPCs)
        {
            if (npc == null || !npc.gameObject.activeSelf) continue;

            Vector3 directionToNpc = (npc.transform.position - transform.position).normalized;
            // 高さの影響を無視してXZ平面だけで判定する（小人などの場合にも対応しやすい）
            directionToNpc.y = 0;
            
            // モデルの向きを使用する
            Vector3 forward = transform.forward;
            forward.y = 0;
            
            // 内積を計算 (1に近いほど正面)
            float dot = Vector3.Dot(forward.normalized, directionToNpc);
            
            // 指定した角度（視野角）以内かどうか判定
            // Dotが cos(angle) より大きければ範囲内
            // 例: 60度なら cos(30) = 0.866... ぐらい
            // ここでは簡易的に角度計算してから比較
            float angle = Vector3.Angle(forward, directionToNpc);
            
            if (angle <= interactionAngle)
            {
                // 一番正面に近いものを優先
                if (dot > bestDot)
                {
                    bestDot = dot;
                    bestCandidate = npc;
                }
            }
        }

        // 候補が見つかったか更新
        if (bestCandidate != currentInteractable)
        {
            currentInteractable = bestCandidate;
            
            if (currentInteractable != null)
            {
                if (interactionUI != null) interactionUI.Show(currentInteractable.interactionText);
            }
            else
            {
                if (interactionUI != null) interactionUI.Hide();
            }
        }
    }

    // Triggerによるエリア侵入検知
    void OnTriggerEnter(Collider other)
    {
        NPCInteractable npc = other.GetComponent<NPCInteractable>();
        if (npc != null && !nearbyNPCs.Contains(npc))
        {
            nearbyNPCs.Add(npc);
        }
    }

    // Triggerによるエリア退出検知
    void OnTriggerExit(Collider other)
    {
        NPCInteractable npc = other.GetComponent<NPCInteractable>();
        if (npc != null && nearbyNPCs.Contains(npc))
        {
            nearbyNPCs.Remove(npc);
            // 今ターゲットしているのがこれだったら解除
            if (currentInteractable == npc)
            {
                currentInteractable = null;
                if (interactionUI != null) interactionUI.Hide();
            }
        }
    }

    // インタラクション入力処理
    void HandleInteractionInput()
    {
        // Action経由の入力チェック
        bool interactActionPressed = (interactAction.action != null && interactAction.action.WasPressedThisFrame());
        
        // フォールバック
        bool spacePressed = (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame);
        bool enterPressed = (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame);
        
        bool interactPressed = interactActionPressed || spacePressed || enterPressed;

        // キーボード/ボタン入力の場合
        if (interactPressed)
        {
            if (currentInteractable != null)
            {
                Debug.Log("Interacting with: " + currentInteractable.name);
                currentInteractable.Interact();
            }
        }

        // マウスクリックの処理 (Raycastでの既存処理は一応残しておくか、不要なら削除。
        // 今回は「小人に当たりづらい」問題を解決するTrigger式がメインだが、
        // 明示的にクリックしたい場合もあるかもしれないため、干渉しない限り残しておいて良い。
        // ただしTrigger式と重複しないように注意)
        bool clickActionPressed = (clickAction.action != null && clickAction.action.WasPressedThisFrame());
        bool mousePressed = (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
        bool clickPressed = clickActionPressed || mousePressed;

        if (clickPressed)
        {
            HandleMouseClickInteraction();
        }
    }

    // マウスクリック時の個別処理（画面上のクリック）
    void HandleMouseClickInteraction()
    {
        // カメラからマウス位置へRayを飛ばして判定
        if (Camera.main != null && Mouse.current != null)
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 100f))
            {
                NPCInteractable npc = hit.collider.GetComponent<NPCInteractable>();
                if (npc != null)
                {
                    // 距離チェック (interactionDistanceはRaycast用だったが、クリック時の最大距離として再利用)
                    float distance = Vector3.Distance(transform.position, npc.transform.position);
                    
                    if (distance <= interactionDistance * 2.5f) // クリックの場合は少し遠くても許容するなど調整
                    {
                        npc.Interact();
                    }
                }
            }
        }
    }

    // 移動処理
    void HandleMovement()
    {
        float xInput = 0;
        float zInput = 0;

        // キーボード入力の取得
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
                zInput += 1;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
                zInput -= 1;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                xInput -= 1;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                xInput += 1;
        }

        Vector3 moveDirection = Vector3.zero;

        // カメラの向きに基づいて移動方向を計算
        if (Camera.main != null)
        {
            Vector3 cameraForward = Camera.main.transform.forward;
            Vector3 cameraRight = Camera.main.transform.right;

            // Y成分を0にして、地面に水平な移動にする
            cameraForward.y = 0;
            cameraRight.y = 0;
            cameraForward.Normalize();
            cameraRight.Normalize();

            // 入力に基づいて方向を決定
            moveDirection = (cameraForward * zInput) + (cameraRight * xInput);
        }
        else
        {
            // カメラがない場合のフォールバック
            moveDirection = new Vector3(xInput, 0, zInput);
        }

        // 斜め移動が速くならないように正規化
        Vector3 normalizedDirection = moveDirection.normalized;

        // 移動を実行（ワールド座標系） -- これは親(Root)を動かす
        transform.Translate(normalizedDirection * moveSpeed * Time.deltaTime, Space.World);

        // 移動方向に体を回転させる 
        // ノイズ対策: 入力が非常に小さい場合は回転させない (0.001fの閾値)
        if (moveDirection.sqrMagnitude > 0.001f)
        {
            Quaternion toRotation = Quaternion.LookRotation(normalizedDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, toRotation, rotationSpeed * Time.deltaTime);
        }
    }
}
