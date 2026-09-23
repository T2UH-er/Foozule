using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager1 : MonoBehaviour
{
    public static InputManager1 Instance { get; private set; }

    // Tuong thich nguoc voi TrayScrollManager1 dang doc selectedPiece
    public BlockPiece1 selectedPiece => GameManager1.Instance != null ? GameManager1.Instance.HeldPiece : null;
    public bool IsInteractingWithPiece => selectedPiece != null || isPendingDrag || pendingPiece != null;

    [Header("Double Click Settings")]
    [Tooltip("Thời gian tối đa giữa 2 lần click để tính là Double Click")]
    public float doubleClickThreshold = 0.3f;

    [Header("Drag Settings")]
    [Tooltip("Khoảng cách di chuyển chuột tối thiểu (pixel) để bắt đầu nhấc khối kéo đi")]
    public float dragThresholdDistance = 10f;

    private float lastClickTime = -1f;
    private BlockPiece1 lastClickedPiece = null;
    private Vector2Int lastClickedGridOrigin = new Vector2Int(-999, -999);

    private bool isPendingDrag = false;
    private Vector2 mouseDownScreenPos;
    private Vector3 mouseDownWorldPos;
    private BlockPiece1 pendingPiece = null;
    private PlacedBlockInfo1 pendingGridInfo = null;

    private Vector3 dragOffset;
    private Camera mainCam;

    // ─── NEW INPUT SYSTEM STATIC HELPERS ─────────────────────────────────

    /// <summary>
    /// Vị trí con trỏ (chuột hoặc ngón tay chạm) trên màn hình (Screen Pixel Coordinates).
    /// </summary>
    public static Vector2 PointerScreenPosition
    {
        get
        {
            if (Touchscreen.current != null)
            {
                var touch = Touchscreen.current.primaryTouch;
                if (touch.press.isPressed || touch.press.wasReleasedThisFrame)
                    return touch.position.ReadValue();
            }
            if (Mouse.current != null)
            {
                return Mouse.current.position.ReadValue();
            }
            if (Pointer.current != null)
            {
                return Pointer.current.position.ReadValue();
            }
            return Vector2.zero;
        }
    }

    /// <summary>
    /// Vừa nhấn xuống trong frame này (tương đương Input.GetMouseButtonDown(0)).
    /// </summary>
    public static bool WasPrimaryPressedThisFrame
    {
        get
        {
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
                return true;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                return true;
            if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
                return true;
            return false;
        }
    }

    /// <summary>
    /// Đang được giữ nhấn (tương đương Input.GetMouseButton(0)).
    /// </summary>
    public static bool IsPrimaryPressed
    {
        get
        {
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
                return true;
            if (Mouse.current != null && Mouse.current.leftButton.isPressed)
                return true;
            if (Pointer.current != null && Pointer.current.press.isPressed)
                return true;
            return false;
        }
    }

    /// <summary>
    /// Vừa nhấc ra trong frame này (tương đương Input.GetMouseButtonUp(0)).
    /// </summary>
    public static bool WasPrimaryReleasedThisFrame
    {
        get
        {
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasReleasedThisFrame)
                return true;
            if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
                return true;
            if (Pointer.current != null && Pointer.current.press.wasReleasedThisFrame)
                return true;
            return false;
        }
    }

    /// <summary>
    /// Chuột phải vừa click (xoay nhanh trên PC).
    /// </summary>
    public static bool WasSecondaryPressedThisFrame
    {
        get
        {
            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
                return true;
            return false;
        }
    }

    private void Awake()
    {
        Instance = this;
        mainCam = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
    }

    private void Update()
    {
        // ─── 1. XỬ LÝ CHUỘT TRÁI / CHẠM MÀN HÌNH (CLICK XUỐNG) ───
        if (WasPrimaryPressedThisFrame)
        {
            AudioManager.Instance.PlayAudio("dragdrop");
            // Bỏ qua nếu đang click lên UI (Menu, Tutorial Dark Overlay, Button...)
            if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            mouseDownScreenPos = PointerScreenPosition;
            mouseDownWorldPos = GetMouseWorldPos();
            RaycastHit2D hit = Physics2D.Raycast(mouseDownWorldPos, Vector2.zero);

            if (hit.collider != null)
            {
                // Kiểm tra Khách hàng (Hiển thị thoại khi chạm vào khách)
                Customer1 customer = hit.collider.GetComponentInParent<Customer1>();
                if (customer != null)
                {
                    customer.OnCustomerTapped();
                }

                // Kiểm tra khối trên Khay
                BlockPiece1 piece = hit.collider.GetComponentInParent<BlockPiece1>();
                if (piece != null)
                {
                    if (piece.IsRotating) return; // Đang xoay thì không nhận input

                    // Kiểm tra Double Click trên Khay
                    if (piece == lastClickedPiece && (Time.unscaledTime - lastClickTime) <= doubleClickThreshold)
                    {
                        lastClickTime = -1f;
                        lastClickedPiece = null;
                        isPendingDrag = false;
                        pendingPiece = null;
                        piece.TriggerRotate();
                        return;
                    }
                    else
                    {
                        lastClickTime = Time.unscaledTime;
                        lastClickedPiece = piece;
                        lastClickedGridOrigin = new Vector2Int(-999, -999);
                        pendingPiece = piece;
                        pendingGridInfo = null;
                        isPendingDrag = true;
                        return;
                    }
                }

                // Kiểm tra khối đã đặt trên Grid
                PlacedBlockInfo1 info = hit.collider.GetComponentInParent<PlacedBlockInfo1>();
                if (info != null)
                {
                    // Kiểm tra Double Click trên Grid
                    if (info.originCell == lastClickedGridOrigin && (Time.unscaledTime - lastClickTime) <= doubleClickThreshold)
                    {
                        lastClickTime = -1f;
                        lastClickedGridOrigin = new Vector2Int(-999, -999);
                        lastClickedPiece = null;
                        isPendingDrag = false;
                        pendingGridInfo = null;

                        if (GridManager1.Instance != null)
                            GridManager1.Instance.TryRotatePlacedBlock(info);
                        return;
                    }
                    else
                    {
                        lastClickTime = Time.unscaledTime;
                        lastClickedGridOrigin = info.originCell;
                        lastClickedPiece = null;
                        pendingPiece = null;
                        pendingGridInfo = info;
                        isPendingDrag = true;
                    }
                }
            }
        }

        // ─── 2. HỖ TRỢ CHUỘT PHẢI ĐỂ XOAY NHANH TRÊN PC ───
        if (WasSecondaryPressedThisFrame)
        {
            Vector3 worldPos = GetMouseWorldPos();
            RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero);

            if (hit.collider != null)
            {
                BlockPiece1 piece = hit.collider.GetComponentInParent<BlockPiece1>();
                if (piece != null && !piece.IsRotating)
                {
                    piece.TriggerRotate();
                    return;
                }

                PlacedBlockInfo1 info = hit.collider.GetComponentInParent<PlacedBlockInfo1>();
                if (info != null && GridManager1.Instance != null)
                {
                    GridManager1.Instance.TryRotatePlacedBlock(info);
                    return;
                }
            }
        }

        // ─── 3. KIỂM TRA BẮT ĐẦU KÉO (KHI CHUỘT / NGÓN TAY DI CHUYỂN VƯỢT NGƯỠNG) ───
        if (isPendingDrag && IsPrimaryPressed)
        {
            if (Vector2.Distance(PointerScreenPosition, mouseDownScreenPos) >= dragThresholdDistance)
            {
                // Nếu đang ở Tutorial Level 2 bước hướng dẫn xoay món ăn, bắt buộc phải double-click xoay trước khi được kéo
                if (TutorialManager1.Instance != null && TutorialManager1.Instance.IsLevel2AwaitingRotate())
                {
                    isPendingDrag = false;
                    pendingPiece = null;
                    pendingGridInfo = null;
                    return;
                }

                isPendingDrag = false;

                if (pendingPiece != null)
                {
                    dragOffset = pendingPiece.transform.position - mouseDownWorldPos;
                    GameManager1.Instance.BeginPickFromTray(pendingPiece);
                }
                else if (pendingGridInfo != null)
                {
                    GameManager1.Instance.BeginPickFromGrid(pendingGridInfo);
                    if (GameManager1.Instance.HeldPiece != null)
                        dragOffset = GameManager1.Instance.HeldPiece.transform.position - mouseDownWorldPos;
                }

                pendingPiece = null;
                pendingGridInfo = null;
            }
        }

        // ─── 4. CẬP NHẬT KÉO THẢ (DRAGGING) ───
        if (GameManager1.Instance != null && GameManager1.Instance.HeldPiece != null && IsPrimaryPressed)
        {
            GameManager1.Instance.UpdateDrag(GetMouseWorldPos() + dragOffset);
        }

        // ─── 5. THẢ KHỐI (END DRAG) ───
        if (WasPrimaryReleasedThisFrame)
        {
            AudioManager.Instance.PlayAudio("dragdrop");
            isPendingDrag = false;
            pendingPiece = null;
            pendingGridInfo = null;

            if (GameManager1.Instance != null && GameManager1.Instance.HeldPiece != null)
            {
                GameManager1.Instance.EndDrag(GetMouseWorldPos());
            }
        }
    }

    private Vector3 GetMouseWorldPos()
    {
        if (mainCam == null) mainCam = Camera.main;
        Vector2 screenPos = PointerScreenPosition;
        Vector3 mouseScreen = new Vector3(screenPos.x, screenPos.y, -mainCam.transform.position.z);
        return mainCam.ScreenToWorldPoint(mouseScreen);
    }
}
