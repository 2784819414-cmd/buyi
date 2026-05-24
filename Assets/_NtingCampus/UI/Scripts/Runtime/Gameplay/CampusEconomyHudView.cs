using UnityEngine;

namespace NtingCampus.UI.Runtime.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class CampusEconomyHudView : MonoBehaviour
    {
        private void Awake()
        {
            Destroy(this);
        }
    }
}
