using System.Collections.Generic;
using UnityEngine;

public static class UpgradeTreeLayout
{
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

        var roots = CSVLoader.GetRoots();
        if (roots.Count == 0)
            return true;

        var pending = new Queue<string>();
        string rootId = roots[0].identifier;
        positions[rootId] = Vector2.zero;
        pending.Enqueue(rootId);

        bool failed = false;

        while (pending.Count > 0)
        {
            string parentId = pending.Dequeue();
            var children = CSVLoader.GetChildren(parentId);
            if (children.Count == 0)
                continue;

            bool isRoot = parentId == rootId;
            int maxChildren = isRoot ? 4 : 3;
            if (children.Count > maxChildren)
            {
                Debug.LogError(
                    $"UpgradeTreeLayout: node '{parentId}' has {children.Count} children, max allowed is {maxChildren}. Remaining nodes were not placed.");
                failed = true;
                break;
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
                var dir = placementDirections[i];
                Vector2 childPos = parentPos + ToOffset(dir, nodeSpacing);

                if (TryFindExistingKey(positions, childPos, out string existingId))
                {
                    Debug.LogError(
                        $"UpgradeTreeLayout: node '{child.identifier}' overlaps '{existingId}' at {childPos}. Remaining nodes were not placed.");
                    failed = true;
                    break;
                }

                positions[child.identifier] = childPos;
                directionFromParent[child.identifier] = dir;
                pending.Enqueue(child.identifier);
            }

            if (failed)
                break;
        }

        return !failed;
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
