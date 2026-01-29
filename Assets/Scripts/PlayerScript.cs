using UnityEngine;
using UnityEngine.InputSystem;

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

    void Start()
    {
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
            Debug.Log("PlayerScript: Interact Action は正常に設定されています: " + interactAction.action.name);
            // 念のためここでも有効化
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
        if (!canMove) 
        {
            // 会話中などはUIを強制的に隠す
            if (interactionUI != null) interactionUI.Hide();
            return;
        }

        DetectInteractable(); // 毎フレーム周囲をスキャンしてUI表示
        HandleInteractionInput(); // 入力があったらインタラクト実行
        HandleMovement();
    }

    // インタラクト対象の検出とUI表示
    void DetectInteractable()
    {
        currentInteractable = null;
        bool detected = false;

        // 前方へのRay判定
        RaycastHit hit;
        Debug.DrawRay(transform.position + Vector3.up * 0.5f, transform.forward * interactionDistance, Color.red);

        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, transform.forward, out hit, interactionDistance))
        {
            NPCInteractable npc = hit.collider.GetComponent<NPCInteractable>();
            if (npc != null)
            {
                currentInteractable = npc;
                if (interactionUI != null)
                {
                    interactionUI.Show(npc.interactionText);
                }
                detected = true;
            }
        }

        // 何も検出されなかった場合（マウスホバーの処理もここに入れるなら拡張可能だが、まずは前方基本）
        if (!detected)
        {
            if (interactionUI != null)
            {
                interactionUI.Hide();
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

        // マウスクリックの処理 (Raycastとは別に、クリックした位置に対しても判定を行う)
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
                    // 距離チェック
                    float distance = Vector3.Distance(transform.position, npc.transform.position);
                    
                    if (distance <= interactionDistance)
                    {
                        npc.Interact();
                    }
                    else
                    {
                        Debug.Log("Too far to interact via click");
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

        // 移動を実行（ワールド座標系）
        transform.Translate(normalizedDirection * moveSpeed * Time.deltaTime, Space.World);

        // 移動方向に体を回転させる
        if (normalizedDirection != Vector3.zero)
        {
            Quaternion toRotation = Quaternion.LookRotation(normalizedDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, toRotation, rotationSpeed * Time.deltaTime);
        }
    }
}
