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
        // Все параметры навигации берём из ассета RoomTemplateConfig.
        var config = GetConfig(template);

        MarkDisk(plan, plan.hub, config.navigationRadius, ContentCell.Navigation, true);

        var random = new System.Random(levelSeed + roomId);

        foreach (var enterance in plan.enterances)
        {
            BuildPath(plan, enterance, plan.hub, config, random);
        }
    }

    private RoomTemplateConfig GetConfig(RoomTemplate template)
    {
        if (configs.TryGetValue(template, out var config) && config != null)
        {
            return config;
        }

        // Ассет для шаблона не назначен — создаём дефолтный конфиг в рантайме,
        // чтобы генератор не падал, и кэшируем его в словаре.
        Debug.LogWarning("Нет ассета RoomTemplateConfig для шаблона " + template + " — использую настройки по умолчанию.");
        var fallback = ScriptableObject.CreateInstance<RoomTemplateConfig>();
        fallback.template = template;
        configs[template] = fallback;
        return fallback;
    }

    private static void BuildPath(RoomContentPlan plan, Vector2Int start, Vector2Int end, RoomTemplateConfig config, System.Random rng)
    {
        switch (config.pathMode)
        {
            case NavigationPathMode.Direct:
                BuildDirectPath(plan, start, end, config, rng);
                break;
            case NavigationPathMode.Segment:
                BuildSegmentedPath(plan, start, end, config);
                break;
            case NavigationPathMode.Meander:
                BuildMeanderPath(plan, start, end, config, rng);
                break;
        }
    }

    private static void BuildDirectPath(RoomContentPlan plan, Vector2Int start, Vector2Int end, RoomTemplateConfig config, System.Random rng)
    {
        Vector2Int current = start;
        MarkDisk(plan, current, config.navigationRadius, ContentCell.Navigation, true);

        while (current != end)
        {
            int dx = end.x - current.x;
            int dy = end.y - current.y;
            bool preferX = Mathf.Abs(dx) >= Mathf.Abs(dy);
            bool moveX;

            if (dx == 0)
            {
                moveX = false;
            }
            else if (dy == 0)
            {
                moveX = true;
            }
            else
            {
                // directnes — вероятность шагнуть по доминирующей оси:
                // чем выше, тем прямее путь.
                moveX = rng.NextDouble() < config.directnes ? preferX : !preferX;
            }

            current += moveX
                ? new Vector2Int(Math.Sign(dx), 0)
                : new Vector2Int(0, Math.Sign(dy));

            MarkDisk(plan, current, config.navigationRadius, ContentCell.Navigation, true);
        }
    }

    private static void BuildSegmentedPath(RoomContentPlan plan, Vector2Int start, Vector2Int end, RoomTemplateConfig config)
    {
        Vector2Int current = start;
        MarkDisk(plan, current, config.navigationRadius, ContentCell.Navigation, true);

        // Г-образный путь: сначала по длинной оси, потом по короткой.
        bool xAxisFirst = Mathf.Abs(end.x - start.x) >= Mathf.Abs(end.y - start.y);

        while (current != end)
        {
            // Если идём сначала по X — двигаемся по X, пока не выровняемся,
            // потом по Y. Если сначала по Y — наоборот. Иначе при
            // выровненной короткой оси Sign(0) == 0 зацикливал путь.
            bool moveX = xAxisFirst ? current.x != end.x : current.y == end.y;

            if (moveX)
            {
                current += new Vector2Int(Math.Sign(end.x - current.x), 0);
            }
            else
            {
                current += new Vector2Int(0, Math.Sign(end.y - current.y));
            }

            MarkDisk(plan, current, config.navigationRadius, ContentCell.Navigation, true);
        }
    }

    private static void BuildMeanderPath(RoomContentPlan plan, Vector2Int start, Vector2Int end, RoomTemplateConfig config, System.Random rng)
    {
        Vector2Int current = start;
        MarkDisk(plan, current, config.navigationRadius, ContentCell.Navigation, true);

        bool horizontal = Mathf.Abs(end.x - start.x) >= Mathf.Abs(end.y - start.y);

        while (current != end)
        {
            // turnChance — как часто путь меняет ось движения (зигзаг).
            if (rng.NextDouble() < config.turnChance)
            {
                horizontal = !horizontal;
            }

            var stepX = new Vector2Int(Math.Sign(end.x - current.x), 0);
            var stepY = new Vector2Int(0, Math.Sign(end.y - current.y));

            // Шаг разрешён, если он не уводит путь дальше maxLateralOffset
            // от прямой start→end. Если оба шага запрещены — просто идём
            // к цели, чтобы путь гарантированно дошёл до хаба.
            Vector2Int step;
            if (horizontal && stepX.x != 0 && !ExceedsLateralOffset(current + stepX, start, end, config.maxLateralOffset))
            {
                step = stepX;
            }
            else if (stepY.y != 0 && !ExceedsLateralOffset(current + stepY, start, end, config.maxLateralOffset))
            {
                step = stepY;
            }
            else if (stepX.x != 0)
            {
                step = stepX;
            }
            else
            {
                step = stepY;
            }

            current += step;
            MarkDisk(plan, current, config.navigationRadius, ContentCell.Navigation, true);
        }
    }

    private static bool ExceedsLateralOffset(Vector2Int cell, Vector2Int start, Vector2Int end, int maxLateralOffset)
    {
        // Расстояние от точки до прямой start→end (через векторное произведение).
        var toEnd = new Vector2(end.x - start.x, end.y - start.y);
        var toCell = new Vector2(cell.x - start.x, cell.y - start.y);
        float cross = toCell.x * toEnd.y - toCell.y * toEnd.x;
        float lateral = Mathf.Abs(cross) / toEnd.magnitude;
        return lateral > maxLateralOffset;
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
