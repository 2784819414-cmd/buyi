using UnityEngine;

namespace NtingCampus.UI.Runtime.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class CampusRuntimeGameplayOverlayEntity : MonoBehaviour
    {
        [SerializeField] private bool actorEntity;
        [SerializeField, Min(1)] private int floorIndex = 1;
        [SerializeField] private Vector3Int cell;

        public bool IsActorEntity => actorEntity;
        public int FloorIndex => Mathf.Max(1, floorIndex);
        public Vector3Int Cell => cell;

        public void Configure(bool isActorEntity, int targetFloorIndex, Vector3Int targetCell)
        {
            actorEntity = isActorEntity;
            floorIndex = Mathf.Max(1, targetFloorIndex);
            cell = targetCell;
        }
    }
}

