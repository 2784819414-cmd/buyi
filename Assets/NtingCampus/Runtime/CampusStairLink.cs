using UnityEngine;

namespace NtingCampusMapEditor
{
    /// <summary>
    /// Connects two floor cells. Runtime trigger logic is intentionally minimal for the current editor milestone.
    /// </summary>
    public sealed class CampusStairLink : MonoBehaviour
    {
        public int FromFloor = 1;
        public int ToFloor = 2;
        public Vector3Int FromCell;
        public Vector3Int ToCell;
        public Vector3Int SecondaryCell;
        public int Rotation90;
        public int FootprintLength = 2;
        public string LinkId;
        public bool IsAutoReturnStair;
        public bool AutoUnlockTargetFloor = true;
        public Transform OptionalSpawnPoint;

        public Vector3Int PrimaryCell => FromCell;

        public bool ContainsCell(Vector3Int cell)
        {
            return cell == FromCell || cell == GetSecondaryCell();
        }

        public Vector3Int GetSecondaryCell()
        {
            if (SecondaryCell != Vector3Int.zero || FromCell == Vector3Int.zero)
            {
                return SecondaryCell;
            }

            return FromCell + DirectionFromRotation(Rotation90);
        }

        public static Vector3Int DirectionFromRotation(int rotation90)
        {
            int normalized = ((rotation90 % 4) + 4) % 4;
            switch (normalized)
            {
                case 1:
                    return Vector3Int.right;
                case 2:
                    return Vector3Int.down;
                case 3:
                    return Vector3Int.left;
                default:
                    return Vector3Int.up;
            }
        }

        private void Reset()
        {
            EnsureTriggerCollider();
        }

        private void OnValidate()
        {
            EnsureTriggerCollider();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null || !IsPlayer(other.gameObject))
            {
                return;
            }

            CampusFloorVisibilityController controller = GetComponentInParent<CampusFloorVisibilityController>();
            if (controller == null)
            {
                CampusMapRoot root = GetComponentInParent<CampusMapRoot>();
                controller = root != null ? root.GetComponent<CampusFloorVisibilityController>() : FindFirstObjectByType<CampusFloorVisibilityController>();
            }

            if (controller != null)
            {
                controller.SetPlayerFloor(ToFloor);
            }
        }

        private void EnsureTriggerCollider()
        {
            Collider2D trigger = GetComponent<Collider2D>();
            if (trigger != null)
            {
                trigger.isTrigger = true;
                return;
            }

            BoxCollider2D box = gameObject.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(0.8f, 1.8f);
        }

        private static bool IsPlayer(GameObject candidate)
        {
            try
            {
                return candidate != null && candidate.CompareTag("Player");
            }
            catch (UnityException)
            {
                return false;
            }
        }
    }
}
