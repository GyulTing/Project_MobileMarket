using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MobileMarket.Systems;

namespace MobileMarket.UI
{
    /// <summary>
    /// Shows one ItemGrid as a columns x rows block of ItemCellUIs on top of a background drawing.
    /// The grid is centered on (and scaled to fit) a normalized area of that drawing. Cells are pooled and only rebuilt when the grid changes.
    /// </summary>
    public class ItemGridViewUI : MonoBehaviour
    {
        [SerializeField] private ItemCellUI cellTemplate;
        [SerializeField] private GridLayoutGroup layout;
        [SerializeField] private Text title;
        [SerializeField] private Image background;
        [SerializeField] private float maxCellSize = 64f;
        [SerializeField] private float minCellSize = 40f;

        private readonly List<ItemCellUI> cells = new List<ItemCellUI>();
        private ItemGrid grid;
        private InventoryScreenUI screen;
        private Rect gridArea = new Rect(0.2f, 0.2f, 0.6f, 0.6f);

        /// <summary>Shows the grid. Passing art swaps the background drawing and the area of it the grid fits into.</summary>
        public void Bind(InventoryScreenUI owner, ItemGrid target, string heading, Sprite art = null, Rect? area = null)
        {
            screen = owner;
            title.text = heading;
            if (art != null) background.sprite = art;
            if (area.HasValue) gridArea = area.Value;

            if (grid != target)
            {
                if (grid != null) grid.OnChanged -= Refresh;
                grid = target;
                if (grid != null) grid.OnChanged += Refresh;
            }
            FitToArea();
            Refresh();
        }

        public void Unbind()
        {
            if (grid != null) grid.OnChanged -= Refresh;
            grid = null;
        }

        private void OnDestroy() => Unbind();

        /// <summary>Places the cells over gridArea of the (aspect-preserved) background and sizes them so every column and row fits.</summary>
        private void FitToArea()
        {
            if (grid == null || background.sprite == null) return;

            var panel = ((RectTransform)background.transform).rect.size;
            var spriteSize = background.sprite.rect.size;
            float scale = Mathf.Min(panel.x / spriteSize.x, panel.y / spriteSize.y);
            var shown = spriteSize * scale;

            var areaSize = new Vector2(gridArea.width * shown.x, gridArea.height * shown.y);
            var areaCenter = new Vector2((gridArea.center.x - 0.5f) * shown.x, (gridArea.center.y - 0.5f) * shown.y);

            float spacing = layout.spacing.x;
            float padding = layout.padding.left + layout.padding.right;
            float fit = Mathf.Min((areaSize.x - padding) / grid.Columns, (areaSize.y - padding) / grid.Rows) - spacing;
            float cell = Mathf.Clamp(Mathf.Floor(fit), minCellSize, maxCellSize);

            layout.cellSize = new Vector2(cell, cell);
            ((RectTransform)layout.transform).anchoredPosition = areaCenter;
        }

        private void Refresh()
        {
            if (grid == null) return;

            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = grid.Columns;

            while (cells.Count < grid.Size)
            {
                var cell = Instantiate(cellTemplate, layout.transform);
                cell.Screen = screen;
                cells.Add(cell);
            }

            for (int i = 0; i < cells.Count; i++)
            {
                bool used = i < grid.Size;
                cells[i].gameObject.SetActive(used);
                if (!used) continue;
                cells[i].Screen = screen;
                cells[i].BindGrid(grid, i);
                cells[i].Show(grid[i]);
            }
        }
    }
}
