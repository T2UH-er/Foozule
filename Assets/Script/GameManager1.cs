using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GameManager1 — Trung tâm điều phối toàn bộ luồng game.
/// Chịu trách nhiệm: kéo thả khối, đếm lượt đi, và xây dựng Bubble Sprite.
/// InputManager1 chỉ detect input rồi ủy quyền cho class này.
/// </summary>
public class GameManager1 : MonoBehaviour
{
    public static GameManager1 Instance { get; private set; }

    [Header("Block Piece Prefab")]
    public GameObject blockPiecePrefab;

    [Header("Drag Settings")]
    public float dragScaleMultiplier = 1.0f;

    // ─── Trạng thái kéo thả ───────────────────────────────────────────────
    public BlockPiece1 HeldPiece { get; private set; }

    private bool isFromGrid = false;
    private Vector2Int originalOriginCell;
    private ItemData1[] originalItems;
    private BlockShapeData originalShape;
    private Vector3 startWorldPosition;

    [Header("Level Flow")]
    public static int selectedLevelIndex = -1;
    public int currentLevelIndex = 1;

    // ─── Sprite Resources ─────────────────────────────────────────────────
    private SpriteFlavor spriteFlavor;
    private SpriteNumber spriteNumber;

    // ─── Unity Lifecycle ──────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        spriteFlavor = Resources.Load<SpriteFlavor>("SpriteFlavor");
        if (spriteFlavor == null) Debug.LogError("[GameManager1] Không tìm thấy SpriteFlavor.asset trong Resources/");

        spriteNumber = Resources.Load<SpriteNumber>("SpriteNumber");
        if (spriteNumber == null) Debug.LogError("[GameManager1] Không tìm thấy SpriteNumber.asset trong Resources/");
    }

    private void Start()
    {
        AudioManager.Instance.PlayAudio("bgm");
        // 1. Ưu tiên lấy từ menu chọn màn (selectedLevelIndex)
        if (selectedLevelIndex > 0)
        {
            currentLevelIndex = selectedLevelIndex;
            selectedLevelIndex = -1;
        }
        // 2. Nếu test trực tiếp trong Scene: Lấy trực tiếp từ biến levelNumber của BlockSpawner1 như trước
        else if (BlockSpawner1.Instance != null)
        {
            currentLevelIndex = BlockSpawner1.Instance.levelNumber;
        }

        LoadLevel(currentLevelIndex);

        if (FindObjectOfType<TutorialManager1>() == null)
        {
            GameObject tutObj = new GameObject("TutorialManager1");
            tutObj.AddComponent<TutorialManager1>();
        }
    }

    // ─── Level Management API ─────────────────────────────────────────────

    /// <summary>
    /// Chuyển sang level tiếp theo: Tăng level hiện tại lên 1, dọn bàn cờ và spawn lại toàn bộ dữ liệu mới.
    /// Có thể gán trực tiếp vào Button OnClick() trong Unity UI.
    /// </summary>
    public void NextLevel()
    {
        LoadLevel(currentLevelIndex + 1);
    }

    public void LoadCurrentLevel()
    {
        LoadLevel(currentLevelIndex);
    }

    /// <summary>
    /// Nạp dữ liệu level theo index (levelNumber).
    /// </summary>
    public void LoadLevel(int targetLevel)
    {
        Time.timeScale = 1f;
        currentLevelIndex = targetLevel;
        Debug.Log($"[GameManager1] === Loading Level {targetLevel} ===");

        // 0. Tim LevelConfig tuong ung de lay kich thuoc Grid
        LevelConfig levelCfg = null;
        if (BlockSpawner1.Instance?.levelConfigs != null)
        {
            foreach (var cfg in BlockSpawner1.Instance.levelConfigs)
            {
                if (cfg != null && cfg.levelNumber == targetLevel)
                {
                    levelCfg = cfg;
                    break;
                }
            }
        }
        // Fallback: tim trong toan bo asset da load
        if (levelCfg == null)
        {
            LevelConfig[] all = Resources.FindObjectsOfTypeAll<LevelConfig>();
            foreach (var cfg in all)
            {
                if (cfg != null && cfg.levelNumber == targetLevel)
                {
                    levelCfg = cfg;
                    break;
                }
            }
        }

        // 1. Resize Grid neu LevelConfig co chi dinh kich thuoc moi
        if (GridManager1.Instance != null)
        {
            GridManager1.Instance.ClearAllGrid();

            if (levelCfg != null && levelCfg.gridWidth > 0 && levelCfg.gridHeight > 0)
            {
                GridManager1.Instance.ResizeGrid(levelCfg.gridWidth, levelCfg.gridHeight);
                Debug.Log($"[GameManager1] Grid resized to {levelCfg.gridWidth}x{levelCfg.gridHeight}, cellSize={GridManager1.Instance.cellSize}");
            }
            
            // Cập nhật các ô bị chặn (chốt chặn)
            if (levelCfg != null && levelCfg.blockedCells != null)
            {
                GridManager1.Instance.SetBlockedCells(levelCfg.blockedCells);
            }
            else
            {
                GridManager1.Instance.SetBlockedCells(new List<Vector2Int>()); // Reset
            }
        }

        // 2. Spawn lai mon an tren khay
        if (BlockSpawner1.Instance != null)
        {
            BlockSpawner1.Instance.levelNumber = targetLevel;
            BlockSpawner1.Instance.ClearAllPieces();
            BlockSpawner1.Instance.SpawnAllSlotsInLevelConfig(BlockSpawner1.Instance.levelConfigs);
        }

        // 3. Spawn lai khach hang
        if (CustomerSpawner1.Instance != null)
        {
            CustomerSpawner1.Instance.levelNumber = targetLevel;
            CustomerSpawner1.Instance.SpawnCustomersForLevel(targetLevel);
        }

        // 4. Reset diem so va luot di
        if (ScoringSystem1.Instance != null)
        {
            ScoringSystem1.Instance.ResetGameStatus();
        }

        // 5. Kích hoạt hướng dẫn (TutorialManager tự xử lý theo level)
        if (TutorialManager1.Instance != null)
        {
            TutorialManager1.Instance.CheckAndStartLevelTutorial(targetLevel);
        }
        else
        {
            GameObject tutObj = new GameObject("TutorialManager1");
            tutObj.AddComponent<TutorialManager1>();
        }
    }

    // ─── API cho InputManager1 ────────────────────────────────────────────

    /// <summary>Bốc khối đang nằm trên Khay.</summary>
    public void BeginPickFromTray(BlockPiece1 piece)
    {
        if (GridManager1.Instance != null) GridManager1.Instance.ClearPlacementHighlight();
        HeldPiece = piece;
        startWorldPosition = piece.transform.position;
        isFromGrid = false;

        float gridScaleRatio = GridManager1.Instance != null ? GridManager1.Instance.ScaleRatio : 1f;
        HeldPiece.transform.localScale = Vector3.one * dragScaleMultiplier * gridScaleRatio;
    }

    /// <summary>Bốc khối đang nằm trên Grid.</summary>
    public void BeginPickFromGrid(PlacedBlockInfo1 info)
    {
        if (GridManager1.Instance != null) GridManager1.Instance.ClearPlacementHighlight();
        originalShape = info.shapeData;
        originalOriginCell = info.originCell;
        originalItems = info.itemPerCell;

        startWorldPosition = GridManager1.Instance.CellToWorld(originalOriginCell.x, originalOriginCell.y);

        // Xóa khỏi Grid (tự trừ flavor trong totalFlavorCounts)
        GridManager1.Instance.RemovePlacedBlock(originalShape, originalOriginCell);

        // Sinh khối tạm để kéo
        GameObject pieceObj = Instantiate(blockPiecePrefab, startWorldPosition, Quaternion.identity);
        HeldPiece = pieceObj.GetComponent<BlockPiece1>();
        HeldPiece.currentRotationAngle = info.rotationAngle;
        HeldPiece.InitializeCustom(originalShape, originalItems, -1);
        SetupPieceBubble(HeldPiece);

        isFromGrid = true;
        
        float gridScaleRatio = GridManager1.Instance != null ? GridManager1.Instance.ScaleRatio : 1f;
        HeldPiece.transform.localScale = Vector3.one * dragScaleMultiplier * gridScaleRatio;
    }

    public void UpdateDrag(Vector3 targetWorldPos)
    {
        if (HeldPiece == null) return;
        HeldPiece.transform.position = targetWorldPos;
        Physics2D.SyncTransforms();

        if (GridManager1.Instance != null && HeldPiece.shapeData != null)
        {
            Vector2Int targetCell = GridManager1.Instance.WorldToCell(HeldPiece.transform.position);
            if (GridManager1.Instance.CanPlace(HeldPiece.shapeData, targetCell))
            {
                GridManager1.Instance.HighlightPlacement(HeldPiece.shapeData, targetCell);
            }
            else
            {
                GridManager1.Instance.ClearPlacementHighlight();
            }
        }
    }

    /// <summary>Xử lý thả khối — quyết định đặt, trả về, hay hủy.</summary>
    public void EndDrag(Vector3 releaseWorldPos)
    {
        if (HeldPiece == null) return;

        if (GridManager1.Instance != null)
        {
            GridManager1.Instance.ClearPlacementHighlight();
        }

        Vector2Int targetCell = GridManager1.Instance.WorldToCell(HeldPiece.transform.position);

        if (GridManager1.Instance.CanPlace(HeldPiece.shapeData, targetCell))
        {
            // ── Đặt hợp lệ vào Grid ──────────────────────────────────────
            bool isNewPosition = !isFromGrid || (targetCell != originalOriginCell);

            GridManager1.Instance.PlaceBlock(
                HeldPiece.shapeData, targetCell, HeldPiece.itemPerCell, HeldPiece.cellIconPrefab, HeldPiece.currentRotationAngle);

            if (isNewPosition && ScoringSystem1.Instance != null)
                ScoringSystem1.Instance.availableMoves--;

            // Thông báo cho Spawner nếu khối đến từ khay
            if (HeldPiece.slotIndex >= 0 && BlockSpawner1.Instance != null)
                BlockSpawner1.Instance.OnPiecePlaced(HeldPiece.slotIndex, HeldPiece);

            Destroy(HeldPiece.gameObject);

            // Kiểm tra win/lose sau khi đặt khối
            if (ScoringSystem1.Instance != null) ScoringSystem1.Instance.NotifyGridChanged();
        }
        else if (isFromGrid && releaseWorldPos.y < -2.0f)
        {
            // ── Thả vào vùng Khay (y thấp hơn -2) ───────────────────────
            bool returnedToTray = BlockSpawner1.Instance != null
                && BlockSpawner1.Instance.TryReturnToTray(HeldPiece);

            if (returnedToTray)
            {
                // Thành công → tính 1 lượt di chuyển
                if (ScoringSystem1.Instance != null)
                    ScoringSystem1.Instance.availableMoves--;
                // Grid đã thay đổi (bỏ khối đi): kiểm tra lại
                if (ScoringSystem1.Instance != null) ScoringSystem1.Instance.NotifyGridChanged();
            }
            else
            {
                // Khay đầy → hoàn trả về Grid cũ, không tính lượt
                GridManager1.Instance.PlaceBlock(
                    HeldPiece.shapeData, originalOriginCell, originalItems, HeldPiece.cellIconPrefab, HeldPiece.currentRotationAngle);
                Destroy(HeldPiece.gameObject);
                if (ScoringSystem1.Instance != null) ScoringSystem1.Instance.NotifyGridChanged();
            }
        }
        else if (isFromGrid)
        {
            // ── Tha khong hop le tren Grid → tra ve vi tri cu, khong tinh luot ──
            GridManager1.Instance.PlaceBlock(
                HeldPiece.shapeData, originalOriginCell, originalItems, HeldPiece.cellIconPrefab, HeldPiece.currentRotationAngle);
            Destroy(HeldPiece.gameObject);
            if (ScoringSystem1.Instance != null) ScoringSystem1.Instance.NotifyGridChanged();
        }
        else
        {
            // ── Từ Khay, thả không hợp lệ → trả về Khay ─────────────────
            HeldPiece.transform.position = startWorldPosition;
            HeldPiece.transform.localScale = BlockSpawner1.Instance != null
                ? BlockSpawner1.Instance.trayBlockScale
                : new Vector3(0.6f, 0.6f, 0.6f);
        }

        HeldPiece = null;
        isFromGrid = false;
    }

    // ─── Bubble Helpers (dùng chung cho Spawner, Customer) ───────────────

    /// <summary>Xây danh sách Sprite (số + vị) từ ItemData1. Tối đa 4 sprite (lưới 2×2).</summary>
    public List<Sprite> BuildBubbleSprites(ItemData1 item)
    {
        var sprites = new List<Sprite>();
        if (item == null || item.flavorCounts == null) return sprites;

        string[] flavorNames = { "sour", "spicy", "salty", "sweet", "bitter", "umami", "buttery" };
        foreach (string flv in flavorNames)
        {
            int count = item.GetFlavorCount(flv);
            if (count <= 0) continue;
            if (sprites.Count + 2 > 4) break;
            // Them Icon Vi truoc (cot trai), Icon So sau (cot phai)
            if (spriteFlavor != null) sprites.Add(spriteFlavor.GetSprite(flv));
            if (spriteNumber != null) sprites.Add(spriteNumber.GetSprite(count));
        }
        return sprites;
    }

    /// <summary>Xây danh sách Sprite từ List&lt;FlavorData&gt; (dùng cho Customer1).</summary>
    public List<Sprite> BuildBubbleSpritesFromRequirement(List<FlavorData> requirements)
    {
        var sprites = new List<Sprite>();
        if (requirements == null) return sprites;

        string[] flavorNames = { "sour", "spicy", "salty", "sweet", "bitter", "umami", "buttery" };
        foreach (string flv in flavorNames)
        {
            var fd = requirements.Find(f => f.flavorName == flv);
            if (fd.count <= 0) continue;
            if (sprites.Count + 2 > 4) break;
            // Them Icon Vi truoc (cot trai), Icon So sau (cot phai)
            if (spriteFlavor != null) sprites.Add(spriteFlavor.GetSprite(flv));
            if (spriteNumber != null) sprites.Add(spriteNumber.GetSprite(fd.count));
        }
        return sprites;
    }

    /// <summary>Tìm Bubble trên khối và setup sprite vị.</summary>
    public void SetupPieceBubble(BlockPiece1 piece)
    {
        if (piece == null || piece.itemPerCell == null || piece.itemPerCell.Length == 0) return;
        Bubble bubble = piece.GetComponentInChildren<Bubble>();
        if (bubble == null) return;
        bubble.SetupBubble(BuildBubbleSprites(piece.itemPerCell[0]));
    }
}
