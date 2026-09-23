using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quản lý dữ liệu và hiển thị của 1 Khách hàng trong game.
/// Gồm Avatar nhân vật (hiển thị rõ nét) + Bong bóng yêu cầu (Bubble dàn ngang) + Vùng ảnh hưởng (affectedZone).
/// </summary>
public class Customer1 : MonoBehaviour
{
    private GridManager1 gridManager;
    public bool isRequirementMatched = false;

    [Header("UI Components")]
    [SerializeField] private Bubble bubble;
    [SerializeField] private SpriteRenderer avatarRenderer;

    [Header("Layout & Color Settings")]
    [Tooltip("Chiều cao hiển thị của Avatar (Unity Unit)")]
    public float avatarHeight = 2.0f;

    [Tooltip("Độ đậm màu của vùng ảnh hưởng tô lên khung thoại (0 = màu gốc, 1 = màu vùng 100%)")]
    [Range(0f, 1f)]
    public float bubbleColorTint = 0.55f;

    [Tooltip("Vị trí của Bubble thoại so với tâm của Khách")]
    public Vector3 bubbleOffset = new Vector3(0f, 1.8f, 0f);

    [Tooltip("Tỷ lệ scale của Bubble")]
    public float bubbleScale = 0.65f;

    [Header("Customer Config Data")]
    public List<Vector2Int> affectedZone = new List<Vector2Int>();
    public List<FlavorData> requirement = new List<FlavorData>()
    {
        new FlavorData { flavorName = "sour", count = 0 },
        new FlavorData { flavorName = "spicy", count = 0 },
        new FlavorData { flavorName = "salty", count = 0 },
        new FlavorData { flavorName = "sweet", count = 0 },
        new FlavorData { flavorName = "bitter", count = 0 },
        new FlavorData { flavorName = "umami", count = 0 },
        new FlavorData { flavorName = "buttery", count = 0 }
    };
    public Color zoneColor = new Color(0.2f, 0.6f, 1.0f, 0.35f);

    // ─── Affected Zone Visuals ────────────────────────────────────────────
    private List<GameObject> zoneOverlays = new List<GameObject>();
    private static Sprite _whiteSquareSprite;

    [Header("Affected Zone Display")]
    [Tooltip("Điều chỉnh kích thước overlay so với 1 ô lưới (1.0 = khớp chính xác)")]
    public float overlayScale = 1.0f;

    private bool isInitialized = false;

    // ─── Unity Lifecycle ──────────────────────────────────────────────────

    void Awake()
    {
        EnsureComponents();
    }

    void Start()
    {
        if (gridManager == null) gridManager = GridManager1.Instance;

        if (!isInitialized)
        {
            SetupVisuals();
        }
    }

    private void OnEnable()
    {
        if (bubble != null) bubble.ShowBubble();
    }

    private void EnsureComponents()
    {
        // 1. Tìm hoặc tạo GameObject con chứa Avatar
        if (avatarRenderer == null)
        {
            Transform avatarT = transform.Find("Avatar");
            if (avatarT != null)
            {
                avatarRenderer = avatarT.GetComponent<SpriteRenderer>();
            }
            if (avatarRenderer == null)
            {
                avatarRenderer = GetComponent<SpriteRenderer>();
            }
            if (avatarRenderer == null)
            {
                GameObject avatarObj = new GameObject("Avatar");
                avatarObj.transform.SetParent(transform, false);
                avatarObj.transform.localPosition = Vector3.zero;
                avatarRenderer = avatarObj.AddComponent<SpriteRenderer>();
            }
        }

        // 2. Tìm Bubble con
        if (bubble == null)
        {
            bubble = GetComponentInChildren<Bubble>();
        }
    }

    // ─── Khởi tạo từ CustomerConfig (Level) ───────────────────────────────

    public void Initialize(CustomerConfig config)
    {
        EnsureComponents();
        gridManager = GridManager1.Instance;

        if (config != null)
        {
            // 1. Gán Avatar Sprite (giữ nguyên vị trí và scale trong Prefab)
            if (avatarRenderer != null && config.customerSprite != null)
            {
                avatarRenderer.sprite = config.customerSprite;
                avatarRenderer.sortingOrder = 25;
            }

            // 2. Gán Yêu cầu món ăn và Vùng ảnh hưởng
            if (config.requirement != null)
                this.requirement = new List<FlavorData>(config.requirement);

            if (config.affectedZone != null)
                this.affectedZone = new List<Vector2Int>(config.affectedZone);

            this.zoneColor = config.zoneColor;
        }

        SetupVisuals();
        isInitialized = true;
    }

    private void SetupVisuals()
    {
        EnsureComponents();

        // 1. Setup Nội dung & Đổi màu Bubble theo màu vùng ảnh hưởng (zoneColor)
        if (bubble != null)
        {
            SpriteRenderer bubbleSR = bubble.GetComponent<SpriteRenderer>();
            if (bubbleSR != null)
            {
                Color solidColor = new Color(zoneColor.r, zoneColor.g, zoneColor.b, 1.0f);
                // Pha nhẹ với màu trắng để khung thoại mang màu sắc đặc trưng của khách nhưng vẫn sáng đẹp
                bubbleSR.color = Color.Lerp(Color.white, solidColor, bubbleColorTint);
            }

            if (GameManager1.Instance != null)
            {
                bubble.SetupBubble(GameManager1.Instance.BuildBubbleSpritesFromRequirement(requirement));
            }
        }

        // 2. Vẽ Overlay vùng ảnh hưởng trên Grid
        CreateZoneOverlays();
    }

    /// <summary>Tính lại trạng thái hoàn thành yêu cầu. Được gọi khi Grid thay đổi.</summary>
    public void RefreshRequirementStatus()
    {
        isRequirementMatched = IsMatchRequirement();
    }

    public int GetRequirementCount(string flavorName)
    {
        if (requirement == null) return 0;
        int index = requirement.FindIndex(f => string.Equals(f.flavorName?.Trim(), flavorName?.Trim(), System.StringComparison.OrdinalIgnoreCase));
        return index != -1 ? requirement[index].count : 0;
    }

    // ─── Zone Overlay ─────────────────────────────────────────────────────

    private static Sprite GetWhiteSquareSprite()
    {
        if (_whiteSquareSprite != null) return _whiteSquareSprite;
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        _whiteSquareSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0f, 0f), 1f);
        return _whiteSquareSprite;
    }

    public void CreateZoneOverlays()
    {
        foreach (var go in zoneOverlays)
            if (go != null) Destroy(go);
        zoneOverlays.Clear();

        if (gridManager == null) gridManager = GridManager1.Instance;
        if (gridManager == null || affectedZone == null || affectedZone.Count == 0) return;

        Sprite whiteSquare = GetWhiteSquareSprite();
        float cellSize = gridManager.cellSize * overlayScale;

        foreach (Vector2Int cell in affectedZone)
        {
            Vector3 worldPos = gridManager.CellToWorld(cell.x, cell.y);

            GameObject overlay = new GameObject($"ZoneOverlay_{cell.x}_{cell.y}");
            overlay.transform.position = worldPos;
            overlay.transform.localScale = new Vector3(cellSize, cellSize, 1f);

            SpriteRenderer sr = overlay.AddComponent<SpriteRenderer>();
            sr.sprite = whiteSquare;
            sr.color = this.zoneColor;
            sr.sortingOrder = 2;

            zoneOverlays.Add(overlay);
        }
    }

    /// <summary>
    /// Tăng độ sáng và độ phủ màu của ô thuộc vùng khách hàng khi có khối thức ăn rê qua,
    /// và khôi phục lại màu zoneColor gốc khi khối rời đi.
    /// Giúp người chơi nhận diện rõ ô sẽ đặt mà không bị đổi màu vùng đặc trưng của khách.
    /// </summary>
    public void SetZoneCellHighlighted(Vector2Int cell, bool highlighted)
    {
        if (affectedZone == null || zoneOverlays == null) return;
        int index = affectedZone.IndexOf(cell);
        if (index < 0 || index >= zoneOverlays.Count) return;

        GameObject overlay = zoneOverlays[index];
        if (overlay == null) return;

        SpriteRenderer sr = overlay.GetComponent<SpriteRenderer>();
        if (sr == null) return;

        if (highlighted)
        {
            // Tăng độ sáng và độ rực rỡ (RGB) và độ rõ (Alpha) của chính màu khách
            Color brightColor = new Color(
                Mathf.Min(1f, zoneColor.r * 1.35f + 0.18f),
                Mathf.Min(1f, zoneColor.g * 1.35f + 0.18f),
                Mathf.Min(1f, zoneColor.b * 1.35f + 0.18f),
                Mathf.Min(0.88f, Mathf.Max(0.72f, zoneColor.a * 2.2f))
            );
            sr.color = brightColor;
        }
        else
        {
            sr.color = this.zoneColor;
        }
    }

    private void OnDestroy()
    {
        foreach (var go in zoneOverlays)
            if (go != null) Destroy(go);
        zoneOverlays.Clear();
    }

    // ─── Logic Kiểm Tra Thỏa Mãn Yêu Cầu ──────────────────────────────────

    public bool IsMatchRequirement()
    {
        int sour = 0, spicy = 0, salty = 0, sweet = 0, bitter = 0, umami = 0, buttery = 0;

        if (gridManager == null) gridManager = GridManager1.Instance;

        if (affectedZone != null && affectedZone.Count > 0)
        {
            HashSet<Vector2Int> processedOriginCells = new HashSet<Vector2Int>();

            foreach (Vector2Int cell in affectedZone)
            {
                PlacedBlockInfo1 info = gridManager != null ? gridManager.GetPlacedBlockInfoAt(cell.x, cell.y) : null;
                if (info == null) continue;

                if (processedOriginCells.Contains(info.originCell)) continue;
                processedOriginCells.Add(info.originCell);

                if (info.itemPerCell != null && info.itemPerCell.Length > 0 && info.itemPerCell[0] != null)
                {
                    foreach (var flavor in info.itemPerCell[0].flavorCounts ?? new List<FlavorData>())
                    {
                        if (string.IsNullOrEmpty(flavor.flavorName)) continue;
                        switch (flavor.flavorName.Trim().ToLower())
                        {
                            case "sour": sour += flavor.count; break;
                            case "spicy": spicy += flavor.count; break;
                            case "salty": salty += flavor.count; break;
                            case "sweet": sweet += flavor.count; break;
                            case "bitter": bitter += flavor.count; break;
                            case "umami": umami += flavor.count; break;
                            case "buttery": buttery += flavor.count; break;
                        }
                    }
                }
            }
        }
        else if (gridManager != null && gridManager.totalFlavorCounts != null)
        {
            sour = gridManager.totalFlavorCounts.ContainsKey("sour") ? gridManager.totalFlavorCounts["sour"] : 0;
            spicy = gridManager.totalFlavorCounts.ContainsKey("spicy") ? gridManager.totalFlavorCounts["spicy"] : 0;
            salty = gridManager.totalFlavorCounts.ContainsKey("salty") ? gridManager.totalFlavorCounts["salty"] : 0;
            sweet = gridManager.totalFlavorCounts.ContainsKey("sweet") ? gridManager.totalFlavorCounts["sweet"] : 0;
            bitter = gridManager.totalFlavorCounts.ContainsKey("bitter") ? gridManager.totalFlavorCounts["bitter"] : 0;
            umami = gridManager.totalFlavorCounts.ContainsKey("umami") ? gridManager.totalFlavorCounts["umami"] : 0;
            buttery = gridManager.totalFlavorCounts.ContainsKey("buttery") ? gridManager.totalFlavorCounts["buttery"] : 0;
        }

        // Từng khách hàng kiểm tra xem yêu cầu của chính mình đã được thỏa mãn chưa
        return sour >= GetRequirementCount("sour") &&
               spicy >= GetRequirementCount("spicy") &&
               salty >= GetRequirementCount("salty") &&
               sweet >= GetRequirementCount("sweet") &&
               bitter >= GetRequirementCount("bitter") &&
               umami >= GetRequirementCount("umami") &&
               buttery >= GetRequirementCount("buttery");
    }

    /// <summary>
    /// Được gọi khi người chơi click/chạm vào khách hàng trên màn hình (thông qua New Input System).
    /// </summary>
    public void OnCustomerTapped()
    {
        if (bubble != null) bubble.ShowBubble();
    }

    private void OnMouseDown()
    {
        OnCustomerTapped();
    }
}
