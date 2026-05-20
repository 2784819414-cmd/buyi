using UnityEngine;

namespace NtingCampusMapEditor
{
    [DisallowMultipleComponent]
    public sealed class CampusCharacterFacingState : MonoBehaviour
    {
        [SerializeField] private Vector2 forward = Vector2.down;

        public Vector2 Forward
        {
            get
            {
                if (forward.sqrMagnitude <= 0.0001f)
                {
                    forward = Vector2.down;
                }

                return forward.normalized;
            }
        }

        public void SetMovementDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude > 0.0001f)
            {
                forward = direction.normalized;
            }
        }

        public void LookAt(Vector3 worldPosition)
        {
            Vector2 direction = (Vector2)(worldPosition - transform.position);
            SetMovementDirection(direction);
        }
    }
}
