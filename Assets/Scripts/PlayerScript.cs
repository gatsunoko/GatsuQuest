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

    void Start()
    {
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
        if (!canMove) return;

        HandleInteraction();
        HandleMovement();
    }

    // インタラクション（会話開始）の処理
    void HandleInteraction()
    {
        // Action経由の入力チェック
        bool interactActionPressed = (interactAction.action != null && interactAction.action.WasPressedThisFrame());
        bool clickActionPressed = (clickAction.action != null && clickAction.action.WasPressedThisFrame());

        // フォールバック（Action設定がうまくいっていない場合のため、直接デバイスもチェック）
        bool spacePressed = (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame);
        bool enterPressed = (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame);
        bool mousePressed = (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);

        bool interactPressed = interactActionPressed || spacePressed || enterPressed;
        bool clickPressed = clickActionPressed || mousePressed;

        // キーボード入力（Spaceキー等）の場合
        if (interactPressed)
        {
            Debug.Log("Interact button pressed"); // デバッグログ
            
            // プレイヤーの前方にRayを飛ばして判定
            RaycastHit hit;
            // 可視化のためにDebug.DrawRayを使用
            Debug.DrawRay(transform.position + Vector3.up * 0.5f, transform.forward * interactionDistance, Color.red, 1.0f);

            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, transform.forward, out hit, interactionDistance))
            {
                Debug.Log("Ray hit: " + hit.collider.name); // デバッグログ

                NPCInteractable npc = hit.collider.GetComponent<NPCInteractable>();
                if (npc != null)
                {
                    npc.Interact();
                    return;
                }
                else
                {
                    Debug.Log("Hit object does not have NPCInteractable script");
                }
            }
            else
            {
                Debug.Log("Raycast hit nothing");
            }
        }

        // マウスクリックの場合
        if (clickPressed)
        {
            Debug.Log("Click pressed");
            // カメラからマウス位置へRayを飛ばして判定
            if (Camera.main != null)
            {
                Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, 100f))
                {
                    Debug.Log("Click Ray hit: " + hit.collider.name);
                    NPCInteractable npc = hit.collider.GetComponent<NPCInteractable>();
                    if (npc != null)
                    {
                        // 距離チェック
                        float distance = Vector3.Distance(transform.position, npc.transform.position);
                        Debug.Log("Distance to NPC: " + distance);
                        if (distance <= interactionDistance)
                        {
                            npc.Interact();
                            return;
                        }
                        else
                        {
                            Debug.Log("Too far to interact");
                        }
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
