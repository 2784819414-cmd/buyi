using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Inventory;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Retail
{
    internal sealed class CampusRetailCharacterActionProvider : ICampusCharacterActionProvider
    {
        public static readonly CampusRetailCharacterActionProvider Instance =
            new CampusRetailCharacterActionProvider();

        public string ProviderId => "campus_retail_character_actions";

        public bool TryExecute(CampusCharacterActionContext context, out StorageTransferResult result)
        {
            result = StorageTransferResult.Fail(string.Empty);
            if (context.Actor == null)
            {
                return false;
            }

            if (CampusCharacterActionUtility.IdEquals(context.ActionId, CampusRetailActionIds.PickFromShelf))
            {
                if (!TryResolveShelf(context.Target, out CampusRetailShelf shelf))
                {
                    result = StorageTransferResult.Fail(CampusRetailTextCatalog.Get(CampusRetailTextId.ShelfUnconfigured));
                    return true;
                }

                return shelf.TryTakeOneForActor(context.Actor, out result);
            }

            return false;
        }

        private static bool TryResolveShelf(Object target, out CampusRetailShelf shelf)
        {
            shelf = null;
            if (target is CampusRetailShelf directShelf)
            {
                shelf = directShelf;
                return true;
            }

            Component component = ResolveComponent(target);
            if (component == null)
            {
                return false;
            }

            shelf = component.GetComponent<CampusRetailShelf>() ??
                    component.GetComponentInParent<CampusRetailShelf>();
            return shelf != null;
        }

        private static Component ResolveComponent(Object target)
        {
            if (target is Component component)
            {
                return component;
            }

            if (target is GameObject gameObject)
            {
                return gameObject.GetComponent<Component>();
            }

            return null;
        }
    }
}
