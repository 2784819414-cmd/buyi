using UnityEngine;

namespace NtingCampus.UI.Runtime.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class CampusHandHudController : MonoBehaviour
    {
        private void Awake()
        {
            if (GetComponent<CampusGameplayHudController>() == null)
            {
                gameObject.AddComponent<CampusGameplayHudController>();
            }

            Destroy(this);
        }
    }
}
