#nullable enable
using System;
using System.Linq;

namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// The content rules of the map and the bridge (<c>04</c> section 14, rules 13b and 13c; M1-03): a site or an area to open needs a map; the
    /// start area and every area an <see cref="OpenLandEffect"/> opens is one of the map's, and no effect opens the start area; forest and
    /// rock take time to cross; the bridge and <see cref="UnlockBridgeEffect"/> come together; the bridge is faster than the ground and
    /// costs the currency only, as a road does (rule 11).
    /// </summary>
    internal static class MapRules
    {
        /// <summary>Runs every map and bridge rule on the draft and reports each break.</summary>
        public static void Check(ContentDraft draft, ContentErrorList errors)
        {
            CheckMap(draft, errors);
            CheckBridge(draft, errors);
        }

        /// <summary>Rule 13b: the map's own values, and every site and area to open against it.</summary>
        private static void CheckMap(ContentDraft draft, ContentErrorList errors)
        {
            MapDefinition? map = draft.Map;
            if (map is null)
            {
                bool needsMap = draft.Buildings.Any(building => building.Site != BuildingSite.Any)
                    || draft.Research.Any(node => node.Effects.OfType<OpenLandEffect>().Any());
                if (needsMap)
                {
                    errors.Add(ContentErrorCode.MissingValue, "map", "A building's site or an effect that opens land needs a map.");
                }

                return;
            }

            if (map.RoughTileMillis <= 0)
            {
                errors.Add(ContentErrorCode.NotPositive, "map.roughTileMillis", FormattableString.Invariant($"Must be above 0, but is {map.RoughTileMillis}."));
            }

            if (!map.AreaLetters.Contains(map.StartArea))
            {
                errors.Add(ContentErrorCode.UnknownReference, "map.startArea", $"No tile of the map is in area '{map.StartArea}'.");
            }

            foreach (ResearchDefinition node in draft.Research.Where(draft.IsFirst))
            {
                for (int index = 0; index < node.Effects.Count; index++)
                {
                    if (!(node.Effects[index] is OpenLandEffect open))
                    {
                        continue;
                    }

                    string path = ContentPaths.Effect(node, index) + ".area";
                    if (!map.AreaLetters.Contains(open.Area))
                    {
                        errors.Add(ContentErrorCode.UnknownReference, path, $"No tile of the map is in area '{open.Area}'.");
                    }
                    else if (open.Area == map.StartArea)
                    {
                        errors.Add(ContentErrorCode.InvalidEffectTarget, path, $"Area '{open.Area}' is the start area, open from the start.");
                    }
                }
            }
        }

        /// <summary>Rule 13c: the bridge and its unlock come together; the bridge is faster than the ground and costs the currency only.</summary>
        private static void CheckBridge(ContentDraft draft, ContentErrorList errors)
        {
            bool unlocked = draft.Research.Any(node => node.Effects.OfType<UnlockBridgeEffect>().Any());
            BridgeDefinition? bridge = draft.Bridge;
            if (bridge is null)
            {
                if (unlocked)
                {
                    errors.Add(ContentErrorCode.MissingValue, "bridge", "An effect unlocks the bridge, so the content needs one.");
                }

                return;
            }

            if (!unlocked)
            {
                errors.Add(ContentErrorCode.BuildingUnreachable, "bridge", "No research unlocks the bridge, so it can never be built.");
            }

            if (bridge.TileMillis <= 0)
            {
                errors.Add(ContentErrorCode.NotPositive, "bridge.tileMillis", FormattableString.Invariant($"Must be above 0, but is {bridge.TileMillis}."));
            }
            else if (bridge.TileMillis >= draft.Rules.GroundTileMillis)
            {
                errors.Add(
                    ContentErrorCode.RoadNotFaster,
                    "bridge.tileMillis",
                    FormattableString.Invariant($"A bridge is faster than the ground ({draft.Rules.GroundTileMillis} ms), but takes {bridge.TileMillis} ms."));
            }

            foreach (ItemQuantity amount in bridge.Cost)
            {
                string path = "bridge.cost." + amount.Item.Value;
                if (!draft.ItemsById.TryGetValue(amount.Item, out ItemDefinition? item))
                {
                    errors.Add(ContentErrorCode.UnknownReference, path, amount.Item.Value + " is not an item of the content.");
                }
                else if (!item.IsCurrency)
                {
                    errors.Add(ContentErrorCode.InvalidCurrencyUse, path, amount.Item.Value + " is a good; a bridge costs the currency only, as it is built at once.");
                }
                else if (amount.Quantity < 0)
                {
                    errors.Add(ContentErrorCode.Negative, path, FormattableString.Invariant($"Must be at least 0, but is {amount.Quantity}."));
                }
            }
        }
    }
}
