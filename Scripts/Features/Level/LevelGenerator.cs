using System;
using System.Collections.Generic;
using UnityEngine;
public enum SplitAxis
{
    None,
    Vertical,
    Horizontal
}
public enum CellKind
{
    Void,
    Floor,
    Wall,
    DoorVertical,
    DoorHorizontal
}

public enum RoomKind
{
    Start,
    Boss,
    Treasure,
    Shop,
    Normal,
    Unassigned
}

public sealed class BSPNode
{
    public RectInt area;
    public BSPNode left;
    public BSPNode right;
    public SplitAxis splitAxis;
    public int splitLine;
    public int roomId;
    public BSPNode(RectInt area)
    {
        this.area = area;
        left = null;
        right = null;
        splitAxis = SplitAxis.None;
        splitLine = -1;
        roomId = -1;
    }
    public bool IsLeaf
    {
        get { return left == null && right == null; }
    }
}
public sealed class RoomVertex
{
    public int id;
    public BSPNode leaf;
    public RectInt area;
    public Vector2 center;
    public RoomKind kind = RoomKind.Unassigned;
    public int distanceFromStart = -1;
    public bool isMainPath;
    public int mainPathIndex = -1;
}
public sealed class RoomEdge
{
    public int id;
    public int roomA;
    public int roomB;
    public SplitAxis axis;
    public readonly List<Vector2Int> doorCandidates = new();
    public Vector2Int doorCell;
    public float weight;
}
public sealed class RoomTopology
{
    public readonly List<RoomVertex> rooms = new List<RoomVertex>();
    public readonly List<RoomEdge> edges = new List<RoomEdge>();
    public readonly Dictionary<int, List<int>> adjacency = new Dictionary<int, List<int>>();
    public void AddRoom(RoomVertex room)
    {
        rooms.Add(room);
        adjacency.Add(room.id, new List<int>());
    }
    public void AddEdge(RoomEdge edge)
    {
        edge.id = edges.Count;
        edges.Add(edge);
        adjacency[edge.roomA].Add(edge.id);
        adjacency[edge.roomB].Add(edge.id);
    }
    public int GetOtherRoom(int edgeId, int roomId)
    {
        RoomEdge edge = edges[edgeId];
        return edge.roomA == roomId ? edge.roomB : edge.roomA;
    }
}
public sealed class SelectedTopology
{
    public readonly HashSet<int> roomIds = new HashSet<int>();
    public readonly HashSet<int> edgeIds = new HashSet<int>();
    public readonly List<int> mainPath = new List<int>();
}
public struct TileSpawn
{
    public GameObject prefab;
    public float rotationY;
    public bool shouldSpawn;
    public TileSpawn(GameObject prefab, float rotationY)
    {
        this.prefab = prefab;
        this.rotationY = rotationY;
        shouldSpawn = prefab != null;
    }
    public static TileSpawn None
    {
        get
        {
            TileSpawn result = new TileSpawn();
            result.prefab = null;
            result.rotationY = 0f;
            result.shouldSpawn = false;
            return result;
        }
    }
}
public class LevelGenerator : MonoBehaviour
{
    [Serializable]
    public struct LevelPrefabs
    {
        public GameObject floor;
        public GameObject wall;
        public GameObject corner;
        public GameObject innerCorner;
        public GameObject door;
        public GameObject diagonal;
        public GameObject solid;
    }
    private sealed class ConnectionCandidate
    {
        public int roomA;
        public int roomB;
        public readonly List<Vector2Int> doorCells = new List<Vector2Int>();
    }
    private sealed class TraversalResult
    {
        public int farthestRoom;
        public float farthestDistance;
        public readonly Dictionary<int, int> parentRoom = new Dictionary<int, int>();
        public readonly Dictionary<int, int> parentEdge = new Dictionary<int, int>();
        public readonly Dictionary<int, float> distance = new Dictionary<int, float>();
    }
    private sealed class BranchCandidate
    {
        public int anchorRoom;
        public readonly List<int> roomIds = new List<int>();
        public readonly List<int> edgeIds = new List<int>();
        public float weight;
        public float orderingScore;
    }
    private sealed class SpecialRoomCandidate
    {
        public int roomId;
        public float score;
    }
    [Header("Prefabs")]
    public LevelPrefabs levelPrefabs;
    [Header("BSP")]
    public int seed = 1;
    public int width = 40;
    public int height = 40;
    public int maxDepth = 5;
    public int minLeafSize = 6;
    public float maxAspectRatio = 1.5f;
    public float minAspectRatio = -1.5f;
    [Header("Topology")]
    [Min(0)] public int deadEndCount = 3;
    [Min(1)] public int minDeadEndLength = 1;
    [Min(1)] public int maxDeadEndLength = 4;
    [Tooltip("0 means no room limit. The complete main path is always preserved.")]
    [Min(0)] public int maxSelectedRooms = 0;
    [Min(0f)] public float distanceWeight = 0.05f;
    [Min(0f)] public float branchRandomness = 0.35f;
    [Header("Grid")]
    public Vector3 cellSize = Vector3.one;
    public bool forceXZGrid = true;
    [Header("Debug")]
    public bool logUnsupportedTilePatterns = true;
    private Grid unityGrid;
    private BSPNode root;
    private System.Random rng;
    private System.Random roomRoleRng;
    private CellKind[,] map;
    private Transform generatedRoot;
    private RoomTopology topology;
    private SelectedTopology selectedTopology;
    public float skeletonHeight = 0.5f;
    public float hubRadius = 0.5f;
    public float entranceRadius = 0.5f;
    [Header("Room settigns")]
    public float hubCenterWeight = .25f;
    public int doorCenterDispersion = 0;
    [Header("Room roles")]
    public Vector2Int shopRoomCount;
    public Vector2Int treasureRoomCount;
    public int minSpecialRoomDistance = 0;
    public List<RoomTemplateConfig> roomTemplates;
    private Dictionary<int, RoomContentPlan> roomContentPlans;

    private void Awake()
    {
        unityGrid = GetComponent<Grid>();
        GenerateLevel();
    }
    [ContextMenu("Generate Level")]
    public void GenerateLevel()
    {
        rng = new System.Random(seed);

        CreateEmptyMap();
        
        RectInt rootArea = new RectInt(1, 1, width - 2, height - 2);
        root = new BSPNode(rootArea);
        
        Split(root, 0);
        
        topology = BuildRoomTopology(root);
        selectedTopology = SelectTopology(topology);
        roomRoleRng = new System.Random(CombinedSeed(seed, 0x5d4f45d));
        AssignRoomKinds(topology, selectedTopology);
        RasterizeSelectedTopology(topology, selectedTopology);

        var roomGenerator = new RoomGenerator(seed, roomTemplates);
        RoomGenerator.HubCenterWeight = hubCenterWeight;
        roomContentPlans = roomGenerator.Generate(topology, selectedTopology);
        Build3D();
    }

    public void AssignRoomKinds(RoomTopology source, SelectedTopology selected)
    {
        if (selected.roomIds.Count == 0) return;
        if (selected.mainPath.Count == 0) return;
        var startRoomId = selected.mainPath[0];
        var bossRoomId = selected.mainPath[selected.mainPath.Count - 1];
        MarkMainPath(source, selected);
        source.rooms[startRoomId].kind = RoomKind.Start;
        source.rooms[bossRoomId].kind = RoomKind.Boss;
        CalculateSelectedDistance(source, selected, startRoomId);

        foreach(var roomId in selected.roomIds)
        {
            var room = source.rooms[roomId];
            if (room.kind == RoomKind.Unassigned)
            {
                source.rooms[roomId].kind = RoomKind.Normal;
            }

        }

        AssignSpecialRooms(source, selected, startRoomId, bossRoomId);
    }
    private void AssignSpecialRooms(RoomTopology source, SelectedTopology selected, int roomStartId, int bossRoomId)
    {
        var candidates = new List<SpecialRoomCandidate>();
        foreach(var roomId in selected.roomIds)
        {
            if (roomId == roomStartId || roomId == bossRoomId) continue;
            var room = source.rooms[roomId];
            if (room.isMainPath) continue;
            if (!room.leaf.IsLeaf) continue;

            int degree = 0;
            var edges = source.adjacency[roomId];
            foreach (var edgeId in edges)
            {
                if (!selected.edgeIds.Contains(edgeId))
                {
                    continue;
                }
                var otherRoomId = source.GetOtherRoom(edgeId, roomId);
                if (selected.roomIds.Contains(otherRoomId)) degree++;
            }

            if (degree != 1) continue;
            //if (room.distanceFromStart < minSpecialRoomDistance) continue;
            var candidate = new SpecialRoomCandidate();
            candidate.roomId = roomId;
            candidate.score = room.distanceFromStart;
            candidates.Add(candidate);
        }
        candidates.Sort((a, b) => a.score.CompareTo(b.score));

        if (candidates.Count == 0) return;

        var treasureCount = NextInclusive(roomRoleRng, treasureRoomCount.x, treasureRoomCount.y);
        var shopCount = NextInclusive(roomRoleRng, shopRoomCount.x, shopRoomCount.y);

        treasureCount = (int)(treasureCount * candidates.Count / (treasureCount + shopCount));
        shopCount = (int)(shopCount * candidates.Count / (treasureCount + shopCount));

        var specialCount = treasureCount + shopCount;
        specialCount = Mathf.Min(specialCount, candidates.Count);
    
        var minSpecialCount = Mathf.Min(treasureCount, shopCount);
        var minKind = minSpecialCount == treasureCount ? RoomKind.Treasure : RoomKind.Shop;
        var otherKind = !(minSpecialCount == treasureCount) ? RoomKind.Shop : RoomKind.Treasure;
        
        for (int i = 0; i < specialCount && i < candidates.Count; i++)
        {
            var id = candidates[i].roomId;
            if (i < treasureCount)
            {
                source.rooms[id].kind = RoomKind.Treasure;
            }
            else
            {
                source.rooms[id].kind = RoomKind.Shop;
            }
        }
    }

    private static int NextInclusive(System.Random rng, int minimum, int maximum)
    {
        var min = Mathf.Max (0, Mathf.Min(minimum, maximum));
        var max = Mathf.Max(0, Mathf.Max(minimum, maximum));
        return rng.Next (min, max + 1);
    }

    private void MarkMainPath(RoomTopology source, SelectedTopology selected)
    {
        for (int i = 0; i < selected.mainPath.Count; i++)
        {
            var room = source.rooms[selected.mainPath[i]];
            room.isMainPath = true;
            room.mainPathIndex = i;
        }
    }
    private void OnDrawGizmos()
    {
        if (topology == null || selectedTopology == null)
        {
            return;
        }

        var grid = unityGrid;
        if (grid == null)
        {
            grid = GetComponent<Grid>();
        }
        foreach(var roomId in selectedTopology.roomIds)
        { 
            var room = topology.rooms[roomId];
            var center = GetRoomWorldCenter(grid, room.area);
            Gizmos.color = GetRoomColor(room.kind);
            Gizmos.DrawWireCube(center, new Vector3(room.area.width * cellSize.x, 0.2f, room.area.height * cellSize.y));
        }
        Gizmos.color = Color.magenta;
        foreach (var edgeId in selectedTopology.edgeIds)
        {
            var edge = topology.edges[edgeId];
            var A = GetRoomWorldCenter(grid, topology.rooms[edge.roomA].area);
            var B = GetRoomWorldCenter(grid, topology.rooms[edge.roomB].area);
            Gizmos.DrawLine(A, B);
        }
        DrawRoomSkeletonGizmos();
    }

    private void DrawRoomSkeletonGizmos()
    {
        if (roomContentPlans == null) return;
        if (topology == null) return;
        if (unityGrid == null) return;

        foreach (var pair in roomContentPlans)
        {
            var roomId = pair.Key;
            var plan = pair.Value;
            var room = topology.rooms[roomId];
            for (int x = 0; x < plan.size.x; x++)
            {
                for (int y = 0; y < plan.size.y; y++)
                {
                    var contentCell = plan.cells[x, y];
                    if (contentCell == ContentCell.Free) continue;
                    switch (contentCell)
                    {
                        case ContentCell.DoorClearance:
                            Gizmos.color = Color.cyan;
                            break;
                        case ContentCell.Navigation:
                            Gizmos.color = Color.green;
                            break;
                        case ContentCell.Obstacle:
                            Gizmos.color = Color.red;
                            break;
                        case ContentCell.Reserved:
                            Gizmos.color = Color.yellow;
                            break;
                        default:
                            continue;
                    }
                    int globalX = room.area.xMin + x;
                    int globalY = room.area.yMin + y;

                    var world = unityGrid.GetCellCenterWorld(new Vector3Int(globalX, globalY, 0));
                    world.y += skeletonHeight;
                    Gizmos.DrawCube(world, new Vector3(cellSize.x, 0.08f, cellSize.y));

                }
            }
        }

    }

    private Vector3 RoomLocalToWorld(RoomVertex room, Vector2Int localCell)
    {
        var globalX = room.area.xMin + localCell.x;
        var globalY = room.area.yMin + localCell.y;
        var globalCell = new Vector3Int(globalX, globalY, 0);
        return unityGrid.GetCellCenterLocal(globalCell);
    }

    private Vector3 GetRoomWorldCenter(Grid grid, RectInt area)
    {
        var minimumCell = new Vector3Int(area.xMin, area.yMin, 0);
        var maximumCell = new Vector3Int(area.xMax, area.yMax, 0);

        var minimum = grid.GetCellCenterWorld(minimumCell);  
        var maximum = grid.GetCellCenterWorld(maximumCell);

        return (minimum + maximum) / 2;
    }

    private static Color GetRoomColor(RoomKind kind)
    {
        switch (kind)
        {
            case RoomKind.Start:
                return Color.green;
            case RoomKind.Boss:
                return Color.red;
            case RoomKind.Treasure:
                return Color.yellow;
            case RoomKind.Shop:
                return Color.cyan;
            case RoomKind.Normal:
                return Color.white;
            default:
                return Color.gray;
        }
    }

    private void CalculateSelectedDistance(RoomTopology source, SelectedTopology selected, int roomStartId)
    {
        Queue<int> queue = new();

        source.rooms[roomStartId].distanceFromStart = 0;
        
        queue.Enqueue(roomStartId);

        while (queue.Count != 0)
        {
            var curent = queue.Dequeue();
            var curentRoom = source.rooms[curent];
            var edges = source.adjacency[curent];
            foreach(var edgeId in edges)
            {
                if (!selected.edgeIds.Contains(edgeId))
                {
                    continue;
                }

                var nextRoomId = source.GetOtherRoom(edgeId, curent);

                if (!selected.roomIds.Contains(nextRoomId))
                {
                    continue;
                }
                var nextRoom = source.rooms[nextRoomId];
                if (nextRoom.distanceFromStart >= 0)
                {
                    continue;
                }
                nextRoom.distanceFromStart = curentRoom.distanceFromStart + 1;
                queue.Enqueue(nextRoomId);
            }
        }
    }

    private void CreateEmptyMap()
    {
        map = new CellKind[width, height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                map[x, y] = CellKind.Void;
            }
        }
    }

    //1 комната субграфа start
    //2 дальняя boss
    // branches листья - спец комнаты(магазин, трежерка)
    private static int CombinedSeed(int levelseed, int roomId)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + levelseed;
            hash = hash * 31 + roomId;
            return hash;
        }
    }
    private RoomTopology BuildRoomTopology(BSPNode rootNode)
    {
        RoomTopology result = new RoomTopology();
        AssignRoomIds(rootNode, result);
        BuildTopologyEdges(rootNode, result);
        return result;
    }
    private void AssignRoomIds(BSPNode node, RoomTopology result)
    {
        if (node == null)
        {
            return;
        }
        if (node.IsLeaf)
        {
            int roomId = result.rooms.Count;
            node.roomId = roomId;
            RoomVertex vertex = new RoomVertex();
            vertex.id = roomId;
            vertex.leaf = node;
            vertex.area = node.area;
            vertex.center = node.area.center;
            result.AddRoom(vertex);
            return;
        }
        AssignRoomIds(node.left, result);
        AssignRoomIds(node.right, result);
    }
    private void BuildTopologyEdges(
        BSPNode node,
        RoomTopology result)
    {
        if (node == null || node.IsLeaf)
        {
            return;
        }
        BuildTopologyEdges(node.left, result);
        BuildTopologyEdges(node.right, result);
        RoomEdge edge = CreateConnectionForSplit(node, result);
        if (edge != null)
        {
            result.AddEdge(edge);
        }
        else
        {
            Debug.LogError(
                "Failed to connect BSP subtrees for split " +
                node.splitAxis + " at line " + node.splitLine + ".",
                this
            );
        }
    }
    private RoomEdge CreateConnectionForSplit(
        BSPNode node,
        RoomTopology result)
    {
        if (node.splitAxis == SplitAxis.Vertical)
        {
            return CreateVerticalConnection(node, result);
        }
        if (node.splitAxis == SplitAxis.Horizontal)
        {
            return CreateHorizontalConnection(node, result);
        }
        return null;
    }
    private RoomEdge CreateVerticalConnection(
        BSPNode node,
        RoomTopology result)
    {
        int wallX = node.splitLine;
        List<BSPNode> leftLeaves = new List<BSPNode>();
        List<BSPNode> rightLeaves = new List<BSPNode>();
        WalkLeaves(node.left, delegate (RectInt room)
        {
            return room.xMax == wallX;
        }, leftLeaves);
        WalkLeaves(node.right, delegate (RectInt room)
        {
            return room.xMin == wallX + 1;
        }, rightLeaves);
        List<ConnectionCandidate> candidates = new List<ConnectionCandidate>();
        for (int i = 0; i < leftLeaves.Count; i++)
        {
            BSPNode left = leftLeaves[i];
            for (int j = 0; j < rightLeaves.Count; j++)
            {
                BSPNode right = rightLeaves[j];
                int overlapMin = Mathf.Max(left.area.yMin, right.area.yMin);
                int overlapMax = Mathf.Min(left.area.yMax, right.area.yMax);
                AddVerticalDoorCandidates(
                    candidates,
                    left.roomId,
                    right.roomId,
                    wallX,
                    overlapMin,
                    overlapMax
                );
            }
        }
        ConnectionCandidate candidate = ChooseConnectionCandidate(candidates);
        if (candidate == null)
        {
            return null;
        }
        return CreateRoomEdge(
            result,
            candidate.roomA,
            candidate.roomB,
            SplitAxis.Vertical,
            candidate.doorCells
        );
    }
    private RoomEdge CreateHorizontalConnection(
        BSPNode node,
        RoomTopology result)
    {
        int wallY = node.splitLine;
        List<BSPNode> bottomLeaves = new List<BSPNode>();
        List<BSPNode> topLeaves = new List<BSPNode>();
        WalkLeaves(node.left, delegate (RectInt room)
        {
            return room.yMax == wallY;
        }, bottomLeaves);
        WalkLeaves(node.right, delegate (RectInt room)
        {
            return room.yMin == wallY + 1;
        }, topLeaves);
        List<ConnectionCandidate> candidates = new List<ConnectionCandidate>();
        for (int i = 0; i < bottomLeaves.Count; i++)
        {
            BSPNode bottom = bottomLeaves[i];
            for (int j = 0; j < topLeaves.Count; j++)
            {
                BSPNode top = topLeaves[j];
                int overlapMin = Mathf.Max(bottom.area.xMin, top.area.xMin);
                int overlapMax = Mathf.Min(bottom.area.xMax, top.area.xMax);
                AddHorizontalDoorCandidates(
                    candidates,
                    bottom.roomId,
                    top.roomId,
                    wallY,
                    overlapMin,
                    overlapMax
                );
            }
        }
        ConnectionCandidate candidate = ChooseConnectionCandidate(candidates);
        if (candidate == null)
        {
            return null;
        }
        return CreateRoomEdge(
            result,
            candidate.roomA,
            candidate.roomB,
            SplitAxis.Horizontal,
            candidate.doorCells
        );
    }
    private void AddVerticalDoorCandidates(
        List<ConnectionCandidate> candidates,
        int roomA,
        int roomB,
        int wallX,
        int overlapMin,
        int overlapMax)
    {
        List<int> coordinates = GetCoordinatesWhoseTwoCellChunkFits(
            overlapMin,
            overlapMax
        );
        if (coordinates.Count == 0)
        {
            return;
        }
        ConnectionCandidate candidate = new ConnectionCandidate();
        candidate.roomA = roomA;
        candidate.roomB = roomB;
        for (int i = 0; i < coordinates.Count; i++)
        {
            candidate.doorCells.Add(new Vector2Int(wallX, coordinates[i]));
        }
        candidates.Add(candidate);
    }
    private void AddHorizontalDoorCandidates(
        List<ConnectionCandidate> candidates,
        int roomA,
        int roomB,
        int wallY,
        int overlapMin,
        int overlapMax)
    {
        List<int> coordinates = GetCoordinatesWhoseTwoCellChunkFits(
            overlapMin,
            overlapMax
        );
        if (coordinates.Count == 0)
        {
            return;
        }
        ConnectionCandidate candidate = new ConnectionCandidate();
        candidate.roomA = roomA;
        candidate.roomB = roomB;
        for (int i = 0; i < coordinates.Count; i++)
        {
            candidate.doorCells.Add(new Vector2Int(coordinates[i], wallY));
        }
        candidates.Add(candidate);
    }
    private List<int> GetCoordinatesWhoseTwoCellChunkFits(
        int minInclusive,
        int maxExclusive)
    {
        List<int> result = new List<int>();
        int firstChunkStart = minInclusive;
        if ((firstChunkStart & 1) != 0)
        {
            firstChunkStart++;
        }
        for (int chunkStart = firstChunkStart;
             chunkStart + 1 < maxExclusive;
             chunkStart += 2)
        {
            result.Add(chunkStart);
            result.Add(chunkStart + 1);
        }
        return result;
    }
    private ConnectionCandidate ChooseConnectionCandidate(
        List<ConnectionCandidate> candidates)
    {
        if (candidates.Count == 0)
        {
            return null;
        }
        return candidates[rng.Next(0, candidates.Count)];
    }
    private static Vector2Int GetChunkOrigin(Vector2Int cell)
    {
        return new Vector2Int(cell.x & ~1, cell.y & ~1);
    }
    private RoomEdge CreateRoomEdge(
        RoomTopology result,
        int roomA,
        int roomB,
        SplitAxis axis,
        List<Vector2Int> doorCandidates)
    {
        Vector2 centerA = result.rooms[roomA].center;
        Vector2 centerB = result.rooms[roomB].center;
        RoomEdge edge = new RoomEdge();
        edge.roomA = roomA;
        edge.roomB = roomB;
        edge.axis = axis;
        edge.weight = 1f + Vector2.Distance(centerA, centerB) * distanceWeight;
        for (int i = 0; i < doorCandidates.Count; i++)
        {
            edge.doorCandidates.Add(doorCandidates[i]);
        }
        return edge;
    }
    private void WalkLeaves(
        BSPNode node,
        Predicate<RectInt> predicate,
        List<BSPNode> result)
    {
        if (node == null)
        {
            return;
        }
        if (node.IsLeaf)
        {
            if (predicate(node.area))
            {
                result.Add(node);
            }
            return;
        }
        WalkLeaves(node.left, predicate, result);
        WalkLeaves(node.right, predicate, result);
    }
    private SelectedTopology SelectTopology(RoomTopology source)
    {
        SelectedTopology selected = new SelectedTopology();
        if (source.rooms.Count == 0)
        {
            return selected;
        }
        if (source.rooms.Count == 1)
        {
            selected.roomIds.Add(0);
            selected.mainPath.Add(0);
            return selected;
        }
        TraversalResult firstPass = TraverseTree(source, 0);
        TraversalResult secondPass = TraverseTree(source, firstPass.farthestRoom);
        List<int> mainPathRooms;
        List<int> mainPathEdges;
        ReconstructPath(
            firstPass.farthestRoom,
            secondPass.farthestRoom,
            secondPass.parentRoom,
            secondPass.parentEdge,
            out mainPathRooms,
            out mainPathEdges
        );
        for (int i = 0; i < mainPathRooms.Count; i++)
        {
            selected.mainPath.Add(mainPathRooms[i]);
            selected.roomIds.Add(mainPathRooms[i]);
        }
        for (int i = 0; i < mainPathEdges.Count; i++)
        {
            selected.edgeIds.Add(mainPathEdges[i]);
        }
        HashSet<int> mainPathSet = new HashSet<int>(mainPathRooms);
        List<BranchCandidate> branches = CollectBranchCandidates(
            source,
            mainPathRooms,
            mainPathSet
        );
        branches.Sort(delegate (BranchCandidate a, BranchCandidate b)
        {
            return b.orderingScore.CompareTo(a.orderingScore);
        });
        int addedBranches = 0;
        for (int i = 0; i < branches.Count; i++)
        {
            if (addedBranches >= deadEndCount)
            {
                break;
            }
            BranchCandidate branch = branches[i];
            int availableRooms = GetRemainingRoomBudget(selected.roomIds.Count);
            if (availableRooms == 0)
            {
                break;
            }
            int roomsToTake = branch.roomIds.Count;
            roomsToTake = Mathf.Min(roomsToTake, maxDeadEndLength);
            if (availableRooms > 0)
            {
                roomsToTake = Mathf.Min(roomsToTake, availableRooms);
            }
            if (roomsToTake < minDeadEndLength)
            {
                continue;
            }
            for (int roomIndex = 0; roomIndex < roomsToTake; roomIndex++)
            {
                selected.roomIds.Add(branch.roomIds[roomIndex]);
                selected.edgeIds.Add(branch.edgeIds[roomIndex]);
            }
            addedBranches++;
        }
        return selected;
    }
    private int GetRemainingRoomBudget(int currentlySelected)
    {
        if (maxSelectedRooms <= 0)
        {
            return int.MaxValue;
        }
        int remaining = maxSelectedRooms - currentlySelected;
        return Mathf.Max(0, remaining);
    }
    private TraversalResult TraverseTree(RoomTopology source, int startRoom)
    {
        TraversalResult result = new TraversalResult();
        Stack<int> stack = new Stack<int>();
        stack.Push(startRoom);
        result.parentRoom[startRoom] = -1;
        result.parentEdge[startRoom] = -1;
        result.distance[startRoom] = 0f;
        result.farthestRoom = startRoom;
        result.farthestDistance = 0f;
        while (stack.Count > 0)
        {
            int roomId = stack.Pop();
            float roomDistance = result.distance[roomId];
            if (roomDistance > result.farthestDistance)
            {
                result.farthestDistance = roomDistance;
                result.farthestRoom = roomId;
            }
            List<int> adjacentEdges = source.adjacency[roomId];
            for (int i = 0; i < adjacentEdges.Count; i++)
            {
                int edgeId = adjacentEdges[i];
                int otherRoom = source.GetOtherRoom(edgeId, roomId);
                if (result.parentRoom.ContainsKey(otherRoom))
                {
                    continue;
                }
                result.parentRoom[otherRoom] = roomId;
                result.parentEdge[otherRoom] = edgeId;
                result.distance[otherRoom] = roomDistance + source.edges[edgeId].weight;
                stack.Push(otherRoom);
            }
        }
        return result;
    }
    private void ReconstructPath(
        int startRoom,
        int endRoom,
        Dictionary<int, int> parentRoom,
        Dictionary<int, int> parentEdge,
        out List<int> pathRooms,
        out List<int> pathEdges)
    {
        pathRooms = new List<int>();
        pathEdges = new List<int>();
        int current = endRoom;
        pathRooms.Add(current);
        while (current != startRoom)
        {
            int edgeId = parentEdge[current];
            int parent = parentRoom[current];
            pathEdges.Add(edgeId);
            current = parent;
            pathRooms.Add(current);
        }
        pathRooms.Reverse();
        pathEdges.Reverse();
    }
    private List<BranchCandidate> CollectBranchCandidates(
        RoomTopology source,
        List<int> mainPathRooms,
        HashSet<int> mainPathSet)
    {
        List<BranchCandidate> result = new List<BranchCandidate>();
        for (int pathIndex = 0; pathIndex < mainPathRooms.Count; pathIndex++)
        {
            int anchor = mainPathRooms[pathIndex];
            List<int> adjacentEdges = source.adjacency[anchor];
            for (int edgeIndex = 0; edgeIndex < adjacentEdges.Count; edgeIndex++)
            {
                int firstEdgeId = adjacentEdges[edgeIndex];
                int firstRoom = source.GetOtherRoom(firstEdgeId, anchor);
                if (mainPathSet.Contains(firstRoom))
                {
                    continue;
                }
                BranchCandidate branch = FindLongestBranch(
                    source,
                    anchor,
                    firstRoom,
                    firstEdgeId,
                    mainPathSet
                );
                if (branch.roomIds.Count >= minDeadEndLength)
                {
                    branch.orderingScore =
                        branch.weight +
                        (float)rng.NextDouble() * branchRandomness;
                    result.Add(branch);
                }
            }
        }
        return result;
    }
    private BranchCandidate FindLongestBranch(
        RoomTopology source,
        int anchor,
        int firstRoom,
        int firstEdgeId,
        HashSet<int> blockedRooms)
    {
        Dictionary<int, int> parentRoom = new Dictionary<int, int>();
        Dictionary<int, int> parentEdge = new Dictionary<int, int>();
        Dictionary<int, float> distance = new Dictionary<int, float>();
        Stack<int> stack = new Stack<int>();
        parentRoom[firstRoom] = anchor;
        parentEdge[firstRoom] = firstEdgeId;
        distance[firstRoom] = source.edges[firstEdgeId].weight;
        stack.Push(firstRoom);
        int farthestRoom = firstRoom;
        float farthestDistance = distance[firstRoom];
        while (stack.Count > 0)
        {
            int roomId = stack.Pop();
            float roomDistance = distance[roomId];
            if (roomDistance > farthestDistance)
            {
                farthestDistance = roomDistance;
                farthestRoom = roomId;
            }
            List<int> adjacentEdges = source.adjacency[roomId];
            for (int i = 0; i < adjacentEdges.Count; i++)
            {
                int edgeId = adjacentEdges[i];
                int otherRoom = source.GetOtherRoom(edgeId, roomId);
                if (otherRoom == anchor || blockedRooms.Contains(otherRoom))
                {
                    continue;
                }
                if (parentRoom.ContainsKey(otherRoom))
                {
                    continue;
                }
                parentRoom[otherRoom] = roomId;
                parentEdge[otherRoom] = edgeId;
                distance[otherRoom] = roomDistance + source.edges[edgeId].weight;
                stack.Push(otherRoom);
            }
        }
        BranchCandidate result = new BranchCandidate();
        result.anchorRoom = anchor;
        result.weight = farthestDistance;
        int current = farthestRoom;
        while (current != anchor)
        {
            result.roomIds.Add(current);
            result.edgeIds.Add(parentEdge[current]);
            current = parentRoom[current];
        }
        result.roomIds.Reverse();
        result.edgeIds.Reverse();
        return result;
    }
    private void RasterizeSelectedTopology(
        RoomTopology source,
        SelectedTopology selected)
    {
        CreateEmptyMap();
        foreach (int roomId in selected.roomIds)
        {
            CarveRoom(source.rooms[roomId].area);
        }
        BuildWallsAroundFloors();
        HashSet<Vector2Int> usedDoorChunks = new HashSet<Vector2Int>();
        foreach (int edgeId in selected.edgeIds)
        {
            CarveDoor(source.edges[edgeId], usedDoorChunks);
        }
    }
    private void CarveRoom(RectInt room)
    {
        for (int y = room.yMin; y < room.yMax; y++)
        {
            for (int x = room.xMin; x < room.xMax; x++)
            {
                if (IsInsideMap(x, y))
                {
                    map[x, y] = CellKind.Floor;
                }
            }
        }
    }
    private void BuildWallsAroundFloors()
    {
        List<Vector2Int> floorCells = new List<Vector2Int>();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (map[x, y] == CellKind.Floor)
                {
                    floorCells.Add(new Vector2Int(x, y));
                }
            }
        }
        for (int i = 0; i < floorCells.Count; i++)
        {
            Vector2Int floor = floorCells[i];
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    if (offsetX == 0 && offsetY == 0)
                    {
                        continue;
                    }
                    int x = floor.x + offsetX;
                    int y = floor.y + offsetY;
                    if (IsInsideMap(x, y) && map[x, y] == CellKind.Void)
                    {
                        map[x, y] = CellKind.Wall;
                    }
                }
            }
        }
    }
    private void CarveDoor(
        RoomEdge edge,
        HashSet<Vector2Int> usedDoorChunks)
    {
        Vector2Int? selectedCell = ChooseDoorCell(edge, usedDoorChunks);
        if (!selectedCell.HasValue)
        {
            Debug.LogError(
                "No valid door position for edge " + edge.id +
                " between rooms " + edge.roomA + " and " + edge.roomB + ".",
                this
            );
            return;
        }
        Vector2Int cell = selectedCell.Value;
        edge.doorCell = cell;
        usedDoorChunks.Add(GetChunkOrigin(cell));
        map[cell.x, cell.y] = edge.axis == SplitAxis.Vertical
            ? CellKind.DoorVertical
            : CellKind.DoorHorizontal;
    }
    private Vector2Int? ChooseDoorCell(
        RoomEdge edge,
        HashSet<Vector2Int> usedDoorChunks)
    {
        int bestScore = int.MinValue;
        List<Vector2Int> bestCells = new List<Vector2Int>();
        for (int i = 0; i < edge.doorCandidates.Count; i++)
        {
            Vector2Int cell = edge.doorCandidates[i];
            if (!IsInsideMap(cell.x, cell.y))
            {
                continue;
            }
            if (map[cell.x, cell.y] != CellKind.Wall)
            {
                continue;
            }
            Vector2Int chunk = GetChunkOrigin(cell);
            int score = ScoreDoorCell(cell, chunk, usedDoorChunks);

            score += ScoreDoorCenter(edge, cell) + rng.Next(0, doorCenterDispersion);

            if (score > bestScore)
            {
                bestScore = score;
                bestCells.Clear();
                bestCells.Add(cell);
            }
            else if (score == bestScore)
            {
                bestCells.Add(cell);
            }
        }
        if (bestCells.Count == 0)
        {
            return null;
        }
        return bestCells[rng.Next(0, bestCells.Count)];
    }

    private int ScoreDoorCenter(RoomEdge edge, Vector2Int cell)
    {
        var a = topology.rooms[edge.roomA].area;
        var b = topology.rooms[edge.roomB].area;

        float center;
        float distance;
        if (edge.axis == SplitAxis.Vertical)
        {
            int min = Mathf.Min(a.yMin, b.yMin);
            int max = Mathf.Min(a.yMax, b.yMax);
            center = (min + max) / 2f;
            distance = Mathf.Abs(center - cell.y);
        }
        else
        {
            int min = Mathf.Min(a.xMin, b.xMin);
            int max = Mathf.Min(a.xMax, b.xMax);
            center = (min + max) / 2f;
            distance = Mathf.Abs(center - cell.x);
        }

        return Mathf.RoundToInt(-distance * 10f);
    }

    private int ScoreDoorCell(
        Vector2Int cell,
        Vector2Int chunk,
        HashSet<Vector2Int> usedDoorChunks)
    {
        int score = 0;
        if (!usedDoorChunks.Contains(chunk))
        {
            score += 100;
        }
        CellKind c1 = map[chunk.x, chunk.y];
        CellKind c2 = map[chunk.x + 1, chunk.y];
        CellKind c3 = map[chunk.x, chunk.y + 1];
        CellKind c4 = map[chunk.x + 1, chunk.y + 1];
        int floorMask = BuildFloorMask(c1, c2, c3, c4);
        if (floorMask == 0x3 ||
            floorMask == 0xC ||
            floorMask == 0x5 ||
            floorMask == 0xA)
        {
            score += 1000;
        }
        int wallMask = BuildWallMask(c1, c2, c3, c4);
        if (CountBits(wallMask) == 2)
        {
            score += 100;
        }
        return score;
    }
    private static int CountBits(int value)
    {
        int count = 0;
        while (value != 0)
        {
            count += value & 1;
            value >>= 1;
        }
        return count;
    }
    private bool IsInsideMap(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }
    public void Build3D()
    {
        if (map == null)
        {
            return;
        }
        if (unityGrid == null)
        {
            unityGrid = GetComponent<Grid>();
        }
        GridInit();
        ClearGeneratedObjects();
        GameObject rootObject = new GameObject("Generated Level");
        rootObject.transform.SetParent(transform, false);
        generatedRoot = rootObject.transform;
        for (int y = 0; y < height - 1; y += 2)
        {
            for (int x = 0; x < width - 1; x += 2)
            {
                CellKind c1 = map[x, y];
                CellKind c2 = map[x + 1, y];
                CellKind c3 = map[x, y + 1];
                CellKind c4 = map[x + 1, y + 1];
                TileSpawn tile = GuessPrefab(c1, c2, c3, c4, x, y);
                if (!tile.shouldSpawn || tile.prefab == null)
                {
                    continue;
                }
                Spawn(tile.prefab, x, y, tile.rotationY);
            }
        }
    }
    private TileSpawn GuessPrefab(
        CellKind c1,
        CellKind c2,
        CellKind c3,
        CellKind c4,
        int chunkX,
        int chunkY)
    {
        if (c1 == CellKind.Void &&
            c2 == CellKind.Void &&
            c3 == CellKind.Void &&
            c4 == CellKind.Void)
        {
            return TileSpawn.None;
        }
        if (IsDoor(c1) || IsDoor(c2) || IsDoor(c3) || IsDoor(c4))
        {
            return GuessDoorPrefab(c1, c2, c3, c4, chunkX, chunkY);
        }
        int wallMask = BuildWallMask(c1, c2, c3, c4);
        switch (wallMask)
        {
            case 0x0:
                return new TileSpawn(levelPrefabs.floor, 0f);
            // Two adjacent wall cells: straight wall.
            case 0x3:
                return new TileSpawn(levelPrefabs.wall, 180f);
            case 0xA:
                return new TileSpawn(levelPrefabs.wall, 90f);
            case 0xC:
                return new TileSpawn(levelPrefabs.wall, 0f);
            case 0x5:
                return new TileSpawn(levelPrefabs.wall, 270f);
            // Three wall cells and one open quadrant: outer corner.
            case 0x7:
                return new TileSpawn(levelPrefabs.corner, 270f);
            case 0xB:
                return new TileSpawn(levelPrefabs.corner, 180f);
            case 0xD:
                return new TileSpawn(levelPrefabs.corner, 0f);
            case 0xE:
                return new TileSpawn(levelPrefabs.corner, 90f);
            // One wall cell and three open quadrants: inner corner / T-ending.
            case 0x1:
                return CreateInnerCorner(90f, wallMask, chunkX, chunkY);
            case 0x2:
                return CreateInnerCorner(0f, wallMask, chunkX, chunkY);
            case 0x4:
                return CreateInnerCorner(180f, wallMask, chunkX, chunkY);
            case 0x8:
                return CreateInnerCorner(270f, wallMask, chunkX, chunkY);
            // Diagonal marching-squares ambiguity.
            case 0x6:
                return CreateDiagonal(0f, wallMask, chunkX, chunkY);
            case 0x9:
                return CreateDiagonal(90f, wallMask, chunkX, chunkY);
            // Fully solid 2x2 block.
            case 0xF:
                return CreateSolid(wallMask, chunkX, chunkY);
            default:
                LogUnsupportedPattern("wall", wallMask, chunkX, chunkY);
                return TileSpawn.None;
        }
    }
    private TileSpawn GuessDoorPrefab(
        CellKind c1,
        CellKind c2,
        CellKind c3,
        CellKind c4,
        int chunkX,
        int chunkY)
    {
        int doorCount = 0;
        CellKind doorKind = CellKind.Void;
        int doorIndex = -1;
        CellKind[] cells = { c1, c2, c3, c4 };
        for (int i = 0; i < cells.Length; i++)
        {
            if (!IsDoor(cells[i]))
            {
                continue;
            }
            doorCount++;
            doorKind = cells[i];
            doorIndex = i;
        }
        if (doorCount != 1)
        {
            LogUnsupportedPattern("door-count", doorCount, chunkX, chunkY);
            return TileSpawn.None;
        }
        int floorMask = BuildFloorMask(c1, c2, c3, c4);
        switch (floorMask)
        {
            case 0x3:
                return new TileSpawn(levelPrefabs.door, 0f);
            case 0xC:
                return new TileSpawn(levelPrefabs.door, 180f);
            case 0x5:
                return new TileSpawn(levelPrefabs.door, 90f);
            case 0xA:
                return new TileSpawn(levelPrefabs.door, 270f);
        }
        // Fallback for a door near a T-junction. Orientation comes from
        // the split axis and the door's quadrant inside this 2x2 chunk.
        if (doorKind == CellKind.DoorVertical)
        {
            bool doorInLeftColumn = doorIndex == 0 || doorIndex == 2;
            return new TileSpawn(
                levelPrefabs.door,
                doorInLeftColumn ? 270f : 90f
            );
        }
        bool doorInBottomRow = doorIndex == 0 || doorIndex == 1;
        return new TileSpawn(
            levelPrefabs.door,
            doorInBottomRow ? 180f : 0f
        );
    }
    private TileSpawn CreateInnerCorner(
        float rotation,
        int mask,
        int chunkX,
        int chunkY)
    {
        if (levelPrefabs.innerCorner != null)
        {
            return new TileSpawn(levelPrefabs.innerCorner, rotation);
        }
        LogUnsupportedPattern("inner-corner-without-prefab", mask, chunkX, chunkY);
        if (levelPrefabs.corner != null)
        {
            return new TileSpawn(levelPrefabs.corner, rotation);
        }
        return TileSpawn.None;
    }
    private TileSpawn CreateDiagonal(
        float rotation,
        int mask,
        int chunkX,
        int chunkY)
    {
        if (levelPrefabs.diagonal != null)
        {
            return new TileSpawn(levelPrefabs.diagonal, rotation);
        }
        LogUnsupportedPattern("diagonal-without-prefab", mask, chunkX, chunkY);
        return CreateSolid(mask, chunkX, chunkY);
    }
    private TileSpawn CreateSolid(int mask, int chunkX, int chunkY)
    {
        if (levelPrefabs.solid != null)
        {
            return new TileSpawn(levelPrefabs.solid, 0f);
        }
        LogUnsupportedPattern("solid-without-prefab", mask, chunkX, chunkY);
        return TileSpawn.None;
    }
    private static int BuildWallMask(
        CellKind c1,
        CellKind c2,
        CellKind c3,
        CellKind c4)
    {
        int mask = 0;
        if (c1 == CellKind.Wall) mask |= 1 << 0;
        if (c2 == CellKind.Wall) mask |= 1 << 1;
        if (c3 == CellKind.Wall) mask |= 1 << 2;
        if (c4 == CellKind.Wall) mask |= 1 << 3;
        return mask;
    }
    private static int BuildFloorMask(
        CellKind c1,
        CellKind c2,
        CellKind c3,
        CellKind c4)
    {
        int mask = 0;
        if (c1 == CellKind.Floor) mask |= 1 << 0;
        if (c2 == CellKind.Floor) mask |= 1 << 1;
        if (c3 == CellKind.Floor) mask |= 1 << 2;
        if (c4 == CellKind.Floor) mask |= 1 << 3;
        return mask;
    }
    private static bool IsDoor(CellKind cell)
    {
        return cell == CellKind.DoorVertical ||
               cell == CellKind.DoorHorizontal;
    }
    private void LogUnsupportedPattern(
        string category,
        int value,
        int chunkX,
        int chunkY)
    {
        if (!logUnsupportedTilePatterns)
        {
            return;
        }
        Debug.LogWarning(
            "Unsupported tile pattern [" + category + "] value=" + value +
            " chunk=(" + chunkX + ", " + chunkY + ").",
            this
        );
    }
    private void Spawn(GameObject prefab, int x, int y, float rotationY)
    {
        Vector3Int cell = new Vector3Int(x, y, 0);
        Vector3 position = unityGrid.GetCellCenterWorld(cell);
        Quaternion rotation = Quaternion.Euler(0f, rotationY, 0f);
        GameObject instance = Instantiate(
            prefab,
            position,
            rotation,
            generatedRoot
        );
        instance.name = x + "_" + y;
    }
    private void GridInit()
    {
        if (unityGrid == null)
        {
            return;
        }
        unityGrid.cellSize = cellSize;
        if (forceXZGrid)
        {
            unityGrid.cellLayout = GridLayout.CellLayout.Rectangle;
            unityGrid.cellSwizzle = GridLayout.CellSwizzle.XZY;
        }
    }
    private void ClearGeneratedObjects()
    {
        if (generatedRoot == null)
        {
            Transform existing = transform.Find("Generated Level");
            if (existing != null)
            {
                generatedRoot = existing;
            }
        }
        if (generatedRoot == null)
        {
            return;
        }
        if (Application.isPlaying)
        {
            Destroy(generatedRoot.gameObject);
        }
        else
        {
            DestroyImmediate(generatedRoot.gameObject);
        }
        generatedRoot = null;
    }
    public void Split(BSPNode node, int depth)
    {
        if (node == null || depth >= maxDepth)
        {
            return;
        }
        RectInt area = node.area;
        bool canSplitVertical = area.width >= minLeafSize * 2 + 1;
        bool canSplitHorizontal = area.height >= minLeafSize * 2 + 1;
        if (!canSplitVertical && !canSplitHorizontal)
        {
            return;
        }
        bool splitVertical = ChooseSplitOrientation(
            area,
            canSplitVertical,
            canSplitHorizontal
        );
        if (splitVertical)
        {
            SplitVertical(node, depth);
        }
        else
        {
            SplitHorizontal(node, depth);
        }
    }
    private bool ChooseSplitOrientation(
        RectInt area,
        bool canSplitVertical,
        bool canSplitHorizontal)
    {
        if (canSplitVertical && !canSplitHorizontal)
        {
            return true;
        }
        if (canSplitHorizontal && !canSplitVertical)
        {
            return false;
        }
        float aspect = (float)area.width / area.height;
        if (aspect > maxAspectRatio)
        {
            return true;
        }
        if (aspect < 1f / maxAspectRatio)
        {
            return false;
        }
        return rng.NextDouble() < 0.5;
    }
    private void SplitVertical(BSPNode node, int depth)
    {
        RectInt area = node.area;
        int minOffset = minLeafSize;
        int maxOffset = area.width - minLeafSize - 1;
        int offset;
        if (!TryChooseEvenOffset(minOffset, maxOffset, out offset, true, area))
        {
            return;
        }
        int wallX = area.xMin + offset;
        RectInt leftArea = new RectInt(
            area.xMin,
            area.yMin,
            offset,
            area.height
        );
        RectInt rightArea = new RectInt(
            wallX + 1,
            area.yMin,
            area.xMax - wallX - 1,
            area.height
        );
        node.splitAxis = SplitAxis.Vertical;
        node.splitLine = wallX;
        node.left = new BSPNode(leftArea);
        node.right = new BSPNode(rightArea);
        Split(node.left, depth + 1);
        Split(node.right, depth + 1);
    }
    private void SplitHorizontal(BSPNode node, int depth)
    {
        RectInt area = node.area;
        int minOffset = minLeafSize;
        int maxOffset = area.height - minLeafSize - 1;
        int offset;
        if (!TryChooseEvenOffset(minOffset, maxOffset, out offset, false, area))
        {
            return;
        }
        int wallY = area.yMin + offset;
        RectInt bottomArea = new RectInt(
            area.xMin,
            area.yMin,
            area.width,
            offset
        );
        RectInt topArea = new RectInt(
            area.xMin,
            wallY + 1,
            area.width,
            area.yMax - wallY - 1
        );
        node.splitAxis = SplitAxis.Horizontal;
        node.splitLine = wallY;
        node.left = new BSPNode(bottomArea);
        node.right = new BSPNode(topArea);
        Split(node.left, depth + 1);
        Split(node.right, depth + 1);
    }
    private bool TryChooseEvenOffset(
        int minOffset,
        int maxOffset,
        out int offset,
        bool vertical,
        RectInt area)
    {
        var bestOffsets = new List<int>();
        float bestScore = float.MaxValue;
        int firstEven = (minOffset & 1) == 0 ? minOffset : minOffset + 1;
        int lastEven = (maxOffset & 1) == 0 ? maxOffset : maxOffset - 1;
        if (lastEven < firstEven)
        {
            offset = -1;
            return false;
        }
        for (int candidate = firstEven; candidate <= lastEven; candidate += 2)
        {
            int w1 = vertical ? candidate : area.width;
            int h1 = vertical ? area.height : candidate;
            int w2 = vertical ? area.width - candidate - 1 : area.width;
            int h2 = vertical ? area.height : area.height - candidate - 1;
            float a1 = Mathf.Max((float)w1 / h1, (float)h1 / w1);
            float a2 = Mathf.Max((float)w2 / h2, (float)h2 / w2);
            float score = Mathf.Abs(a1 - 1f) + Mathf.Abs(a2 - 1f);
            if (score < bestScore - 0.0001f)
            {
                bestScore = score;
                bestOffsets.Clear();
                bestOffsets.Add(candidate);
            }
            else if (Mathf.Abs(score - bestScore) < 0.0001f)
            {
                bestOffsets.Add(candidate);
            }
        }
        offset = bestOffsets[rng.Next(bestOffsets.Count)];
        return true;
    }
}




//using System;
//using System.Collections.Generic;
//using UnityEngine;
//public struct TilePrefabs
//{
//    public GameObject[] floor;
//    public GameObject[] wall;
//    public GameObject[] corner;
//    public GameObject[] door;
//}
//public enum SplitAxis
//{
//    None,
//    Vertical,
//    Horizontal
//}
//public enum WallDirection
//{
//    N,
//    E,
//    S,
//    W
//}
//public enum Cornerirection
//{
//    NE,
//    SE,
//    SW,
//    NW
//}
//public enum CellKind
//{
//    Wall = 1,
//    Floor = 0,
//    DoorVertical = 4,
//    DoorHorizontal = 7
//}
//public class BSPNode
//{
//    public RectInt area;
//    public BSPNode left;
//    public BSPNode right;
//    public SplitAxis splitAxis;
//    public int splitLine;
//    public BSPNode(RectInt area)
//    {
//        this.area = area;
//        splitAxis = SplitAxis.None;
//        splitLine = -1;
//    }
//    public bool IsLeaf => left == null && right == null;
//}
//public struct TileSpawn
//{
//    public GameObject prefab;
//    public float RotationY;
//    public TileSpawn(GameObject prefab, float rotationY)
//    {
//        this.prefab = prefab;
//        this.RotationY = rotationY;
//    }
//}
//public class LevelGenerator : MonoBehaviour
//{
//    [Serializable]
//    public struct LevelPrefabs
//    {
//        public GameObject floor;
//        public GameObject wall;
//        public GameObject corner;
//        public GameObject door;
//    }
//    public LevelPrefabs levelPrefabs;
//    [Header("BSP")]
//    public int seed;
//    public int width;
//    public int height;
//    public int maxDepth;
//    public int minLeafSize;
//    public float maxAspectRatio;
//    [Header("Grid")]
//    public Vector3 cellSize = Vector3.one;
//    public bool forceXZGrid;
//    private Grid unityGrid;
//    private BSPNode root;
//    private List<RectInt> rooms = new();
//    private System.Random rng;
//    private CellKind[,] map;
//    private Transform generatedRoot;
//    private void Awake()
//    {
//        unityGrid = GetComponent<Grid>();
//        GenerateLevel();
//    }
//    public void GenerateLevel()
//    {
//        rng = new System.Random(seed);
//        rooms.Clear();
//        map = new CellKind[width, height];
//        for (int y = 0; y < height; y++)
//        {
//            for (int x = 0; x < width; x++)
//            {
//                map[x, y] = CellKind.Wall;
//            }
//        }
//        RectInt rootArea = new RectInt(1, 1, width - 2, height - 2);
//        root = new BSPNode(rootArea);
//        Split(root, 0);
//        CarveLeafRoom(root);
//        CarveDoors(root);
//        Build3D();
//    }
//    public void Build3D()
//    {
//        if (map == null) return;
//        if (unityGrid == null) unityGrid = GetComponent<Grid>();
//        GridInit();
//        for (int y = 0; y < height; y += 2)
//        {
//            for (int x = 0; x < width; x += 2)
//            {
//                var c_1 = map[x, y];
//                var c_2 = map[x + 1, y];
//                var c_3 = map[x, y + 1];
//                var c_4 = map[x + 1, y + 1];
//                var pair = GuessPrefab(c_1, c_2, c_3, c_4);
//                Spawn(pair.prefab, x, y, pair.rotation);
//            }
//        }
//    }
//    private (GameObject prefab, float rotation) GuessPrefab(CellKind c1, CellKind c2, CellKind c3, CellKind c4)
//    {
//        var sum = (int)c1 + (int)c2 + (int)c3 + (int)c4;
//        if (sum == 3)
//        {
//            if (c4 == CellKind.Floor) return (levelPrefabs.corner, 270); //вместо (c4 == CellKind.Floor) можно (int(c4)==0)
//            if (c3 == CellKind.Floor) return (levelPrefabs.corner, 180);
//            if (c2 == CellKind.Floor) return (levelPrefabs.corner, 0);
//            return (levelPrefabs.corner, 90);
//        }
//        if (sum == 2)
//        {
//            if (c1 == CellKind.Wall && c2 == CellKind.Wall) return (levelPrefabs.wall, 180f);
//            if (c2 == CellKind.Wall && c4 == CellKind.Wall) return (levelPrefabs.wall, 90);
//            if (c3 == CellKind.Wall && c4 == CellKind.Wall) return (levelPrefabs.wall, 0);
//            return (levelPrefabs.wall, 270f);
//        }
//        if (sum == 5 || sum == 8)
//        {
//            if (c3 == CellKind.Floor && c4 == CellKind.Floor) return (levelPrefabs.door, 180f); // убрать если вариант 2
//            if (c2 == CellKind.Floor && c4 == CellKind.Floor) return (levelPrefabs.door, 270f);
//            if (c3 == CellKind.Floor && c1 == CellKind.Floor) return (levelPrefabs.door, 90f);
//            return (levelPrefabs.door, 0f); // убрать если вариант 2
//        }
//        return (levelPrefabs.floor, 0f);
//    }
//    private void Spawn(GameObject prefab, int x, int y, float rotationY)
//    {
//        var cell = new Vector3Int(x, y, 0);
//        var position = unityGrid.GetCellCenterWorld(cell);
//        var rotation = Quaternion.Euler(0f, rotationY, 0f);
//        var instance = Instantiate(
//            prefab,
//            position,
//            rotation,
//            generatedRoot
//        );
//        instance.name = $"{x}_{y}";
//    }
//    private void GridInit()
//    {
//        if (unityGrid == null) return;
//        unityGrid.cellSize = cellSize;
//        if (forceXZGrid)
//        {
//            unityGrid.cellLayout = GridLayout.CellLayout.Rectangle;
//            unityGrid.cellSwizzle = GridLayout.CellSwizzle.XZY;
//        }
//    }
//    public void CarveDoorsForSplit(BSPNode node)
//    {
//        switch (node.splitAxis)
//        {
//            case SplitAxis.Vertical:
//                CarveVerticalDoors(node);
//                break;
//            case SplitAxis.Horizontal:
//                CarveHorizontalDoors(node);
//                break;
//            default:
//                break;
//        }
//    }
//    private void CarveVerticalDoors(BSPNode node)
//    {
//        var wallX = node.splitLine;
//        List<RectInt> leftRooms = new();
//        List<RectInt> rightRooms = new();
//        WalkLeafRooms(node.left, r => r.xMax == wallX, leftRooms);
//        WalkLeafRooms(node.right, r => r.xMin == wallX + 1, rightRooms);
//        List<Vector2Int> candidates = new();
//        foreach (var leftRoom in leftRooms)
//        {
//            foreach (var rightRoom in rightRooms)
//            {
//                int overlapMin = Mathf.Max(leftRoom.yMin, rightRoom.yMin);
//                int overlapMax = Mathf.Min(leftRoom.yMax, rightRoom.yMax);
//                if (overlapMin < overlapMax) candidates.Add(new Vector2Int(overlapMin, overlapMax));
//            }
//        }
//        if (candidates.Count == 0) return;
//        var span = candidates[rng.Next(0, candidates.Count)];
//        CarveVerticalDoorsOpening(wallX, span.x, span.y);
//    }
//    private void CarveVerticalDoorsOpening(int x, int yMin, int yMax)
//    {
//        int doorStart = rng.Next(yMin, yMax);
//        map[x, doorStart] = CellKind.DoorVertical;
//    }
//    private void CarveHorizontalDoors(BSPNode node)
//    {
//        var wallY = node.splitLine;
//        List<RectInt> topRooms = new();
//        List<RectInt> bottomRooms = new();
//        WalkLeafRooms(node.left, r => r.yMax == wallY, topRooms);
//        WalkLeafRooms(node.right, r => r.yMin == wallY + 1, bottomRooms);
//        List<Vector2Int> candidates = new();
//        foreach (var top in topRooms)
//        {
//            foreach (var bottom in bottomRooms)
//            {
//                int overlapMin = Mathf.Max(top.xMin, bottom.xMin);
//                int overlapMax = Mathf.Min(top.xMax, bottom.xMax);
//                if (overlapMin < overlapMax) candidates.Add(new Vector2Int(overlapMin, overlapMax));
//            }
//        }
//        if (candidates.Count == 0) return;
//        var span = candidates[rng.Next(0, candidates.Count)];
//        CarveHorizontalDoorsOpening(wallY, span.x, span.y);
//    }
//    private void CarveHorizontalDoorsOpening(int y, int xMin, int xMax)
//    {
//        int doorStart = rng.Next(xMin, xMax);
//        map[doorStart, y] = CellKind.DoorHorizontal;
//    }
//    private void WalkLeafRooms(BSPNode node, Predicate<RectInt> predicate, List<RectInt> result)
//    {
//        if (node == null) return;
//        if (node.IsLeaf)
//        {
//            if (predicate(node.area))
//            {
//                result.Add(node.area);
//            }
//            return;
//        }
//        WalkLeafRooms(node.left, predicate, result);
//        WalkLeafRooms(node.right, predicate, result);
//    }
//    public void CarveDoors(BSPNode node)
//    {
//        if (node == null || node.IsLeaf) return;
//        CarveDoorsForSplit(node);
//        CarveDoors(node.left);
//        CarveDoors(node.right);
//    }
//    public void CarveLeafRoom(BSPNode node)
//    {
//        if (node == null) return;
//        if (node.IsLeaf)
//        {
//            var room = node.area;
//            rooms.Add(node.area);
//            for (int y = room.yMin; y < room.yMax; y++)
//            {
//                for (int x = room.xMin; x < room.xMax; x++)
//                {
//                    map[x, y] = CellKind.Floor;
//                }
//            }
//            return;
//        }
//        CarveLeafRoom(node.left);
//        CarveLeafRoom(node.right);
//    }
//    private void SplitVertical(BSPNode root, int depth)
//    {
//        var area = root.area;
//        int minOffset = minLeafSize;
//        int maxOffset = area.width - minLeafSize - 1;
//        if (maxOffset < minOffset) return;
//        int offset = rng.Next(minOffset, maxOffset + 1);
//        //ЧАСТЬ ДЗ
//        if (offset % 2 != 0) offset++;
//        if (offset >= maxOffset) offset -= 2;
//        if (offset < minOffset) return;
//        int wallX = area.xMin + offset;
//        var leftArea = new RectInt(area.xMin, area.yMin, offset, area.height);
//        var rightArea = new RectInt(wallX + 1, area.yMin, area.xMax - wallX - 1, area.height);
//        root.splitAxis = SplitAxis.Vertical;
//        root.splitLine = wallX;
//        BSPNode left = new BSPNode(leftArea);
//        BSPNode right = new BSPNode(rightArea);
//        root.left = left;
//        root.right = right;
//        Split(root.left, depth + 1);
//        Split(root.right, depth + 1);
//        return;
//    }
//    private void SplitHorizontal(BSPNode root, int depth)
//    {
//        var area = root.area;
//        int minOffset = minLeafSize;
//        int maxOffset = area.height - minLeafSize - 1;
//        if (maxOffset < minOffset) return;
//        int offset = rng.Next(minOffset, maxOffset + 1);
//        //ЧАСТЬ ДЗ
//        if (offset % 2 != 0) offset++;
//        if (offset >= maxOffset) offset -= 2;
//        if (offset < minOffset) return;
//        int wallY = area.yMin + offset;
//        var leftArea = new RectInt(area.xMin, area.yMin, area.width, offset);
//        var rightArea = new RectInt(area.xMin, wallY + 1, area.width, area.yMax - wallY - 1);
//        root.splitAxis = SplitAxis.Horizontal;
//        root.splitLine = wallY;
//        BSPNode left = new BSPNode(leftArea);
//        BSPNode right = new BSPNode(rightArea);
//        root.left = left;
//        root.right = right;
//        Split(root.left, depth + 1);
//        Split(root.right, depth + 1);
//        return;
//    }
//    private bool ChooseSplitOrientation(RectInt area, bool vertical, bool horizontal)
//    {
//        if (vertical && !horizontal) return true;
//        if (horizontal && !vertical) return false;
//        float aspect = (float)area.width / area.height;
//        if (aspect > maxAspectRatio)
//        {
//            return true;
//        }
//        if (aspect < 1f / maxAspectRatio)
//        {
//            return false;
//        }
//        return rng.NextDouble() < 0.5;
//    }
//    public void Split(BSPNode root, int depth)
//    {
//        if (depth == maxDepth) return;
//        var area = root.area;
//        bool canSplitVertical = area.width >= minLeafSize * 2 + 1;
//        bool canSplitHorizontal = area.height >= minLeafSize * 2 + 1;
//        if (!canSplitVertical && !canSplitHorizontal) return;
//        if (ChooseSplitOrientation(area, canSplitVertical, canSplitHorizontal)) SplitVertical(root, depth);
//        else SplitHorizontal(root, depth);
//    }
//}