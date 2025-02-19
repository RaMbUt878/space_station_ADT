using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.EntitySerialization;
using System.Numerics;
using System.Linq;
using Robust.Shared.Utility;

/*
    ADT Content by 🐾 Schrödinger's Code 🐾
    ╔════════════════════════════════════╗
    ║   Schrödinger's Cat Code   🐾      ║
    ║   /\_/\\                           ║
    ║  ( o.o )  Meow!                    ║
    ║   > ^ <                            ║
    ╚════════════════════════════════════╝

*/


namespace Content.Server.Administration.Systems;

/// <summary>
/// This handles the administrative test arena maps, and loading them.
/// </summary>
public sealed class AdminTestArenaVariableSystem : EntitySystem
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly MapLoaderSystem _map = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;


    public Dictionary<(NetUserId, string), EntityUid> ArenaMap { get; private set; } = new();
    public Dictionary<(NetUserId, string), EntityUid?> ArenaGrid { get; private set; } = new();

    public (EntityUid Map, EntityUid? Grid) AssertArenaLoaded(
        ICommonSession admin,
        string pachGridAdminRoom,
        string prefixNameAdminRoom)
    {
        if (ArenaMap.TryGetValue((admin.UserId, prefixNameAdminRoom), out var arenaMap)
            && !Deleted(arenaMap) && !Terminating(arenaMap))
        {
            if (ArenaGrid.TryGetValue((admin.UserId, prefixNameAdminRoom), out var arenaGrid)
                && arenaGrid.HasValue && !Deleted(arenaGrid.Value) && !Terminating(arenaGrid.Value))
            {
                return (arenaMap, arenaGrid);
            }
            else
            {
                ArenaGrid[(admin.UserId, prefixNameAdminRoom)] = null;
                return (arenaMap, null);
            }
        }

        var key = (admin.UserId, prefixNameAdminRoom);
        ArenaMap[key] = _mapManager.GetMapEntityId(_mapManager.CreateMap());
        _metaDataSystem.SetEntityName(ArenaMap[key], $"{prefixNameAdminRoom}M-{admin.Name}");

        _map.TryLoadMapWithId(Comp<MapComponent>(ArenaMap[key]).MapId, new ResPath(pachGridAdminRoom), out _, out var grids);
        if (grids?.Count > 0)
        {
            var firstEntity = grids.First();
            _metaDataSystem.SetEntityName(firstEntity, $"{prefixNameAdminRoom}G-{admin.Name}");
            ArenaGrid[key] = firstEntity;
        }
        else
        {
            ArenaGrid[key] = null;
        }

        return (ArenaMap[key], ArenaGrid[key]);
    }
}
