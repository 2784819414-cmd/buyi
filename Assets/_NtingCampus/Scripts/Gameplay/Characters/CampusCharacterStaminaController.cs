using Nting.Storage;
using NtingCampus.Gameplay.Inventory;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    [RequireComponent(typeof(CampusCharacterRuntime))]
    [DisallowMultipleComponent]
    public sealed class CampusCharacterStaminaController : MonoBehaviour
    {
        [SerializeField] private CampusCharacterRuntime runtime;
        [SerializeField, Min(0f)] private float currentStamina;
        [SerializeField, Min(1f)] private float maxStamina = CampusCharacterStaminaTuning.BaseMaxStamina;
        [SerializeField] private bool sprinting;
        [SerializeField] private bool exhausted;
        [SerializeField] private bool initialized;

        public float CurrentStamina => currentStamina;
        public float MaxStamina => maxStamina;
        public bool IsSprinting => sprinting;
        public bool IsExhausted => exhausted;
        public float NormalizedStamina => maxStamina > 0.001f ? currentStamina / maxStamina : 0f;

        private void Awake()
        {
            EnsureSetup();
        }

        private void Reset()
        {
            runtime = runtime != null ? runtime : GetComponent<CampusCharacterRuntime>();
            maxStamina = CampusCharacterStaminaTuning.ResolveMaxStamina(ResolveBackpackLoad01());
            currentStamina = maxStamina;
            sprinting = false;
            exhausted = false;
            initialized = true;
        }

        public void EnsureSetup()
        {
            runtime = runtime != null ? runtime : GetComponent<CampusCharacterRuntime>();
            if (!initialized)
            {
                maxStamina = CampusCharacterStaminaTuning.ResolveMaxStamina(ResolveBackpackLoad01());
                currentStamina = maxStamina;
                sprinting = false;
                exhausted = false;
                initialized = true;
                return;
            }

            RefreshCapacity();
        }

        public float ResolveMoveSpeed(Vector2 moveInput, bool wantsSprint, float deltaTime)
        {
            EnsureSetup();

            bool hasMoveInput = moveInput.sqrMagnitude > 0.0001f;
            float elapsedSeconds = Mathf.Max(0f, deltaTime);

            if (currentStamina <= CampusCharacterStaminaTuning.SprintMinimumStamina)
            {
                currentStamina = 0f;
                exhausted = true;
            }
            else if (exhausted && currentStamina >= ResolveSprintResumeStamina())
            {
                exhausted = false;
            }

            sprinting = hasMoveInput &&
                wantsSprint &&
                !exhausted &&
                currentStamina > CampusCharacterStaminaTuning.SprintMinimumStamina;

            if (sprinting)
            {
                currentStamina = Mathf.Max(
                    0f,
                    currentStamina - CampusCharacterStaminaTuning.SprintCostPerSecond * elapsedSeconds);
                if (currentStamina <= CampusCharacterStaminaTuning.SprintMinimumStamina)
                {
                    currentStamina = 0f;
                    sprinting = false;
                    exhausted = true;
                }
            }
            else
            {
                currentStamina = Mathf.Min(
                    maxStamina,
                    currentStamina + CampusCharacterStaminaTuning.RecoveryPerSecond * elapsedSeconds);
            }

            return sprinting
                ? CampusCharacterMovementTuning.ResolvePlayerSprintSpeed()
                : CampusCharacterMovementTuning.ResolvePlayerMoveSpeed();
        }

        private void RefreshCapacity()
        {
            float previousMaxStamina = maxStamina;
            maxStamina = CampusCharacterStaminaTuning.ResolveMaxStamina(ResolveBackpackLoad01());
            if (previousMaxStamina <= 0.001f)
            {
                currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
                return;
            }

            currentStamina = Mathf.Min(currentStamina, maxStamina);
            if (currentStamina >= ResolveSprintResumeStamina())
            {
                exhausted = false;
            }
        }

        private float ResolveSprintResumeStamina()
        {
            return maxStamina * CampusCharacterStaminaTuning.SprintResumeStamina01;
        }

        private float ResolveBackpackLoad01()
        {
            if (runtime == null)
            {
                return 0f;
            }

            CampusCharacterInventory inventory = CampusCharacterInventoryService.GetOrCreateInventory(runtime, false);
            StorageContainerModel backpack = inventory != null ? inventory.Backpack : null;
            if (inventory == null || !inventory.HasBackpack || backpack == null || backpack.MaxWeight <= 0.001f)
            {
                return 0f;
            }

            return Mathf.Clamp01(backpack.CurrentWeight / backpack.MaxWeight);
        }
    }

    public static class CampusCharacterStaminaTuning
    {
        public const float BaseMaxStamina = 100f;
        public const float FullLoadMaxStaminaMultiplier = 0.55f;
        public const float SprintCostPerSecond = 24f;
        public const float RecoveryPerSecond = 18f;
        public const float SprintMinimumStamina = 0.01f;
        public const float SprintResumeStamina01 = 0.2f;

        public static float ResolveMaxStamina(float backpackLoad01)
        {
            return Mathf.Lerp(
                BaseMaxStamina,
                BaseMaxStamina * FullLoadMaxStaminaMultiplier,
                Mathf.Clamp01(backpackLoad01));
        }
    }
}
