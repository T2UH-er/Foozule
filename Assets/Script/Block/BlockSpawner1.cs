using System.Collections.Generic;
using UnityEngine;

public class BlockSpawner1 : MonoBehaviour
{
    public int levelNumber = 0;
    public static BlockSpawner1 Instance { get; private set; }

    public GameObject blockPiecePrefab;
    public List<Transform> spawnSlots = new List<Transform>();
    public LevelConfig[] levelConfigs;

    public Transform trayContainer;
    private Vector3 initialTrayPosition;
    public Vector3 trayBlockScale = new Vector3(0.6f, 0.6f, 0.6f);
    public int totalBlocksCount = 10;
    public float slotSpacing = 13.5f;

    [Header("Dynamic Spacing & Smooth Transition")]
    [Tooltip("Khoảng cách mặc định giữa các món khi số lượng >= 5")]
    public float defaultSpacing = 13.0f;
    [Tooltip("Khoảng cách khi còn đúng 2 món trên khay")]
    public float twoPieceSpacing = 14.5f;
    [Tooltip("Khoảng cách khi còn đúng 3 món trên khay")]
    public float threePieceSpacing = 13.0f;
    [Tooltip("Khoảng cách khi còn đúng 4 món trên khay")]
    public float fourPieceSpacing = 11.8f;
    [Tooltip("Tốc độ trượt dồn hàng mượt mà khi thêm/bớt món")]
    public float slideSpeed = 14.0f;

    [SerializeField] public BlockPiece1[] currentPieces;
    private List<BlockPiece1> activePieces = new List<BlockPiece1>();

    public int ActivePieceCount => activePieces.Count;
    public IReadOnlyList<BlockPiece1> ActivePieces => activePieces;
    public float CurrentSpacing => GetDynamicSpacing(activePieces.Count);

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (trayContainer != null)
            initialTrayPosition = trayContainer.localPosition;

        activePieces = new List<BlockPiece1>();
        currentPieces = new BlockPiece1[0];
    }

    public float GetDynamicSpacing(int count)
    {
        if (count <= 1) return defaultSpacing;
        if (count == 2) return twoPieceSpacing;
        if (count == 3) return threePieceSpacing;
        if (count == 4) return fourPieceSpacing;
        return defaultSpacing;
    }

    public Vector3 GetSlotPosition(int slotIndex, int totalCount)
    {
        if (totalCount <= 0) totalCount = 1;
        float spacing = GetDynamicSpacing(totalCount);
        float offsetX = (slotIndex - ((totalCount - 1) / 2f)) * spacing;
        return new Vector3(offsetX, 0f, 0f);
    }

    public Vector3 GetSlotPosition(int slotIndex)
    {
        int count = activePieces.Count > 0 ? activePieces.Count : totalBlocksCount;
        return GetSlotPosition(slotIndex, count);
    }

    private void Start()
    {
        ConfigureFlavor();
        SpawnAllSlotsInLevelConfig(levelConfigs);
    }

    private void Update()
    {
        UpdateActivePiecesPositions();
    }

    /// <summary>
    /// Tự động trượt mượt mà các khối món ăn về vị trí mới trong danh sách động (expand/tighten).
    /// </summary>
    private void UpdateActivePiecesPositions()
    {
        if (activePieces == null || activePieces.Count == 0) return;

        int count = activePieces.Count;
        bool anyMoved = false;

        for (int i = 0; i < count; i++)
        {
            BlockPiece1 piece = activePieces[i];
            if (piece == null) continue;

            // Đang được người chơi nhấc kéo thì không ép vị trí khay
            if (GameManager1.Instance != null && GameManager1.Instance.HeldPiece == piece)
                continue;

            Vector3 targetLocalPos = GetSlotPosition(i, count);
            if (Vector3.Distance(piece.transform.localPosition, targetLocalPos) > 0.005f)
            {
                piece.transform.localPosition = Vector3.Lerp(
                    piece.transform.localPosition,
                    targetLocalPos,
                    Time.deltaTime * slideSpeed
                );
                anyMoved = true;
            }
            else
            {
                piece.transform.localPosition = targetLocalPos;
            }
        }

        if (anyMoved)
        {
            Physics2D.SyncTransforms();
        }
    }

    public void SpawnAllSlotsInLevelConfig(LevelConfig[] configs)
    {
        if (configs == null || configs.Length == 0)
        {
            configs = Resources.FindObjectsOfTypeAll<LevelConfig>();
        }
        if (configs == null) return;

        foreach (var levelConfig in configs)
        {
            if (levelConfig == null || levelConfig.levelNumber != levelNumber) continue;

            activePieces.Clear();

            // 1. Sinh các khối món ăn từ trayBlocks theo thứ tự
            if (levelConfig.trayBlocks != null && levelConfig.trayBlocks.Count > 0)
            {
                var sortedBlocks = new List<LevelBlockSpawnConfig>(levelConfig.trayBlocks);
                sortedBlocks.Sort((a, b) => a.slotIndex.CompareTo(b.slotIndex));

                for (int i = 0; i < sortedBlocks.Count; i++)
                {
                    var blockCfg = sortedBlocks[i];
                    if (blockCfg == null || blockCfg.blockConfig == null || blockCfg.blockConfig.itemPerCell == null)
                        continue;

                    SpawnSinglePiece(blockCfg.blockConfig, i, blockCfg.flavorCounts);
                }
            }
            // ─── Tương thích ngược: Sinh từ asset đơn lẻ cũ ───────────────────────
            else if (levelConfig.blockConfig != null && levelConfig.blockConfig.itemPerCell != null)
            {
                SpawnSinglePiece(levelConfig.blockConfig, 0, levelConfig.flavorCounts);
            }

            totalBlocksCount = activePieces.Count;
            SyncCurrentPiecesArray();
            UpdatePieceLayout(instant: true);

            // 2. Tự động đồng bộ thanh cuộn khay & tự căn giữa nếu vừa màn hình
            if (TrayScrollManager1.Instance != null)
            {
                TrayScrollManager1.Instance.OnTrayContentChanged(instantCenter: true);
            }
            else
            {
                TrayScrollManager1 scrollMgr = FindObjectOfType<TrayScrollManager1>();
                if (scrollMgr != null)
                {
                    scrollMgr.OnTrayContentChanged(instantCenter: true);
                }
            }
            break;
        }
    }

    private void SpawnSinglePiece(BlockConfig blockConfig, int slotIndex, List<FlavorData> flavors)
    {
        // Clone ItemData để không ghi đè asset gốc
        ItemData1[] clonedItems = new ItemData1[blockConfig.itemPerCell.Length];
        for (int i = 0; i < blockConfig.itemPerCell.Length; i++)
        {
            if (blockConfig.itemPerCell[i] != null)
            {
                clonedItems[i] = Instantiate(blockConfig.itemPerCell[i]);
                if (flavors != null)
                {
                    clonedItems[i].flavorCounts = new List<FlavorData>(flavors);
                }
            }
        }

        GameObject pieceObj = Instantiate(blockPiecePrefab, trayContainer);
        pieceObj.transform.localScale = trayBlockScale;

        BlockPiece1 piece = pieceObj.GetComponent<BlockPiece1>();
        piece.InitializeCustom(blockConfig.shapeData, clonedItems, slotIndex);

        // Hiển thị Bubble vị trên món ăn
        if (GameManager1.Instance != null)
            GameManager1.Instance.SetupPieceBubble(piece);

        activePieces.Add(piece);
    }

    public void UpdatePieceLayout(bool instant = false)
    {
        int count = activePieces.Count;
        totalBlocksCount = count;

        for (int i = 0; i < count; i++)
        {
            if (activePieces[i] == null) continue;
            activePieces[i].slotIndex = i;

            if (instant)
            {
                activePieces[i].transform.localPosition = GetSlotPosition(i, count);
            }
        }

        if (instant)
        {
            Physics2D.SyncTransforms();
        }
    }

    private void SyncCurrentPiecesArray()
    {
        currentPieces = activePieces.ToArray();
    }

    /// <summary>
    /// Xóa toàn bộ các khối món ăn đang có trên khay và trong scene.
    /// </summary>
    public void ClearAllPieces()
    {
        activePieces.Clear();
        currentPieces = new BlockPiece1[0];

        // 1. Xóa toàn bộ GameObject con trong trayContainer
        if (trayContainer != null)
        {
            for (int i = trayContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(trayContainer.GetChild(i).gameObject);
            }
        }

        // 2. Quét sạch tất cả BlockPiece1 còn sót lại trong Scene
        BlockPiece1[] allPieces = FindObjectsOfType<BlockPiece1>();
        foreach (var piece in allPieces)
        {
            if (piece != null)
                Destroy(piece.gameObject);
        }
    }

    public void ConfigureFlavor()
    {
        // Đã được xử lý tự động và an toàn khi clone item trong SpawnSinglePiece
    }

    public void OnPiecePlaced(int slotIndex, BlockPiece1 piece = null)
    {
        // 1. Tìm và xóa khối vừa đặt khỏi danh sách món trên khay
        if (piece != null && activePieces.Contains(piece))
        {
            activePieces.Remove(piece);
        }
        else if (slotIndex >= 0 && slotIndex < activePieces.Count)
        {
            activePieces.RemoveAt(slotIndex);
        }
        else if (activePieces.Count > 0)
        {
            BlockPiece1 target = activePieces.Find(p => p != null && p.slotIndex == slotIndex);
            if (target != null) activePieces.Remove(target);
            else activePieces.RemoveAt(0);
        }

        // 2. Cập nhật lại chỉ số slotIndex cho các món còn lại
        for (int i = 0; i < activePieces.Count; i++)
        {
            if (activePieces[i] != null)
                activePieces[i].slotIndex = i;
        }

        SyncCurrentPiecesArray();
        totalBlocksCount = activePieces.Count;

        // 3. Kích hoạt hiệu ứng dồn khay (tighten / collapse) mượt mà
        UpdatePieceLayout(instant: false);

        if (TutorialManager1.Instance != null)
            TutorialManager1.Instance.OnFirstPiecePlaced();

        // 4. Nếu hết món thì khay về tâm ban đầu, nếu còn thì cập nhật giới hạn cuộn và tự căn giữa nếu vừa tầm mắt
        if (activePieces.Count == 0 && trayContainer != null)
        {
            trayContainer.localPosition = initialTrayPosition;
        }

        if (TrayScrollManager1.Instance != null)
        {
            TrayScrollManager1.Instance.OnTrayContentChanged(instantCenter: false);
        }
    }

    public bool TryReturnToTray(BlockPiece1 piece)
    {
        if (piece == null) return false;

        // Gắn lại vào trayContainer
        piece.transform.SetParent(trayContainer);
        piece.transform.localScale = trayBlockScale;

        // Thêm vào danh sách active
        activePieces.Add(piece);

        // Cập nhật lại slotIndex cho toàn bộ
        for (int i = 0; i < activePieces.Count; i++)
        {
            if (activePieces[i] != null)
                activePieces[i].slotIndex = i;
        }

        SyncCurrentPiecesArray();
        totalBlocksCount = activePieces.Count;

        // Kích hoạt dãn khay (expand) mượt mà
        UpdatePieceLayout(instant: false);

        if (TrayScrollManager1.Instance != null)
        {
            TrayScrollManager1.Instance.OnTrayContentChanged(instantCenter: false);
        }

        Physics2D.SyncTransforms();
        return true;
    }
}
