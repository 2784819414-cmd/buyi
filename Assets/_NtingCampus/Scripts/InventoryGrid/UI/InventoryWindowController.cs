using UnityEngine;
using UnityEngine.UI;

namespace NtingCampus.InventoryGrid
{
    [DisallowMultipleComponent]
    public sealed class InventoryWindowController : MonoBehaviour
    {
        public InventoryGridView leftGrid;
        public InventoryGridView rightGrid;
        public Text leftTitle;
        public Text rightTitle;

        public void OpenContainers(InventoryContainerRuntime left, InventoryContainerRuntime right)
        {
            if (leftGrid == null || rightGrid == null)
            {
                Debug.LogWarning("Inventory window open failed: grid views are not bound.");
                return;
            }

            leftGrid.SetWindowController(this);
            rightGrid.SetWindowController(this);
            leftGrid.Bind(left);
            rightGrid.Bind(right);
            RefreshTitles();
        }

        public bool TryQuickTransfer(PlacedItem item, InventoryGridView fromGrid)
        {
            if (item == null || item.item == null || fromGrid == null || fromGrid.Container == null)
            {
                Debug.LogWarning("Inventory quick transfer failed: source item or grid is invalid.");
                return false;
            }

            InventoryGridView targetGrid = ResolveTransferTarget(fromGrid);
            if (targetGrid == null || targetGrid.Container == null)
            {
                Debug.LogWarning("Inventory quick transfer failed: target grid is not open.");
                return false;
            }

            if (!targetGrid.Container.TryAutoPlace(item.item))
            {
                return false;
            }

            if (fromGrid.Container.RemoveItem(item))
            {
                RefreshAll();
                return true;
            }

            PlacedItem rollbackItem = targetGrid.Container.FindPlacedItem(item.item);
            if (rollbackItem != null)
            {
                targetGrid.Container.RemoveItem(rollbackItem);
            }

            Debug.LogWarning("Inventory quick transfer failed: source remove failed, target placement rolled back.");
            RefreshAll();
            return false;
        }

        public void RefreshAll()
        {
            if (leftGrid != null)
            {
                leftGrid.SetWindowController(this);
                leftGrid.Refresh();
            }

            if (rightGrid != null)
            {
                rightGrid.SetWindowController(this);
                rightGrid.Refresh();
            }

            RefreshTitles();
        }

        private InventoryGridView ResolveTransferTarget(InventoryGridView fromGrid)
        {
            if (fromGrid == leftGrid)
            {
                return rightGrid;
            }

            if (fromGrid == rightGrid)
            {
                return leftGrid;
            }

            return null;
        }

        private void RefreshTitles()
        {
            if (leftTitle != null)
            {
                leftTitle.text = GetTitle(leftGrid);
            }

            if (rightTitle != null)
            {
                rightTitle.text = GetTitle(rightGrid);
            }
        }

        private static string GetTitle(InventoryGridView grid)
        {
            if (grid == null || grid.Container == null || grid.Container.definition == null)
            {
                return "未打开";
            }

            InventoryContainerDefinition definition = grid.Container.definition;
            string name = string.IsNullOrWhiteSpace(definition.displayName) ? definition.containerId : definition.displayName;
            return name + "  " + definition.width + "x" + definition.height +
                   "  " + grid.Container.GetCurrentWeight().ToString("0.#") + "/" + definition.maxWeight.ToString("0.#") + "kg";
        }
    }
}
