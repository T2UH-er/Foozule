using UnityEngine;
using UnityEngine.UI;

public class TrayScrollManager1 : MonoBehaviour
{
    public static TrayScrollManager1 Instance { get; private set; }

    [Header("Tray References")]
    public Transform trayContainer;
    public Transform trayBoard;
    public float boardPadding = 2.0f;

    [Header("Scroll Limits & Sensitivity")]
    public float minX = 0.0f;
    public float maxX = 0.0f;
    public float scrollSensitivity = 3f;
    public float smoothScrollSpeed = 15f;
    public float pieceVisualPadding = 6.0f;

    [Header("Navigation UI (Để trống sẽ tự động khởi tạo)")]
    public GameObject navButtonsContainer;
    public Button leftNavButton;
    public Button rightNavButton;

    private float targetX = 0f;
    private Vector3 lastMousePos;
    private Vector3 dragStartMousePos;
    private bool isMouseDown = false;
    private bool isDragging = false;
    private Camera mainCam;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (trayContainer == null)
            trayContainer = transform;

        mainCam = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
        targetX = trayContainer.position.x;
    }

    private void Start()
    {
        EnsureNavButtonsCreated();
        AutoResizeTrayBoard();
        CalculateScrollBounds();
    }

    /// <summary>
    /// Tự động điều chỉnh kích thước tấm nền khay (nếu có SpriteRenderer).
    /// </summary>
    public void AutoResizeTrayBoard()
    {
        if (trayBoard == null || BlockSpawner1.Instance == null) return;

        int count = BlockSpawner1.Instance.ActivePieceCount;
        float spacing = BlockSpawner1.Instance.CurrentSpacing;
        float requiredWidth = (count * spacing) + boardPadding;

        SpriteRenderer sr = trayBoard.GetComponent<SpriteRenderer>();
        if (sr != null && sr.drawMode == SpriteDrawMode.Sliced)
        {
            sr.size = new Vector2(requiredWidth, sr.size.y);
        }
        else
        {
            Vector3 scale = trayBoard.localScale;
            scale.x = requiredWidth / 7f;
            trayBoard.localScale = scale;
        }
    }

    /// <summary>
    /// Tính toán giới hạn cuộn (minX, maxX) dựa trên số lượng và vị trí thực tế của các món ăn hiện có.
    /// </summary>
    public void CalculateScrollBounds()
    {
        if (mainCam == null)
            mainCam = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();

        float halfWidth = mainCam != null ? mainCam.orthographicSize * mainCam.aspect : 20.25f;
        float screenWidth = halfWidth * 2f;

        BlockSpawner1 spawner = BlockSpawner1.Instance;
        if (spawner == null || spawner.ActivePieceCount == 0)
        {
            minX = 0f;
            maxX = 0f;
            targetX = 0f;
            return;
        }

        int count = spawner.ActivePieceCount;
        float firstPieceLocalX = spawner.GetSlotPosition(0, count).x;
        float lastPieceLocalX = spawner.GetSlotPosition(count - 1, count).x;

        float contentLeft = firstPieceLocalX - pieceVisualPadding;
        float contentRight = lastPieceLocalX + pieceVisualPadding;
        float contentWidth = contentRight - contentLeft;

        // Nếu tất cả các món vừa vặn trên màn hình -> Không cần cuộn, cố định căn giữa
        if (contentWidth <= screenWidth - 1.0f)
        {
            minX = 0f;
            maxX = 0f;
            targetX = 0f;
        }
        else
        {
            // Trượt sang phải để thấy mép trái (tọa độ container dương)
            maxX = -contentLeft - halfWidth + pieceVisualPadding;
            // Trượt sang trái để thấy mép phải (tọa độ container âm)
            minX = halfWidth - pieceVisualPadding - contentRight;

            if (minX > maxX)
            {
                float tmp = minX;
                minX = maxX;
                maxX = tmp;
            }

            targetX = Mathf.Clamp(targetX, minX, maxX);
        }
    }

    /// <summary>
    /// Được gọi mỗi khi nội dung khay thay đổi (spawn level, đặt khối vào grid, trả khối về khay).
    /// </summary>
    public void OnTrayContentChanged(bool instantCenter = false)
    {
        AutoResizeTrayBoard();
        CalculateScrollBounds();

        if (minX >= maxX - 0.01f)
        {
            SnapToCenter(!instantCenter);
        }
        else if (instantCenter)
        {
            targetX = 0f;
            if (trayContainer != null)
            {
                Vector3 p = trayContainer.position;
                p.x = 0f;
                trayContainer.position = p;
                Physics2D.SyncTransforms();
            }
        }
        else
        {
            targetX = Mathf.Clamp(targetX, minX, maxX);
        }

        UpdateNavButtonsVisuals();
    }

    /// <summary>
    /// Đưa khay về chính giữa màn hình (X = 0).
    /// </summary>
    public void SnapToCenter(bool smooth = true)
    {
        targetX = 0f;
        if (!smooth && trayContainer != null)
        {
            Vector3 p = trayContainer.position;
            p.x = 0f;
            trayContainer.position = p;
            Physics2D.SyncTransforms();
        }
    }

    /// <summary>
    /// Nút điều hướng Trái: xem các món ăn phía bên trái (khay di chuyển sang phải).
    /// </summary>
    public void ScrollLeft()
    {
        if (minX >= maxX) return;

        float step = BlockSpawner1.Instance != null ? BlockSpawner1.Instance.CurrentSpacing : 13f;
        targetX = Mathf.Clamp(targetX + step, minX, maxX);

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayAudio("dragdrop");
    }

    /// <summary>
    /// Nút điều hướng Phải: xem các món ăn phía bên phải (khay di chuyển sang trái).
    /// </summary>
    public void ScrollRight()
    {
        if (minX >= maxX) return;

        float step = BlockSpawner1.Instance != null ? BlockSpawner1.Instance.CurrentSpacing : 13f;
        targetX = Mathf.Clamp(targetX - step, minX, maxX);

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayAudio("dragdrop");
    }

    private void Update()
    {
        HandleDragInput();
        UpdateSmoothScroll();
        UpdateNavButtonsVisuals();
    }

    private void HandleDragInput()
    {
        if (InputManager1.WasPrimaryPressedThisFrame)
        {
            isDragging = false;
            // Bỏ qua nếu click lên UI
            if (UnityEngine.EventSystems.EventSystem.current == null || !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                lastMousePos = InputManager1.PointerScreenPosition;
                dragStartMousePos = InputManager1.PointerScreenPosition;
                isMouseDown = true;
            }
            else
            {
                isMouseDown = false;
            }
        }

        if (InputManager1.IsPrimaryPressed && isMouseDown)
        {
            if (InputManager1.Instance == null || (!InputManager1.Instance.IsInteractingWithPiece && InputManager1.Instance.selectedPiece == null))
            {
                Vector2 currentPos = InputManager1.PointerScreenPosition;
                Vector3 delta = (Vector3)currentPos - lastMousePos;

                if (!isDragging && Vector2.Distance(currentPos, dragStartMousePos) > 8f)
                {
                    if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                    {
                        isDragging = true;
                    }
                }

                if (isDragging && minX < maxX)
                {
                    float moveX = delta.x * 0.01f * scrollSensitivity;
                    targetX = Mathf.Clamp(targetX + moveX, minX, maxX);
                }
            }
            lastMousePos = InputManager1.PointerScreenPosition;
        }

        if (InputManager1.WasPrimaryReleasedThisFrame)
        {
            isMouseDown = false;
            isDragging = false;
        }
    }

    private void UpdateSmoothScroll()
    {
        if (trayContainer == null) return;

        Vector3 pos = trayContainer.position;
        if (Mathf.Abs(pos.x - targetX) > 0.005f)
        {
            pos.x = Mathf.Lerp(pos.x, targetX, Time.deltaTime * smoothScrollSpeed);
            trayContainer.position = pos;
            Physics2D.SyncTransforms();
        }
        else if (pos.x != targetX)
        {
            pos.x = targetX;
            trayContainer.position = pos;
            Physics2D.SyncTransforms();
        }
    }

    private void UpdateNavButtonsVisuals()
    {
        if (leftNavButton == null || rightNavButton == null) return;

        bool canScrollAny = (minX < maxX - 0.05f);
        if (!canScrollAny)
        {
            if (navButtonsContainer != null && navButtonsContainer.activeSelf)
                navButtonsContainer.SetActive(false);
            return;
        }

        if (navButtonsContainer != null && !navButtonsContainer.activeSelf)
            navButtonsContainer.SetActive(true);

        bool canScrollLeft = (targetX < maxX - 0.1f);
        bool canScrollRight = (targetX > minX + 0.1f);

        // Nút Trái
        leftNavButton.interactable = canScrollLeft;
        SetButtonAlpha(leftNavButton, canScrollLeft ? 1.0f : 0.25f);
        if (canScrollLeft && !canScrollRight)
        {
            float pulse = 1f + 0.07f * Mathf.Sin(Time.unscaledTime * 4.5f);
            leftNavButton.transform.localScale = new Vector3(pulse, pulse, 1f);
        }
        else
        {
            leftNavButton.transform.localScale = Vector3.one;
        }

        // Nút Phải
        rightNavButton.interactable = canScrollRight;
        SetButtonAlpha(rightNavButton, canScrollRight ? 1.0f : 0.25f);
        if (canScrollRight)
        {
            float pulse = 1f + 0.07f * Mathf.Sin(Time.unscaledTime * 4.5f);
            rightNavButton.transform.localScale = new Vector3(pulse, pulse, 1f);
        }
        else
        {
            rightNavButton.transform.localScale = Vector3.one;
        }
    }

    private void SetButtonAlpha(Button btn, float alpha)
    {
        if (btn == null) return;
        CanvasGroup cg = btn.GetComponent<CanvasGroup>();
        if (cg == null) cg = btn.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = alpha;
    }

    private void EnsureNavButtonsCreated()
    {
        if (leftNavButton != null && rightNavButton != null)
        {
            leftNavButton.onClick.RemoveListener(ScrollLeft);
            leftNavButton.onClick.AddListener(ScrollLeft);
            rightNavButton.onClick.RemoveListener(ScrollRight);
            rightNavButton.onClick.AddListener(ScrollRight);
            return;
        }

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        Transform existing = canvas.transform.Find("TrayNavContainer");
        if (existing != null)
        {
            navButtonsContainer = existing.gameObject;
            leftNavButton = existing.Find("LeftNavBtn")?.GetComponent<Button>();
            rightNavButton = existing.Find("RightNavBtn")?.GetComponent<Button>();
            if (leftNavButton != null && rightNavButton != null)
            {
                UpdateNavButtonText(leftNavButton, "<");
                UpdateNavButtonText(rightNavButton, ">");

                leftNavButton.onClick.RemoveListener(ScrollLeft);
                leftNavButton.onClick.AddListener(ScrollLeft);
                rightNavButton.onClick.RemoveListener(ScrollRight);
                rightNavButton.onClick.AddListener(ScrollRight);
                return;
            }
        }

        GameObject containerObj = new GameObject("TrayNavContainer", typeof(RectTransform));
        containerObj.transform.SetParent(canvas.transform, false);
        navButtonsContainer = containerObj;

        RectTransform containerRt = containerObj.GetComponent<RectTransform>();
        containerRt.anchorMin = new Vector2(0f, 0f);
        containerRt.anchorMax = new Vector2(1f, 1f);
        containerRt.sizeDelta = Vector2.zero;
        containerRt.anchoredPosition = Vector2.zero;

        // Tính Y theo tọa độ Canvas của trayContainer
        float trayCanvasY = -610f;
        if (mainCam != null && trayContainer != null)
        {
            Vector3 screenPoint = mainCam.WorldToScreenPoint(trayContainer.position);
            RectTransform canvasRt = canvas.GetComponent<RectTransform>();
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screenPoint, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCam, out Vector2 localPoint))
            {
                trayCanvasY = localPoint.y;
            }
        }

        Sprite circleSprite = CreateCircleSprite();

        leftNavButton = CreateNavButton(containerObj.transform, "LeftNavBtn", new Vector2(65f, trayCanvasY), "<", circleSprite);
        leftNavButton.onClick.AddListener(ScrollLeft);

        rightNavButton = CreateNavButton(containerObj.transform, "RightNavBtn", new Vector2(-65f, trayCanvasY), ">", circleSprite);
        rightNavButton.onClick.AddListener(ScrollRight);
    }

    private void UpdateNavButtonText(Button btn, string symbol)
    {
        if (btn == null) return;
        var tmp = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.text = symbol;
            tmp.fontStyle = TMPro.FontStyles.Bold;
            return;
        }
        var uiText = btn.GetComponentInChildren<UnityEngine.UI.Text>();
        if (uiText != null)
        {
            uiText.text = symbol;
            uiText.fontStyle = FontStyle.Bold;
            return;
        }
    }

    private Button CreateNavButton(Transform parent, string name, Vector2 anchoredPos, string symbol, Sprite bgSprite)
    {
        GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(parent, false);

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        bool isLeft = anchoredPos.x > 0;
        rt.anchorMin = new Vector2(isLeft ? 0f : 1f, 0.5f);
        rt.anchorMax = new Vector2(isLeft ? 0f : 1f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(92f, 92f);

        Image img = btnObj.GetComponent<Image>();
        img.sprite = bgSprite;
        img.color = new Color(0.18f, 0.14f, 0.11f, 0.82f);

        Button btn = btnObj.GetComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.2f, 1.1f, 0.9f, 1f);
        colors.pressedColor = new Color(0.8f, 0.7f, 0.6f, 1f);
        colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.35f);
        btn.colors = colors;

        GameObject textObj = new GameObject("Icon", typeof(RectTransform));
        textObj.transform.SetParent(btnObj.transform, false);
        RectTransform textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;
        textRt.anchoredPosition = isLeft ? new Vector2(-2f, 0f) : new Vector2(2f, 0f);

        try
        {
            var tmp = textObj.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text = symbol;
            tmp.fontSize = 50f;
            tmp.fontStyle = TMPro.FontStyles.Bold;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 0.96f, 0.88f, 1f);
            tmp.raycastTarget = false;
        }
        catch
        {
            var uiText = textObj.AddComponent<UnityEngine.UI.Text>();
            uiText.text = symbol;
            uiText.fontSize = 42;
            uiText.fontStyle = FontStyle.Bold;
            uiText.alignment = TextAnchor.MiddleCenter;
            uiText.color = new Color(1f, 0.96f, 0.88f, 1f);
            uiText.raycastTarget = false;
        }

        return btn;
    }

    private static Sprite CreateCircleSprite()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        float center = (size - 1) * 0.5f;
        float radius = center;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alpha = Mathf.Clamp01((radius - dist) + 0.5f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
