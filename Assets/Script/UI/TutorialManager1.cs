using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// TutorialManager1 — Hệ thống hướng dẫn Modular độc lập:
/// - Level 1: Core Loop (Đọc vị khách hàng -> Kéo thả món ăn vào bàn cờ).
/// - Level 2: Hướng dẫn Xoay món ăn (Làm tối màn hình + Double Click để xoay).
/// - Level 8: Hướng dẫn Cơ chế Vùng ảnh hưởng (Focus khách hàng -> Màu nền Avatar tương ứng màu Vùng trên bàn cờ).
/// - Các Level khác: Tự động vô hiệu hóa 100%, 0% ảnh hưởng gameplay.
/// </summary>
public class TutorialManager1 : MonoBehaviour
{
    public static TutorialManager1 Instance { get; private set; }

    [Header("UI References")]
    public Canvas tutorialCanvas;
    public Image darkOverlay;
    public GameObject dialogueBox;
    public TextMeshProUGUI dialogueText;
    public TextMeshProUGUI tapToContinueText;
    public RectTransform handPointer;

    [Header("Sprites & Assets")]
    public Sprite handSprite;
    public Sprite dialogueBoxSprite;
    public TMP_FontAsset fontAsset;

    private int activeTutorialLevel = 0;
    private int currentStep = 0;
    private Coroutine handAnimCoroutine;
    private Camera mainCam;
    private RectTransform dialogueBoxRt;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        mainCam = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
        BlockPiece1.OnPieceRotated += OnPieceRotatedHandler;
    }

    private void OnDestroy()
    {
        BlockPiece1.OnPieceRotated -= OnPieceRotatedHandler;
    }

    private void Start()
    {
        int activeLevel = GameManager1.Instance != null ? GameManager1.Instance.currentLevelIndex : 1;
        CheckAndStartLevelTutorial(activeLevel);
    }

    /// <summary>
    /// API Modular: Kiểm tra và khởi chạy hướng dẫn cho bất kỳ màn chơi nào.
    /// Hoàn toàn không cần sửa đổi các file code khác khi thêm tutorial mới!
    /// </summary>
    public void CheckAndStartLevelTutorial(int targetLevel)
    {
        // Dừng tất cả coroutine cũ (đặc biệt là DismissRoutine hẹn giờ tắt UI từ level trước)
        StopAllCoroutines();
        handAnimCoroutine = null;
        activeTutorialLevel = targetLevel;
        currentStep = 0;

        switch (activeTutorialLevel)
        {
            case 1:
                InitUI();
                StartCoroutine(StartLevel1TutorialRoutine());
                break;
            case 2:
                InitUI();
                StartCoroutine(StartLevel2TutorialRoutine());
                break;
            case 8:
                InitUI();
                StartCoroutine(StartLevel8ZonesTutorialRoutine());
                break;
            case 4:
                InitUI();
                StartCoroutine(StartLevel4TutorialRoutine());
                break;
            default:
                DisableTutorialUI();
                break;
        }
    }

    public void DisableTutorialUI()
    {
        StopAllCoroutines();
        handAnimCoroutine = null;
        if (dialogueBox != null) dialogueBox.SetActive(false);
        if (darkOverlay != null) darkOverlay.gameObject.SetActive(false);
        if (handPointer != null) handPointer.gameObject.SetActive(false);
        if (tutorialCanvas != null) tutorialCanvas.gameObject.SetActive(false);
    }

    private void InitUI()
    {
        LoadDefaultAssets();
        SetupUI();
        if (tutorialCanvas != null) tutorialCanvas.gameObject.SetActive(true);
    }

    private void LoadDefaultAssets()
    {
        if (handSprite == null)
        {
            // 1. Thử load Single Sprite từ Resources
            handSprite = Resources.Load<Sprite>("hand");

            // 2. Nếu Texture ở chế độ Multiple Sprite (sprite sheet), load qua LoadAll
            if (handSprite == null)
            {
                Sprite[] allHandSprites = Resources.LoadAll<Sprite>("hand");
                if (allHandSprites != null && allHandSprites.Length > 0)
                {
                    handSprite = allHandSprites[0];
                }
            }

            // 3. Fallback tìm theo tên trong các đối tượng đã load
            if (handSprite == null)
            {
                Sprite[] allUi = Resources.FindObjectsOfTypeAll<Sprite>();
                foreach (var s in allUi)
                {
                    if (s != null && s.name.ToLower().Contains("hand"))
                    {
                        handSprite = s;
                        break;
                    }
                }
            }

            if (handSprite == null)
            {
                Debug.LogWarning("[TutorialManager1] Không tìm thấy Sprite 'hand' trong Resources/!");
            }
        }

        if (dialogueBoxSprite == null)
        {
            // 1. Thử load Single Sprite từ Resources
            dialogueBoxSprite = Resources.Load<Sprite>("DialougeBox_1_1");

            // 2. Nếu Texture ở chế độ Multiple Sprite, load qua LoadAll
            if (dialogueBoxSprite == null)
            {
                Sprite[] allBoxSprites = Resources.LoadAll<Sprite>("DialougeBox_1_1");
                if (allBoxSprites != null && allBoxSprites.Length > 0)
                {
                    dialogueBoxSprite = allBoxSprites[0];
                }
            }

            // 3. Fallback
            if (dialogueBoxSprite == null)
            {
                Sprite[] allUi = Resources.FindObjectsOfTypeAll<Sprite>();
                foreach (var s in allUi)
                {
                    if (s != null && (s.name.Contains("DialougeBox_1_1") || s.name == "DialougeBox_1_1"))
                    {
                        dialogueBoxSprite = s;
                        break;
                    }
                }
            }
        }

        if (fontAsset == null)
        {
            // Load Font TMP mặc định từ Resources
            fontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            if (fontAsset == null)
            {
                TMP_FontAsset[] allFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                if (allFonts != null && allFonts.Length > 0)
                {
                    fontAsset = allFonts[0];
                }
            }
        }
    }

    private void SetupUI()
    {
        if (tutorialCanvas == null)
        {
            GameObject canvasObj = new GameObject("TutorialCanvas");
            canvasObj.transform.SetParent(this.transform);
            tutorialCanvas = canvasObj.AddComponent<Canvas>();
            tutorialCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            tutorialCanvas.sortingOrder = 99;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // Lớp nền tối (Dark Overlay)
        if (darkOverlay == null)
        {
            GameObject overlayObj = new GameObject("DarkOverlay", typeof(RectTransform), typeof(Image), typeof(Button));
            overlayObj.transform.SetParent(tutorialCanvas.transform, false);
            RectTransform rt = overlayObj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;

            darkOverlay = overlayObj.GetComponent<Image>();
            darkOverlay.color = new Color(0f, 0f, 0f, 0.65f);
            darkOverlay.raycastTarget = true;

            Button btn = overlayObj.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(OnScreenTapped);
        }

        // Khung thoại (Dialogue Box)
        if (dialogueBox == null)
        {
            GameObject boxObj = new GameObject("DialogueBox", typeof(RectTransform), typeof(Image));
            boxObj.transform.SetParent(tutorialCanvas.transform, false);
            dialogueBoxRt = boxObj.GetComponent<RectTransform>();
            dialogueBoxRt.anchorMin = new Vector2(0.5f, 0.5f);
            dialogueBoxRt.anchorMax = new Vector2(0.5f, 0.5f);
            dialogueBoxRt.pivot = new Vector2(0.5f, 0.5f);
            dialogueBoxRt.anchoredPosition = new Vector2(0f, 480f);
            dialogueBoxRt.sizeDelta = new Vector2(980f, 300f);

            Image boxImg = boxObj.GetComponent<Image>();
            if (dialogueBoxSprite != null) boxImg.sprite = dialogueBoxSprite;
            boxImg.type = Image.Type.Simple;
            boxImg.color = Color.white;
            boxImg.raycastTarget = false;

            dialogueBox = boxObj;

            // Text nội dung
            GameObject textObj = new GameObject("DialogueText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(boxObj.transform, false);
            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0.08f, 0.08f);
            textRt.anchorMax = new Vector2(0.92f, 0.92f);
            textRt.pivot = new Vector2(0.5f, 0.5f);
            textRt.sizeDelta = Vector2.zero;

            dialogueText = textObj.GetComponent<TextMeshProUGUI>();
            if (fontAsset != null) dialogueText.font = fontAsset;
            dialogueText.enableAutoSizing = true;
            dialogueText.fontSizeMin = 22;
            dialogueText.fontSizeMax = 32;
            dialogueText.fontStyle = FontStyles.Bold;
            dialogueText.color = new Color(0.12f, 0.12f, 0.15f, 1f);
            dialogueText.alignment = TextAlignmentOptions.Center;
            dialogueText.margin = new Vector4(20f, 5f, 20f, 5f);
            dialogueText.enableWordWrapping = true;
            dialogueText.raycastTarget = false;

            // Text "Chạm vào màn hình để tiếp tục"
            GameObject tapObj = new GameObject("TapText", typeof(RectTransform), typeof(TextMeshProUGUI));
            tapObj.transform.SetParent(boxObj.transform, false);
            RectTransform tapRt = tapObj.GetComponent<RectTransform>();
            tapRt.anchorMin = new Vector2(0.15f, 0.06f);
            tapRt.anchorMax = new Vector2(0.85f, 0.24f);
            tapRt.pivot = new Vector2(0.5f, 0.5f);
            tapRt.sizeDelta = Vector2.zero;

            tapToContinueText = tapObj.GetComponent<TextMeshProUGUI>();
            if (fontAsset != null) tapToContinueText.font = fontAsset;
            tapToContinueText.enableAutoSizing = true;
            tapToContinueText.fontSizeMin = 16;
            tapToContinueText.fontSizeMax = 22;
            tapToContinueText.fontStyle = FontStyles.Bold;
            tapToContinueText.color = new Color(0.35f, 0.35f, 0.4f, 1f);
            tapToContinueText.alignment = TextAlignmentOptions.Center;
            tapToContinueText.text = "<b>— Chạm bất kỳ để tiếp tục —</b>";
            tapToContinueText.raycastTarget = false;
        }
        else
        {
            dialogueBoxRt = dialogueBox.GetComponent<RectTransform>();
            Image boxImg = dialogueBox.GetComponent<Image>();
            if (boxImg != null)
            {
                if (dialogueBoxSprite != null) boxImg.sprite = dialogueBoxSprite;
                boxImg.raycastTarget = false;
            }

            if (dialogueText != null)
            {
                dialogueText.enableAutoSizing = true;
                dialogueText.fontSizeMin = 18;
                dialogueText.fontSizeMax = 32;
                dialogueText.alignment = TextAlignmentOptions.Center;
                dialogueText.margin = new Vector4(20f, 5f, 20f, 5f);
                dialogueText.enableWordWrapping = true;
            }
        }

        // Bàn tay chỉ dẫn (Hand Pointer)
        if (handPointer == null)
        {
            GameObject handObj = new GameObject("HandPointer", typeof(RectTransform), typeof(Image));
            handObj.transform.SetParent(tutorialCanvas.transform, false);
            handPointer = handObj.GetComponent<RectTransform>();
            handPointer.sizeDelta = new Vector2(180f, 220f);
            handPointer.pivot = new Vector2(0.2f, 0.8f);

            Image handImg = handObj.GetComponent<Image>();
            if (handSprite != null) handImg.sprite = handSprite;
            handImg.color = Color.white;
            handImg.raycastTarget = false;
        }
        else
        {
            Image handImg = handPointer.GetComponent<Image>();
            if (handImg != null)
            {
                if (handSprite != null) handImg.sprite = handSprite;
                handImg.color = Color.white;
                handImg.raycastTarget = false;
            }
        }

        handPointer.gameObject.SetActive(false);
    }

    public bool IsLevel2AwaitingRotate() => activeTutorialLevel == 2 && currentStep == 1;

    private void OnScreenTapped()
    {
        if (activeTutorialLevel == 1 && currentStep == 1)
        {
            GoToStep2_GuideDragAndDrop();
        }
        else if (activeTutorialLevel == 8 && currentStep == 1)
        {
            GoToLevel8_Step2_FocusZones();
        }
        else if (activeTutorialLevel == 4 && currentStep == 1)
        {
            DisableTutorialUI();
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // LEVEL 1: CORE LOOP (Focus Customer -> Drag & Drop)
    // ══════════════════════════════════════════════════════════════════════

    private IEnumerator StartLevel1TutorialRoutine()
    {
        yield return new WaitForSeconds(0.25f);
        currentStep = 1;

        if (darkOverlay != null)
        {
            darkOverlay.gameObject.SetActive(true);
            darkOverlay.color = new Color(0f, 0f, 0f, 0.65f);
        }

        if (dialogueBox != null)
        {
            dialogueBox.SetActive(true);
            if (dialogueBoxRt != null) dialogueBoxRt.anchoredPosition = new Vector2(0f, -380f);
        }

        if (dialogueText != null)
        {
            dialogueText.text = "<b>Khách hàng đang muốn ăn món có vị <color=#1D4ED8>2 Mặn</color> và <color=#B91C1C>1 Cay</color>!</b>";
        }

        if (tapToContinueText != null) tapToContinueText.gameObject.SetActive(true);

        if (handPointer != null)
        {
            handPointer.gameObject.SetActive(true);
            if (handAnimCoroutine != null) StopCoroutine(handAnimCoroutine);
            handAnimCoroutine = StartCoroutine(PointCustomerRoutine());
        }
    }

    private IEnumerator PointCustomerRoutine()
    {
        Vector3 customerWorldPos = new Vector3(0f, 6.5f, 0f);
        if (CustomerSpawner1.Instance != null && CustomerSpawner1.Instance.currentCustomers.Count > 0 && CustomerSpawner1.Instance.currentCustomers[0] != null)
        {
            customerWorldPos = CustomerSpawner1.Instance.currentCustomers[0].transform.position;
        }

        Vector2 screenPos = WorldToCanvasPos(customerWorldPos + new Vector3(0.5f, -1.2f, 0f));
        handPointer.anchoredPosition = screenPos;
        handPointer.localRotation = Quaternion.Euler(0f, 0f, 25f);

        Vector2 basePos = screenPos;
        while (currentStep == 1 && (activeTutorialLevel == 1 || activeTutorialLevel == 8))
        {
            float bob = Mathf.Sin(Time.unscaledTime * 6f) * 20f;
            handPointer.anchoredPosition = basePos + new Vector2(0f, bob);
            yield return null;
        }
    }

    public void GoToStep2_GuideDragAndDrop()
    {
        currentStep = 2;
        if (darkOverlay != null) darkOverlay.gameObject.SetActive(false);

        if (dialogueBox != null)
        {
            dialogueBox.SetActive(true);
            if (dialogueBoxRt != null) dialogueBoxRt.anchoredPosition = new Vector2(0f, 480f);
        }

        if (dialogueText != null)
        {
            dialogueText.text = "<b>Kéo <color=#B91C1C>món ăn</color> từ khay và đặt vào <color=#1D4ED8>bàn cờ</color>!</b>";
        }

        if (tapToContinueText != null) tapToContinueText.gameObject.SetActive(false);

        if (handAnimCoroutine != null) StopCoroutine(handAnimCoroutine);
        handAnimCoroutine = StartCoroutine(DragGuideRoutine());
    }

    private IEnumerator DragGuideRoutine()
    {
        if (handPointer == null) yield break;
        handPointer.gameObject.SetActive(true);
        handPointer.localRotation = Quaternion.identity;

        CanvasGroup cg = handPointer.GetComponent<CanvasGroup>();
        if (cg == null) cg = handPointer.gameObject.AddComponent<CanvasGroup>();

        while (currentStep == 2 && activeTutorialLevel == 1)
        {
            Vector3 startWorld = new Vector3(-2f, -6f, 0f);
            if (BlockSpawner1.Instance != null)
            {
                if (BlockSpawner1.Instance.currentPieces != null && BlockSpawner1.Instance.currentPieces.Length > 0 && BlockSpawner1.Instance.currentPieces[0] != null)
                {
                    startWorld = BlockSpawner1.Instance.currentPieces[0].transform.position;
                }
                else if (BlockSpawner1.Instance.trayContainer != null)
                {
                    startWorld = BlockSpawner1.Instance.trayContainer.TransformPoint(BlockSpawner1.Instance.GetSlotPosition(0));
                }
            }

            Vector3 targetWorld = Vector3.zero;
            if (GridManager1.Instance != null)
            {
                float halfCell = GridManager1.Instance.cellSize * 0.5f;
                targetWorld = GridManager1.Instance.CellToWorld(0, 0) + new Vector3(halfCell, halfCell, 0f);
            }

            Vector2 startCanvas = WorldToCanvasPos(startWorld);
            Vector2 targetCanvas = WorldToCanvasPos(targetWorld);

            // 1. Fade in
            handPointer.anchoredPosition = startCanvas;
            handPointer.localScale = Vector3.one * 1.15f;
            cg.alpha = 0f;

            float fadeElapsed = 0f;
            while (fadeElapsed < 0.2f)
            {
                fadeElapsed += Time.unscaledDeltaTime;
                cg.alpha = Mathf.Clamp01(fadeElapsed / 0.2f);
                yield return null;
            }

            // 2. Nhấn giữ
            float pressElapsed = 0f;
            while (pressElapsed < 0.2f)
            {
                pressElapsed += Time.unscaledDeltaTime;
                handPointer.localScale = Vector3.Lerp(Vector3.one * 1.15f, Vector3.one * 0.9f, pressElapsed / 0.2f);
                yield return null;
            }

            // 3. Kéo lướt lên bàn cờ
            float dragDuration = 1.1f;
            float dragElapsed = 0f;
            while (dragElapsed < dragDuration)
            {
                dragElapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(dragElapsed / dragDuration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                handPointer.anchoredPosition = Vector2.Lerp(startCanvas, targetCanvas, smoothT);
                yield return null;
            }

            // 4. Thả tay
            float releaseElapsed = 0f;
            while (releaseElapsed < 0.3f)
            {
                releaseElapsed += Time.unscaledDeltaTime;
                float t = releaseElapsed / 0.3f;
                handPointer.localScale = Vector3.Lerp(Vector3.one * 0.9f, Vector3.one * 1.1f, t);
                cg.alpha = 1f - t;
                yield return null;
            }

            yield return new WaitForSeconds(0.4f);
        }
    }

    public void OnFirstPiecePlaced()
    {
        if (activeTutorialLevel != 1 || currentStep != 2) return;
        currentStep = 3;

        if (handAnimCoroutine != null) StopCoroutine(handAnimCoroutine);
        if (handPointer != null) handPointer.gameObject.SetActive(false);

        if (dialogueText != null)
        {
            dialogueText.text = "<b>Tuyệt vời! Hãy đặt nốt món còn lại để phục vụ khách!</b>";
        }

        StartCoroutine(DismissRoutine(2.0f));
    }

    // ══════════════════════════════════════════════════════════════════════
    // LEVEL 2: DOUBLE TAP TO ROTATE (Làm tối màn hình + Focus món ăn)
    // ══════════════════════════════════════════════════════════════════════

    private IEnumerator StartLevel2TutorialRoutine()
    {
        yield return new WaitForSeconds(0.25f);
        currentStep = 1;

        if (darkOverlay != null)
        {
            darkOverlay.gameObject.SetActive(true);
            darkOverlay.color = new Color(0f, 0f, 0f, 0.65f);
            darkOverlay.raycastTarget = false;
        }

        if (dialogueBox != null)
        {
            dialogueBox.SetActive(true);
            if (dialogueBoxRt != null) dialogueBoxRt.anchoredPosition = new Vector2(0f, 480f);
        }

        if (dialogueText != null)
        {
            dialogueText.text = "<b>Chạm 2 lần liên tiếp (<color=#1D4ED8>Double Click</color>) vào món ăn để xoay hướng!</b>";
        }

        if (tapToContinueText != null) tapToContinueText.gameObject.SetActive(false);

        if (handAnimCoroutine != null) StopCoroutine(handAnimCoroutine);
        handAnimCoroutine = StartCoroutine(DoubleTapRotateGuideRoutine());
    }

    private IEnumerator DoubleTapRotateGuideRoutine()
    {
        if (handPointer == null) yield break;
        handPointer.gameObject.SetActive(true);
        handPointer.localRotation = Quaternion.identity;

        CanvasGroup cg = handPointer.GetComponent<CanvasGroup>();
        if (cg == null) cg = handPointer.gameObject.AddComponent<CanvasGroup>();

        while (activeTutorialLevel == 2 && currentStep == 1)
        {
            Vector3 pieceWorldPos = new Vector3(-2f, -6f, 0f);
            if (BlockSpawner1.Instance != null && BlockSpawner1.Instance.currentPieces != null && BlockSpawner1.Instance.currentPieces.Length > 0 && BlockSpawner1.Instance.currentPieces[0] != null)
            {
                pieceWorldPos = BlockSpawner1.Instance.currentPieces[0].transform.position;
            }

            Vector2 pieceCanvasPos = WorldToCanvasPos(pieceWorldPos);
            handPointer.anchoredPosition = pieceCanvasPos;
            cg.alpha = 1f;

            // Nhịp Tap 1
            float t1 = 0f;
            while (t1 < 0.12f)
            {
                t1 += Time.unscaledDeltaTime;
                handPointer.localScale = Vector3.Lerp(Vector3.one * 1.15f, Vector3.one * 0.85f, t1 / 0.12f);
                yield return null;
            }
            float t1b = 0f;
            while (t1b < 0.12f)
            {
                t1b += Time.unscaledDeltaTime;
                handPointer.localScale = Vector3.Lerp(Vector3.one * 0.85f, Vector3.one * 1.15f, t1b / 0.12f);
                yield return null;
            }

            yield return new WaitForSeconds(0.08f);

            // Nhịp Tap 2
            float t2 = 0f;
            while (t2 < 0.12f)
            {
                t2 += Time.unscaledDeltaTime;
                handPointer.localScale = Vector3.Lerp(Vector3.one * 1.15f, Vector3.one * 0.85f, t2 / 0.12f);
                yield return null;
            }
            float t2b = 0f;
            while (t2b < 0.12f)
            {
                t2b += Time.unscaledDeltaTime;
                handPointer.localScale = Vector3.Lerp(Vector3.one * 0.85f, Vector3.one * 1.15f, t2b / 0.12f);
                yield return null;
            }

            yield return new WaitForSeconds(1.4f);
        }
    }

    private void OnPieceRotatedHandler(BlockPiece1 piece)
    {
        if (activeTutorialLevel != 2 || currentStep != 1) return;
        currentStep = 2;

        if (handAnimCoroutine != null) StopCoroutine(handAnimCoroutine);
        if (handPointer != null) handPointer.gameObject.SetActive(false);
        if (darkOverlay != null) darkOverlay.gameObject.SetActive(false);

        if (dialogueText != null)
        {
            dialogueText.text = "<b>Tuyệt vời! Hãy xoay và kéo các món ăn vào bàn để phục vụ khách!</b>";
        }

        StartCoroutine(DismissRoutine(2.5f));
    }

    // ══════════════════════════════════════════════════════════════════════
    // LEVEL 8: ZONES MECHANIC (Cơ chế Vùng ảnh hưởng & Màu nền Khách hàng)
    // ══════════════════════════════════════════════════════════════════════

    private IEnumerator StartLevel8ZonesTutorialRoutine()
    {
        yield return new WaitForSeconds(0.25f);
        currentStep = 1;

        // Bước 1: Màn hình tối lại, focus vào hàng Khách hàng
        if (darkOverlay != null)
        {
            darkOverlay.gameObject.SetActive(true);
            darkOverlay.color = new Color(0f, 0f, 0f, 0.65f);
        }

        if (dialogueBox != null)
        {
            dialogueBox.SetActive(true);
            if (dialogueBoxRt != null) dialogueBoxRt.anchoredPosition = new Vector2(0f, -380f);
        }

        if (dialogueText != null)
        {
            dialogueText.text = "<b>Cơ chế mới: <color=#1D4ED8>Chia Khu Vực</color>!</b>\n<b>Mỗi khách hàng có <color=#B91C1C>màu nền Avatar</color> tương ứng với màu ô trên bàn cờ.</b>";
        }

        if (tapToContinueText != null) tapToContinueText.gameObject.SetActive(true);

        if (handPointer != null)
        {
            handPointer.gameObject.SetActive(true);
            if (handAnimCoroutine != null) StopCoroutine(handAnimCoroutine);
            handAnimCoroutine = StartCoroutine(PointCustomerRoutine());
        }
    }

    private void GoToLevel8_Step2_FocusZones()
    {
        currentStep = 2;
        if (darkOverlay != null) darkOverlay.gameObject.SetActive(false);

        if (dialogueBox != null)
        {
            dialogueBox.SetActive(true);
            if (dialogueBoxRt != null) dialogueBoxRt.anchoredPosition = new Vector2(0f, 480f);
        }

        if (dialogueText != null)
        {
            dialogueText.text = "<b>Món ăn chạm vào vùng nào sẽ phục vụ khách đó.\n<color=#1D4ED8>1 món ăn có thể phục vụ nhiều khách</color> nếu đặt đè lên các vùng khác nhau!</b>";
        }

        if (tapToContinueText != null) tapToContinueText.gameObject.SetActive(false);

        if (handAnimCoroutine != null) StopCoroutine(handAnimCoroutine);
        handAnimCoroutine = StartCoroutine(PointBoardZonesRoutine());

        StartCoroutine(DismissRoutine(4.5f));
    }

    private IEnumerator PointBoardZonesRoutine()
    {
        if (handPointer == null) yield break;
        handPointer.gameObject.SetActive(true);
        handPointer.localRotation = Quaternion.Euler(0f, 0f, -45f);

        Vector3 boardCenter = Vector3.zero;
        if (GridManager1.Instance != null)
        {
            boardCenter = GridManager1.Instance.CellToWorld(GridManager1.Instance.width / 2, GridManager1.Instance.height / 2);
        }

        Vector2 basePos = WorldToCanvasPos(boardCenter + new Vector3(0.5f, 0.5f, 0f));
        while (currentStep == 2 && activeTutorialLevel == 8)
        {
            float bob = Mathf.Sin(Time.unscaledTime * 5f) * 15f;
            handPointer.anchoredPosition = basePos + new Vector2(bob, bob);
            yield return null;
        }
    }

    private IEnumerator DismissRoutine(float delay = 2.0f)
    {
        yield return new WaitForSeconds(delay);
        if (dialogueBox != null) dialogueBox.SetActive(false);
        if (darkOverlay != null) darkOverlay.gameObject.SetActive(false);
        if (handPointer != null) handPointer.gameObject.SetActive(false);
        if (tutorialCanvas != null) tutorialCanvas.gameObject.SetActive(false);
    }

    private Vector2 WorldToCanvasPos(Vector3 worldPos)
    {
        if (mainCam == null) mainCam = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
        Vector2 screenPos = mainCam != null ? (Vector2)mainCam.WorldToScreenPoint(worldPos) : new Vector2(Screen.width / 2f, Screen.height / 2f);

        RectTransform canvasRect = tutorialCanvas != null ? tutorialCanvas.GetComponent<RectTransform>() : null;
        if (canvasRect == null) return screenPos;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, tutorialCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCam, out Vector2 localPoint);
        return localPoint;
    }
    // ══════════════════════════════════════════════════════════════════════
    // LEVEL 4: SWIPE TRAY (Lướt khay sang 2 bên)
    // ══════════════════════════════════════════════════════════════════════

    private IEnumerator StartLevel4TutorialRoutine()
    {
        yield return new WaitForSeconds(0.25f);
        currentStep = 1;

        if (darkOverlay != null)
        {
            darkOverlay.gameObject.SetActive(true);
            darkOverlay.color = new Color(0f, 0f, 0f, 0.65f);
            darkOverlay.raycastTarget = true; // Block raycasts to game until tapped or 5s
        }

        if (dialogueBox != null)
        {
            dialogueBox.SetActive(true);
            if (dialogueBoxRt != null) dialogueBoxRt.anchoredPosition = new Vector2(0f, 480f);
        }

        if (dialogueText != null)
        {
            dialogueText.text = "<b>Hãy lướt sang hai bên để xem tất cả món ăn</b>";
        }

        if (tapToContinueText != null) tapToContinueText.gameObject.SetActive(true);

        if (handAnimCoroutine != null) StopCoroutine(handAnimCoroutine);
        handAnimCoroutine = StartCoroutine(SwipeTrayGuideRoutine());

        // Dismiss sau 5s nếu người chơi không chạm màn hình
        StartCoroutine(DismissRoutine(5.0f));
    }

    private IEnumerator SwipeTrayGuideRoutine()
    {
        if (handPointer == null) yield break;
        handPointer.gameObject.SetActive(true);
        handPointer.localRotation = Quaternion.identity;

        CanvasGroup cg = handPointer.GetComponent<CanvasGroup>();
        if (cg == null) cg = handPointer.gameObject.AddComponent<CanvasGroup>();

        while (activeTutorialLevel == 4 && currentStep == 1)
        {
            Vector3 centerPosWorld = new Vector3(0f, -6f, 0f);
            if (BlockSpawner1.Instance != null && BlockSpawner1.Instance.trayContainer != null)
            {
                centerPosWorld.y = BlockSpawner1.Instance.trayContainer.position.y;
            }

            Vector3 rightPosWorld = centerPosWorld + new Vector3(2.5f, 0f, 0f);
            Vector3 leftPosWorld = centerPosWorld + new Vector3(-2.5f, 0f, 0f);

            Vector2 rightCanvas = WorldToCanvasPos(rightPosWorld);
            Vector2 leftCanvas = WorldToCanvasPos(leftPosWorld);

            // 1. Fade in
            handPointer.anchoredPosition = rightCanvas;
            handPointer.localScale = Vector3.one * 1.15f;
            cg.alpha = 0f;

            float fadeElapsed = 0f;
            while (fadeElapsed < 0.2f)
            {
                fadeElapsed += Time.unscaledDeltaTime;
                cg.alpha = Mathf.Clamp01(fadeElapsed / 0.2f);
                yield return null;
            }

            // 2. Press down
            float pressElapsed = 0f;
            while (pressElapsed < 0.2f)
            {
                pressElapsed += Time.unscaledDeltaTime;
                handPointer.localScale = Vector3.Lerp(Vector3.one * 1.15f, Vector3.one * 0.9f, pressElapsed / 0.2f);
                yield return null;
            }

            // 3. Swipe Left
            float dragDuration = 1.0f;
            float dragElapsed = 0f;
            while (dragElapsed < dragDuration)
            {
                dragElapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(dragElapsed / dragDuration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                handPointer.anchoredPosition = Vector2.Lerp(rightCanvas, leftCanvas, smoothT);
                yield return null;
            }

            // 4. Release
            float releaseElapsed = 0f;
            while (releaseElapsed < 0.2f)
            {
                releaseElapsed += Time.unscaledDeltaTime;
                float t = releaseElapsed / 0.2f;
                handPointer.localScale = Vector3.Lerp(Vector3.one * 0.9f, Vector3.one * 1.15f, t);
                yield return null;
            }

            // 5. Fade out
            float fadeOutElapsed = 0f;
            while (fadeOutElapsed < 0.2f)
            {
                fadeOutElapsed += Time.unscaledDeltaTime;
                cg.alpha = 1f - (fadeOutElapsed / 0.2f);
                yield return null;
            }

            yield return new WaitForSeconds(0.3f);
        }
    }
}
