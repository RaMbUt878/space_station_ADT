using Content.Shared.Changeling.Components;
using Content.Shared.Store;

namespace Content.Server.Store.Conditions;

/// <summary>
/// Only allows a listing to be purchased while buyer can refresh.
/// </summary>
public sealed partial class ChangelingLastResortNotUsedCondition : ListingCondition
{
    public override bool Condition(ListingConditionArgs args)
    {
        if (!args.EntityManager.TryGetComponent<ChangelingComponent>(args.Buyer, out var ling))
            return false;
        if (ling.LastResortUsed)
            return false;

        return true;
    }
}
