using UnityEngine;

namespace NtingCampusMapEditor
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CampusInteractionSensor))]
    public sealed class CampusInteractionController : MonoBehaviour
    {
        public CampusInteractionSensor Sensor;
        public CampusInteractionPromptView PromptView;
        public KeyCode InteractKey = KeyCode.E;
        public bool PollInput = true;
        public bool RefreshEveryFrame = true;
        public bool AutoCreatePromptView = true;

        public CampusInteractionTarget CurrentTarget { get; private set; }

        private void Awake()
        {
            CacheReferences(true);
            ApplyKeyLabel();
        }

        private void Reset()
        {
            CacheReferences(true);
            ApplyKeyLabel();
        }

        private void OnValidate()
        {
            CacheReferences(false);
            ApplyKeyLabel();
        }

        private void Update()
        {
            if (RefreshEveryFrame)
            {
                RefreshTarget();
            }

            if (PollInput && CampusInteractionInput.WasKeyPressed(InteractKey))
            {
                TryInteractCurrent();
            }
        }

        private void OnDisable()
        {
            if (PromptView != null)
            {
                PromptView.HideImmediate();
            }
        }

        public void SetFacingDirection(Vector2 direction)
        {
            CacheReferences(true);
            if (Sensor != null)
            {
                Sensor.SetFacingDirection(direction);
            }
        }

        public bool TryInteractCurrent()
        {
            if (!CurrentTarget.IsValid)
            {
                RefreshTarget();
            }

            if (!CurrentTarget.IsValid || !CurrentTarget.CanInteract)
            {
                return false;
            }

            CurrentTarget.Interact(gameObject);
            RefreshTarget();
            return true;
        }

        public void RefreshTarget()
        {
            CacheReferences(true);
            ApplyKeyLabel();

            if (Sensor != null && Sensor.TryGetBestTarget(gameObject, CurrentTarget, out CampusInteractionTarget target))
            {
                CurrentTarget = target;
                if (PromptView != null)
                {
                    PromptView.Show(target);
                }

                return;
            }

            CurrentTarget = default;
            if (PromptView != null)
            {
                PromptView.Hide();
            }
        }

        public CampusInteractionSensor GetOrCreateSensor()
        {
            CacheReferences(true);
            return Sensor;
        }

        private void CacheReferences(bool createMissing)
        {
            if (Sensor == null)
            {
                Sensor = GetComponent<CampusInteractionSensor>();
            }

            if (Sensor == null && createMissing)
            {
                Sensor = gameObject.AddComponent<CampusInteractionSensor>();
            }

            if (PromptView == null)
            {
                PromptView = GetComponentInChildren<CampusInteractionPromptView>(true);
            }

            if (PromptView == null && AutoCreatePromptView && createMissing)
            {
                GameObject viewObject = new GameObject("交互提示", typeof(RectTransform));
                viewObject.transform.SetParent(transform, false);
                PromptView = viewObject.AddComponent<CampusInteractionPromptView>();
            }
        }

        private void ApplyKeyLabel()
        {
            if (Sensor != null)
            {
                Sensor.DefaultKeyText = CampusInteractionInput.GetKeyLabel(InteractKey);
            }
        }
    }
}
