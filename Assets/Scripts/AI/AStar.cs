using System.Collections.Generic;
using Unity.Mathematics;
using SheepGame.Sim;


class Node
{
    public int2 coordinate;
    public int hCost;
    public int gCost;
    public int fCost => this.hCost + this.gCost;
    public Node parent;

    public Node(int2 c)
    {
        this.coordinate = c;
        hCost = 0;
        gCost = 0;
    }

    public bool Equals(Node other)
    {
        return this.coordinate.x == other.coordinate.x && this.coordinate.y == other.coordinate.y;
    }
}
public class AStar
{
    private ObstacleGrid obstacles;
    private List<Node> openSet;
    private HashSet<Node> closedSet;

    public AStar(ObstacleGrid og)
    {
        obstacles = og;
    }

    public List<int2> FindPath(int2 startPos, int2 targetPos)
    {
        if (obstacles.InBounds(startPos))
            return new List<int2>();

        this.openSet.Clear();
        this.closedSet.Clear();

        Node start = new Node(startPos);
        Node target = new Node(targetPos);

        this.openSet.Add(start);

        while (openSet.Count > 0)
        {
            // Get open node with lowest f cost
            int2 current = GetLowestFCostNode(this.openSet);
            Node curNode = new Node(current);

            // Close node
            this.openSet.Remove(curNode);
            this.closedSet.Add(curNode);

            // Check if target found
            if (curNode == target)
                break;

            // Evaluate non-closed, non-obstacle neighbors
            foreach (Node neighbor in this.GetNeighbors(curNode))
            {
                if (neighbor == null || !obstacles.IsBlocked(neighbor.coordinate) || this.closedSet.Contains(neighbor))
                    continue;

                // Check if this node needs to be added/updated (unexplored node, or found lower g-cost)
                int newNeighborGCost = curNode.gCost + this.GetDistance(curNode, neighbor);
                if (newNeighborGCost < neighbor.gCost || !this.openSet.Contains(neighbor))
                {
                    // Update costs
                    neighbor.gCost = newNeighborGCost;
                    neighbor.hCost = GetDistance(neighbor, target);
                    neighbor.parent = curNode;

                    // Open node
                    if (!this.openSet.Contains(neighbor))
                        this.openSet.Add(neighbor);
                }
            }
        }

        return this.RetracePath(start, target);
    }

    private List<int2> RetracePath(Node start, Node end)
    {
        List<int2> path = new();
        Node current = end;

        while (current != null && current != start)
        {
            path.Add(current.coordinate);
            current = current.parent;
        }

        path.Reverse();
        return path;
    }

    private int GetDistance(Node a, Node b)
    {
        // Prioritize diagonal moves from A
        //      (with simplified length floor(10 * sqrt(2)))
        // to get to the same axis as B, then use straight-line distance
        //      (with simplified length floor(10 * 1))

        (int x, int y) = (math.abs(b.coordinate.x - a.coordinate.x), math.abs(b.coordinate.y - a.coordinate.y));
        return x > y
            ? 14 * y + 10 * (x - y)
            : 14 * x + 10 * (y - x);
    }

    private List<Node> GetNeighbors(Node node)
    {
        List<Node> neighbors = new List<Node>();
        for (int i = -1; i <= 1; i++)
        {
            for (int j = -1; j <= 1; j++)
            {
                int newX = node.coordinate.x + i;
                int newY = node.coordinate.y + j;

                // Skip self
                if (i == 0 && j == 0)
                    continue;

                Node neighbor = new Node(new int2(newX, newY));
                if (obstacles.InBounds(new int2(newX, newY)))
                    neighbors.Add(neighbor);
            }
        }
        return neighbors;
    }

    private int2 GetLowestFCostNode(List<Node> set)
    {
        if (set.Count < 1)
            return new int2(-1, -1);

        Node lowest = set[0];
        foreach (Node node in set)
        {
            if (node.fCost < lowest.fCost || node.fCost == lowest.fCost && node.hCost < lowest.hCost)
                lowest = node;
        }
        return lowest.coordinate;
    }
}