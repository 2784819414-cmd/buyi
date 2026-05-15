using UnityEngine;

namespace NtingCampus.Gameplay.Rooms
{
    [DisallowMultipleComponent]
    public sealed class CampusGameplayRoomMarker : MonoBehaviour
    {
        [SerializeField] private string roomIdOverride = string.Empty;
        [SerializeField] private string roomDisplayName = string.Empty;
        [SerializeField] private CampusRoomType roomType = CampusRoomType.Unknown;
        [SerializeField, Min(1)] private int floorIndex = 1;
        [SerializeField] private Vector3Int anchorCell;
        [SerializeField] private Vector2Int roomSize = new Vector2Int(1, 1);
        [SerializeField] private bool usableForGameplay = true;
        [SerializeField] private Color debugColor = new Color(0.2f, 0.85f, 0.5f, 0.25f);

        public string RoomIdOverride => roomIdOverride;
        public string RoomDisplayName => roomDisplayName;
        public CampusRoomType RoomType => roomType;
        public int FloorIndex => floorIndex;
        public Vector3Int AnchorCell => anchorCell;
        public Vector2Int RoomSize => new Vector2Int(Mathf.Max(1, roomSize.x), Mathf.Max(1, roomSize.y));
        public bool UsableForGameplay => usableForGameplay;

        public BoundsInt BuildBounds()
        {
            Vector2Int normalizedSize = RoomSize;
            return new BoundsInt(anchorCell, new Vector3Int(normalizedSize.x, normalizedSize.y, 1));
        }

        public void Configure(
            string idOverride,
            string displayName,
            CampusRoomType type,
            int targetFloorIndex,
            Vector3Int cell,
            Vector2Int size,
            bool gameplayUsable)
        {
            roomIdOverride = string.IsNullOrWhiteSpace(idOverride) ? string.Empty : idOverride.Trim();
            roomDisplayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();
            roomType = type;
            floorIndex = Mathf.Max(1, targetFloorIndex);
            anchorCell = cell;
            roomSize = new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
            usableForGameplay = gameplayUsable;
        }

        private void OnDrawGizmosSelected()
        {
            BoundsInt bounds = BuildBounds();
            Vector3 center = transform.position + new Vector3((bounds.size.x - 1) * 0.5f, (bounds.size.y - 1) * 0.5f, 0f);
            Gizmos.color = debugColor;
            Gizmos.DrawCube(center, new Vector3(bounds.size.x, bounds.size.y, 0.1f));
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(center, new Vector3(bounds.size.x, bounds.size.y, 0.1f));
        }
    }
}
