using UnityEngine;
using System.Collections.Generic;

namespace DungeonPainter.Data
{
    /// <summary>
    /// Represents a reusable room template
    /// </summary>
    [System.Serializable]
    public class RoomTemplate
    {
        public string name;
        public List<Vector2Int> cells;
        public Vector2Int size;
        public string description;

        public RoomTemplate(string name, List<Vector2Int> cells)
        {
            this.name = name;
            this.cells = new List<Vector2Int>(cells);
            CalculateSize();
        }

        private void CalculateSize()
        {
            if (cells.Count == 0)
            {
                size = Vector2Int.zero;
                return;
            }

            Vector2Int min = cells[0];
            Vector2Int max = cells[0];

            foreach (var cell in cells)
            {
                min.x = Mathf.Min(min.x, cell.x);
                min.y = Mathf.Min(min.y, cell.y);
                max.x = Mathf.Max(max.x, cell.x);
                max.y = Mathf.Max(max.y, cell.y);
            }

            size = new Vector2Int(max.x - min.x + 1, max.y - min.y + 1);
        }

        public List<Vector2Int> GetNormalizedCells()
        {
            if (cells.Count == 0)
                return new List<Vector2Int>();

            Vector2Int min = cells[0];
            foreach (var cell in cells)
            {
                min.x = Mathf.Min(min.x, cell.x);
                min.y = Mathf.Min(min.y, cell.y);
            }

            List<Vector2Int> normalized = new List<Vector2Int>();
            foreach (var cell in cells)
            {
                normalized.Add(cell - min);
            }

            return normalized;
        }
    }

    /// <summary>
    /// Collection of built-in room templates
    /// </summary>
    public static class RoomTemplates
    {
        public static List<RoomTemplate> GetBuiltInTemplates()
        {
            List<RoomTemplate> templates = new List<RoomTemplate>();

            // Small square room (3x3)
            templates.Add(new RoomTemplate("Small Square", CreateSquare(3)));

            // Medium square room (5x5)
            templates.Add(new RoomTemplate("Medium Square", CreateSquare(5)));

            // Large square room (8x8)
            templates.Add(new RoomTemplate("Large Square", CreateSquare(8)));

            // Small rectangle (3x5)
            templates.Add(new RoomTemplate("Small Horizontal", CreateRectangle(5, 3)));

            // Medium rectangle (5x8)
            templates.Add(new RoomTemplate("Medium Horizontal", CreateRectangle(8, 5)));

            // L-Shape room
            templates.Add(new RoomTemplate("L-Shape", CreateLShape()));

            // T-Shape room
            templates.Add(new RoomTemplate("T-Shape", CreateTShape()));

            // Plus/Cross shape
            templates.Add(new RoomTemplate("Cross", CreateCross()));

            // Corridor (1x10)
            templates.Add(new RoomTemplate("Corridor", CreateRectangle(10, 1)));

            // Small circular-ish
            templates.Add(new RoomTemplate("Small Round", CreateCircular(3)));

            // Medium circular
            templates.Add(new RoomTemplate("Medium Round", CreateCircular(5)));

            return templates;
        }

        private static List<Vector2Int> CreateSquare(int size)
        {
            List<Vector2Int> cells = new List<Vector2Int>();
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    cells.Add(new Vector2Int(x, y));
                }
            }
            return cells;
        }

        private static List<Vector2Int> CreateRectangle(int width, int height)
        {
            List<Vector2Int> cells = new List<Vector2Int>();
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    cells.Add(new Vector2Int(x, y));
                }
            }
            return cells;
        }

        private static List<Vector2Int> CreateLShape()
        {
            List<Vector2Int> cells = new List<Vector2Int>();
            // Horizontal part
            for (int x = 0; x < 5; x++)
            {
                cells.Add(new Vector2Int(x, 0));
                cells.Add(new Vector2Int(x, 1));
            }
            // Vertical part
            for (int y = 2; y < 5; y++)
            {
                cells.Add(new Vector2Int(0, y));
                cells.Add(new Vector2Int(1, y));
            }
            return cells;
        }

        private static List<Vector2Int> CreateTShape()
        {
            List<Vector2Int> cells = new List<Vector2Int>();
            // Top horizontal bar
            for (int x = 0; x < 7; x++)
            {
                cells.Add(new Vector2Int(x, 0));
                cells.Add(new Vector2Int(x, 1));
            }
            // Vertical stem
            for (int y = 2; y < 6; y++)
            {
                cells.Add(new Vector2Int(3, y));
            }
            return cells;
        }

        private static List<Vector2Int> CreateCross()
        {
            List<Vector2Int> cells = new List<Vector2Int>();
            int size = 7;
            int center = size / 2;

            // Horizontal bar
            for (int x = 0; x < size; x++)
            {
                cells.Add(new Vector2Int(x, center));
            }

            // Vertical bar
            for (int y = 0; y < size; y++)
            {
                cells.Add(new Vector2Int(center, y));
            }

            return cells;
        }

        private static List<Vector2Int> CreateCircular(int radius)
        {
            List<Vector2Int> cells = new List<Vector2Int>();
            int diameter = radius * 2 + 1;

            for (int x = 0; x < diameter; x++)
            {
                for (int y = 0; y < diameter; y++)
                {
                    int dx = x - radius;
                    int dy = y - radius;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    if (distance <= radius)
                    {
                        cells.Add(new Vector2Int(x, y));
                    }
                }
            }

            return cells;
        }
    }
}
