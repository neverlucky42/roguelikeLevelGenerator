using System;
using System.Collections.Generic;
using UnityEngine;
public enum ContentCell
{
    Free,
    Obstacle,
    DoorClearance,
    Navigation,
    Reserved
}

public enum NavigationPathMode
{
    Direct,
    Segment,
    Meander
}

public enum RoomTemplate
{
    Empty,
    Arena,
    OpenArea,
    Islands,
    Navigation,
    Split,
    Snake,
    Crossroads,
    Clusters,
    Perimetr
}

public class RoomAnalysis
{
    public int doorCount;
    public float aspectRatio;
    public Vector2Int size;
    public int area;
    
    public bool oppositeDoors;
    public bool adjacentDoors;

    public bool isWide;
    public bool isTall;
    public bool isSquare;

    public enum roomSide
    {
        Top,
        Bottom,
        Left,
        Right
    }

    public RoomAnalysis(RoomContentPlan plan)
    {
        this.size = new Vector2Int();
        size.x = plan.size.x;
        size.y = plan.size.y;
        doorCount = plan.doors.Count;
        aspectRatio = (float)size.x / (float)size.y;
        area = size.x * size.y;
        isWide = aspectRatio > 1.5f;
        isTall = aspectRatio > 0.67f;
        isSquare = !isWide && !isTall;
        AnalysisDoorConf(plan, out oppositeDoors, out adjacentDoors);
    }

    public static roomSide GetEntrancesSide(RoomContentPlan plan, Vector2Int entrance)
    {
        var left = entrance.x;
        var right = plan.size.x - left - 1;
        var bottom = entrance.y;
        var top = plan.size.y - bottom - 1;
        
        var min = Mathf.Min(Mathf.Min(left, right), Mathf.Min(bottom, top));
        if (min == left)
        {
            return roomSide.Left;
        }
        else if (min == right)
        {
            return roomSide.Right;
        }
        else if (min == top)
        {
            return roomSide.Top;
        }
        else
        {
            return roomSide.Bottom;
        }
    }

    private static void AnalysisDoorConf(RoomContentPlan plan, out bool oppositeDoors, out bool adjacentDoors)
    {
        oppositeDoors = false;
        adjacentDoors = false;

        if (plan.enterances.Count != 2)
        { 
            oppositeDoors = false;
            adjacentDoors = false;
            return;
        }
        var side1 = GetEntrancesSide(plan, plan.enterances[0]);
        var side2 = GetEntrancesSide(plan, plan.enterances[1]);

        if (side1 == roomSide.Left && side2 == roomSide.Right ||
            side1 == roomSide.Right && side2 == roomSide.Left ||
            side1 == roomSide.Top && side2 == roomSide.Bottom ||
            side1 == roomSide.Bottom && side2 == roomSide.Top) oppositeDoors =  true;

        if (side1 != side2)
        {
            adjacentDoors = true;
        }
    }

}


public sealed class RoomContentPlan
{
    public readonly List<Vector2Int> doors = new();
    public readonly List<Vector2Int> enterances = new();
    public Vector2Int hub;
    public readonly ContentCell[,] cells;
    public readonly int roomID;
    public readonly RoomKind roomKind;
    public readonly Vector2Int size;

    public RoomContentPlan(int roomId, RoomKind roomKind, Vector2Int size)
    {
        this.roomID = roomId;
        this.roomKind = roomKind;
        this.size = size;
        cells = new ContentCell[size.x, size.y];
    }

    public bool IsInside(Vector2Int cell)
    {
        return cell.x >= 0 && cell.y >= 0 && cell.x < size.x && cell.y < size.y;
    }
}
public sealed class RoomGenerator
{
    private const int DoorClearanceDepth = 1;
    public const int NavigationRadius = 0;
    private int levelSeed;

    private readonly Dictionary<RoomTemplate, RoomTemplateConfig> configs;
    
    private System.Random rng;
    public static float HubCenterWeight { get; set; }
    public RoomGenerator(int seed, IEnumerable<RoomTemplateConfig> templateConfigs)
    {
        this.levelSeed = seed;
        configs = new();
        foreach (var config in templateConfigs)
        {
            configs[config.template] = config;
        }
    }

    public Dictionary<int, RoomContentPlan> Generate(RoomTopology topology, SelectedTopology selectedTopology)
    {
        var result = new Dictionary<int, RoomContentPlan>();
        foreach (var roomId in selectedTopology.roomIds)
        {
            var room = topology.rooms[roomId];
            var plan = new RoomContentPlan(roomId, room.kind, new Vector2Int(room.area.width, room.area.height));
            CollectDoors(topology, selectedTopology, plan, room);
            MarkDoorClearance(plan);
            var analyzeRoom = new RoomAnalysis(plan);
            var template = SelectTemplate(plan, analyzeRoom, roomId);
            plan.hub = FindHub(plan);
            BuildNavigationSkeleton(plan, analyzeRoom, template, roomId);
            result.Add(roomId, plan);
        }
        return result;
    }
    private RoomTemplate SelectTemplate(RoomContentPlan plan, RoomAnalysis analysis, int roomId)
    {
        switch(plan.roomKind)
        {
            case RoomKind.Start: return RoomTemplate.Empty;
            case RoomKind.Boss: return RoomTemplate.Arena;
            case RoomKind.Shop: return RoomTemplate.OpenArea;
            case RoomKind.Treasure: return RoomTemplate.Islands;
        }
        if (analysis.doorCount >= 4) return RoomTemplate.Crossroads;

        if (analysis.doorCount == 1)
        {
            if (analysis.area >= 80) return RoomTemplate.Islands;
            return RoomTemplate.Clusters;
        }
        if (analysis.doorCount == 2)
        {
            if (analysis.oppositeDoors)
            {
                if (analysis.area >= 80) return RoomTemplate.Snake;
                return RoomTemplate.Split;
            }

            if (analysis.adjacentDoors) return RoomTemplate.Clusters;

        }

        return RoomTemplate.Clusters;

    }

    private static void CollectDoors(RoomTopology topology, SelectedTopology selectedTopology, RoomContentPlan plan, RoomVertex room)
    {
        foreach (var edgeId in topology.adjacency[room.id])
        {
            if (!selectedTopology.edgeIds.Contains(edgeId)) continue;
            var edge = topology.edges[edgeId];
            plan.doors.Add(ToLocal(room.area, edge.doorCell));
            plan.enterances.Add(ToLocal(room.area, GetEnterance(room.id, edge)));
        }
    }

    private static void MarkDoorClearance(RoomContentPlan plan)
    {
        foreach (var door in plan.doors)
        {
            for (int y = door.y - DoorClearanceDepth; y <= door.y + DoorClearanceDepth; y++)
            {
                for (int x = door.x - DoorClearanceDepth; x <= door.x + DoorClearanceDepth; x++)
                {
                    if (plan.IsInside(new Vector2Int(x, y)))
                    {
                        plan.cells[x, y] = ContentCell.DoorClearance;
                    }

                }
            }

        }

    }

    private void BuildNavigationSkeleton(RoomContentPlan plan, RoomAnalysis analysis, RoomTemplate template, int roomId)
    {
        MarkDisk(plan, plan.hub, NavigationRadius, ContentCell.Navigation, true);

        var random = new System.Random(levelSeed + roomId);

        foreach (var enterance in plan.enterances)
        {
            switch (template)
            {
                case RoomTemplate.Arena:
                case RoomTemplate.OpenArea:
                    break;
                case RoomTemplate.Islands:
                case RoomTemplate.Clusters:
                    break;
                case RoomTemplate.Snake:
                    break;
                case RoomTemplate.Split:
                    break;
                case RoomTemplate.Crossroads:
                    break;
            }

            BuildPath(plan, enterance, plan.hub, random);
        }
    }

    private static void BuildPath(RoomContentPlan plan, Vector2Int start, Vector2Int end, System.Random rng)
    {
        Vector2Int current = start;

        MarkDisk(plan, current, NavigationRadius, ContentCell.Navigation, true);
        var rand = 0d;
        while (current != end)
        {
            if (current.x == end.x) rand = 1d;
            else if (current.y == end.y) rand = 0d;
            else rand = rng.NextDouble();
            if (rand < 0.5d)
            {
                current.x += Math.Sign(end.x - current.x);
            }
            else
            {
                current.y += Math.Sign(end.y - current.y);
            }
            MarkDisk(plan, current, NavigationRadius, ContentCell.Navigation, true);
        }
    }

    private static Vector2Int GetEnterance(int roomId, RoomEdge edge)
    {
        if (edge.axis == SplitAxis.Vertical)
        {
            return roomId == edge.roomA ? edge.doorCell + Vector2Int.left : edge.doorCell + Vector2Int.right;
        }
        return roomId == edge.roomA ? edge.doorCell + Vector2Int.down : edge.doorCell + Vector2Int.up;
    }

    private static Vector2Int ToLocal(RectInt room, Vector2Int cell)
    {
        return new Vector2Int(cell.x - room.xMin, cell.y - room.yMin);
    }
    private static Vector2Int FindHub(RoomContentPlan plan)
    {

        var center = new Vector2((plan.size.x - 1) * .5f, (plan.size.y - 1) * .5f);
        if (plan.enterances.Count == 0)
        {
            return Vector2Int.FloorToInt(center);
        }
        if (plan.enterances.Count == 1)
        {
            bool isX = plan.enterances[0].x != plan.doors[0].x;
            return new Vector2Int(
                isX ? plan.size.x / 2 : (plan.enterances[0].x + plan.size.x / 2) / 2,
                isX ? (plan.enterances[0].y + plan.size.y / 2) / 2 : plan.size.y / 2);
        }

        var bestCell = Vector2Int.FloorToInt(center);
        float bestScore = float.MaxValue;
        int margin = 1;
        for (int y = margin; y < plan.size.y - margin; y++)
        {
            for (int x = margin; x < plan.size.x - margin; x++)
            {
                var candidate = new Vector2Int(x, y);
                float score = 0;
                foreach (var entrance in plan.enterances)
                {
                    score += Mathf.Abs(candidate.x - entrance.x) + Mathf.Abs(candidate.y - entrance.y);
                }
                score += Vector2.Distance(candidate, center) * HubCenterWeight;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestCell = candidate;
                }
            }
        }
        return bestCell;
    }

    private static void MarkDisk(RoomContentPlan plan, Vector2Int center, int radius, ContentCell value, bool overwriterNav)
    {
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                var cell = center + new Vector2Int(x, y);
                if (!plan.IsInside(cell))
                {
                    continue;
                }
                if (!overwriterNav && plan.cells[cell.x, cell.y] == ContentCell.Navigation)
                {
                    continue;
                }

                if (value == ContentCell.Navigation && plan.cells[cell.x, cell.y] == ContentCell.DoorClearance)
                {
                    continue;
                }

                plan.cells[cell.x, cell.y] = value;
            }
        }
    }
}
