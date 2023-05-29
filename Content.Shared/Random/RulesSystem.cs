using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;

namespace Content.Shared.Random;

public sealed class RulesSystem : EntitySystem
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDef = default!;
    [Dependency] private readonly AccessReaderSystem _reader = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public bool IsTrue(EntityUid uid, RulesPrototype rules)
    {
        foreach (var rule in rules.Rules)
        {
            switch (rule)
            {
                case AlwaysTrueRule:
                    break;
                case GridInRangeRule griddy:
                {
                    if (!TryComp<TransformComponent>(uid, out var xform))
                    {
                        return false;
                    }

                    if (xform.GridUid != null)
                    {
                        return !griddy.Inverted;
                    }

                    var worldPos = _transform.GetWorldPosition(xform);

                    foreach (var _ in _mapManager.FindGridsIntersecting(
                                 xform.MapID,
                                 new Box2(worldPos - griddy.Range, worldPos + griddy.Range)))
                    {
                        return !griddy.Inverted;
                    }

                    break;
                }
                case InSpaceRule:
                {
                    if (!TryComp<TransformComponent>(uid, out var xform) ||
                        xform.GridUid != null)
                    {
                        return false;
                    }

                    break;
                }
                case NearbyAccessRule access:
                {
                    var xformQuery = GetEntityQuery<TransformComponent>();

                    if (!xformQuery.TryGetComponent(uid, out var xform) ||
                        xform.MapUid == null)
                    {
                        return false;
                    }

                    var found = false;
                    var worldPos = _transform.GetWorldPosition(xform, xformQuery);
                    var count = 0;

                    // TODO: Update this when we get the callback version
                    foreach (var comp in _lookup.GetComponentsInRange<AccessReaderComponent>(xform.MapID,
                                 worldPos, access.Range))
                    {
                        if (!_reader.AreAccessTagsAllowed(access.Access, comp) ||
                            access.Anchored &&
                            (!xformQuery.TryGetComponent(comp.Owner, out var compXform) ||
                             !compXform.Anchored))
                        {
                            continue;
                        }

                        count++;

                        if (count < access.Count)
                            continue;

                        found = true;
                        break;
                    }

                    if (!found)
                        return false;

                    break;
                }
                case NearbyComponentsRule nearbyComps:
                {
                    var xformQuery = GetEntityQuery<TransformComponent>();

                    if (!xformQuery.TryGetComponent(uid, out var xform) ||
                        xform.MapUid == null)
                    {
                        return false;
                    }

                    var found = false;
                    var worldPos = _transform.GetWorldPosition(xform);
                    var count = 0;

                    foreach (var compType in nearbyComps.Components.Values)
                    {
                        // TODO: Update this when we get the callback version
                        foreach (var comp in _lookup.GetComponentsInRange(compType.Component.GetType(), xform.MapID,
                                     worldPos, nearbyComps.Range))
                        {
                            if (nearbyComps.Anchored &&
                                (!xformQuery.TryGetComponent(comp.Owner, out var compXform) ||
                                 !compXform.Anchored))
                            {
                                continue;
                            }

                            count++;

                            if (count < nearbyComps.Count)
                                continue;

                            found = true;
                            break;
                        }

                        if (found)
                            break;
                    }

                    if (!found)
                        return false;

                    break;
                }
                case NearbyEntitiesRule entity:
                {
                    if (!TryComp<TransformComponent>(uid, out var xform) ||
                        xform.MapUid == null)
                    {
                        return false;
                    }

                    var found = false;
                    var worldPos = _transform.GetWorldPosition(xform);
                    var count = 0;

                    foreach (var ent in _lookup.GetEntitiesInRange(xform.MapID, worldPos, entity.Range))
                    {
                        if (!entity.Whitelist.IsValid(ent, EntityManager))
                            continue;

                        count++;

                        if (count < entity.Count)
                            continue;

                        found = true;
                        break;
                    }

                    if (!found)
                        return false;

                    break;
                }
                case NearbyTilesPercentRule tiles:
                {
                    if (!TryComp<TransformComponent>(uid, out var xform) ||
                        !TryComp<MapGridComponent>(xform.GridUid, out var grid))
                    {
                        return false;
                    }

                    var tileCount = 0;
                    var matchingTileCount = 0;

                    foreach (var tile in grid.GetTilesIntersecting(new Circle(_transform.GetWorldPosition(xform),
                                 tiles.Range)))
                    {
                        tileCount++;

                        if (!tiles.Tiles.Contains(_tileDef[tile.Tile.TypeId].ID))
                            continue;

                        if (tiles.IgnoreAnchored && grid.GetAnchoredEntitiesEnumerator(tile.GridIndices).MoveNext(out _))
                            continue;

                        matchingTileCount++;
                    }

                    if (matchingTileCount / (float) tileCount < tiles.Percent)
                        return false;

                    break;
                }
                case OnMapGridRule:
                {
                    if (!TryComp<TransformComponent>(uid, out var xform) ||
                        xform.GridUid != xform.MapUid ||
                        xform.MapUid == null)
                    {
                        return false;
                    }

                    break;
                }
                default:
                    throw new NotImplementedException();
            }
        }

        return true;
    }
}