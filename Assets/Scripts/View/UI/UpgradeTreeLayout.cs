using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class UpgradeTreeLayout
{
    const int MaxPlacementSteps = 3;

    public enum Direction
    {
        Up = 0,
        Right = 1,
        Down = 2,
        Left = 3
    }

    public static bool TryBuild(float nodeSpacing, out Dictionary<string, Vector2> positions)
    {
        positions = new Dictionary<string, Vector2>();
        var directionFromParent = new Dictionary<string, Direction>();

        var roots = CSVLoader.GetRoots()
            .Where(info => info != null && info.IsVisible())
            .ToList();
        if (roots.Count == 0)
            return true;

        var pending = new Queue<string>();
        string rootId = roots[0].identifier;
        positions[rootId] = Vector2.zero;
        pending.Enqueue(rootId);

        bool hadSkippedNodes = false;

        while (pending.Count > 0)
        {
            string parentId = pending.Dequeue();
            var children = CSVLoader.GetChildren(parentId)
                .Where(info => info != null && info.IsVisible())
                .ToList();
            if (children.Count == 0)
                continue;

            bool isRoot = parentId == rootId;
            int maxChildren = isRoot ? 4 : 3;
            if (children.Count > maxChildren)
            {
                Debug.LogError(
                    $"UpgradeTreeLayout: node '{parentId}' has {children.Count} children, max allowed is {maxChildren}. Extra children are skipped.");
                children = children.Take(maxChildren).ToList();
                hadSkippedNodes = true;
            }

            Direction[] placementDirections;
            if (isRoot)
            {
                placementDirections = new[] { Direction.Up, Direction.Right, Direction.Down, Direction.Left };
            }
            else
            {
                var incoming = directionFromParent[parentId];
                placementDirections = new[]
                {
                    incoming,
                    TurnLeft(incoming),
                    TurnRight(incoming)
                };
            }

            Vector2 parentPos = positions[parentId];
            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (!TryPlaceChild(child, parentPos, placementDirections, nodeSpacing, positions, directionFromParent, pending, out string overlapReason))
                {
                    Debug.LogError(
                        $"UpgradeTreeLayout: node '{child.identifier}' could not be placed without overlap. {overlapReason} This node is skipped.");
                    hadSkippedNodes = true;
                }
            }
        }

        return !hadSkippedNodes;
    }

    static bool TryPlaceChild(
        UpgradeInfo child,
        Vector2 parentPos,
        Direction[] placementDirections,
        float nodeSpacing,
        Dictionary<string, Vector2> positions,
        Dictionary<string, Direction> directionFromParent,
        Queue<string> pending,
        out string failureReason)
    {
        failureReason = null;

        // Check every expansion direction in order (root: 4, others: same/left/right).
        // Only fail after all directions (and farther steps along them) are occupied.
        for (int step = 1; step <= MaxPlacementSteps; step++)
        {
            for (int d = 0; d < placementDirections.Length; d++)
            {
                var dir = placementDirections[d];
                Vector2 childPos = parentPos + ToOffset(dir, nodeSpacing * step);

                if (TryFindExistingKey(positions, childPos, out _))
                    continue;

                positions[child.identifier] = childPos;
                directionFromParent[child.identifier] = dir;
                pending.Enqueue(child.identifier);
                return true;
            }
        }

        failureReason =
            $"Tried all {placementDirections.Length} directions x {MaxPlacementSteps} steps; every slot is occupied.";
        return false;
    }

    static bool TryFindExistingKey(Dictionary<string, Vector2> positions, Vector2 pos, out string existingId)
    {
        foreach (var pair in positions)
        {
            if (Vector2.Distance(pair.Value, pos) < 0.01f)
            {
                existingId = pair.Key;
                return true;
            }
        }

        existingId = null;
        return false;
    }

    static Direction TurnLeft(Direction direction)
    {
        return (Direction)(((int)direction + 3) % 4);
    }

    static Direction TurnRight(Direction direction)
    {
        return (Direction)(((int)direction + 1) % 4);
    }

    static Vector2 ToOffset(Direction direction, float distance)
    {
        switch (direction)
        {
            case Direction.Up:
                return new Vector2(0f, distance);
            case Direction.Right:
                return new Vector2(distance, 0f);
            case Direction.Down:
                return new Vector2(0f, -distance);
            default:
                return new Vector2(-distance, 0f);
        }
    }
}
