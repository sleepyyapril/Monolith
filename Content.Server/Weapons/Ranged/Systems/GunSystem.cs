// SPDX-FileCopyrightText: 2022 Flipp Syder
// SPDX-FileCopyrightText: 2022 Nemanja
// SPDX-FileCopyrightText: 2022 Paul Ritter
// SPDX-FileCopyrightText: 2022 Rane
// SPDX-FileCopyrightText: 2022 Visne
// SPDX-FileCopyrightText: 2022 metalgearsloth
// SPDX-FileCopyrightText: 2022 themias
// SPDX-FileCopyrightText: 2023 AJCM-git
// SPDX-FileCopyrightText: 2023 Arendian
// SPDX-FileCopyrightText: 2023 Chief-Engineer
// SPDX-FileCopyrightText: 2023 Ilya Chvilyov
// SPDX-FileCopyrightText: 2023 Kara
// SPDX-FileCopyrightText: 2023 MendaxxDev
// SPDX-FileCopyrightText: 2023 Slava0135
// SPDX-FileCopyrightText: 2023 TaralGit
// SPDX-FileCopyrightText: 2023 and_a
// SPDX-FileCopyrightText: 2023 deltanedas
// SPDX-FileCopyrightText: 2023 deltanedas <@deltanedas:kde.org>
// SPDX-FileCopyrightText: 2024 Aiden
// SPDX-FileCopyrightText: 2024 Bixkitts
// SPDX-FileCopyrightText: 2024 Cojoke
// SPDX-FileCopyrightText: 2024 DrSmugleaf
// SPDX-FileCopyrightText: 2024 Dvir
// SPDX-FileCopyrightText: 2024 Ed
// SPDX-FileCopyrightText: 2024 Jake Huxell
// SPDX-FileCopyrightText: 2024 Jessica M
// SPDX-FileCopyrightText: 2024 LordCarve
// SPDX-FileCopyrightText: 2024 RiceMar1244
// SPDX-FileCopyrightText: 2024 Sigil
// SPDX-FileCopyrightText: 2024 VividPups
// SPDX-FileCopyrightText: 2024 Whatstone
// SPDX-FileCopyrightText: 2024 beck-thompson
// SPDX-FileCopyrightText: 2024 eoineoineoin
// SPDX-FileCopyrightText: 2024 keronshb
// SPDX-FileCopyrightText: 2024 nikthechampiongr
// SPDX-FileCopyrightText: 2025 Ark
// SPDX-FileCopyrightText: 2025 Leon Friedrich
// SPDX-FileCopyrightText: 2025 SlamBamActionman
// SPDX-FileCopyrightText: 2025 sleepyyapril
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Server.Cargo.Systems;
using Content.Server.Power.EntitySystems;
using Content.Shared.Damage.Systems;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Content.Shared.Interaction; // Frontier
using Content.Shared.Examine; // Frontier
using Content.Shared.Hands.Components;
using Content.Shared.Power;

namespace Content.Server.Weapons.Ranged.Systems;

public sealed partial class GunSystem : SharedGunSystem
{
    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly BatterySystem _battery = default!;
    [Dependency] private readonly DamageExamineSystem _damageExamine = default!;
    [Dependency] private readonly PricingSystem _pricing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BallisticAmmoProviderComponent, PriceCalculationEvent>(OnBallisticPrice);
        SubscribeLocalEvent<AutoShootGunComponent, ActivateInWorldEvent>(OnActivateGun); // Frontier
        SubscribeLocalEvent<AutoShootGunComponent, ComponentInit>(OnGunInit); // Frontier
        SubscribeLocalEvent<AutoShootGunComponent, ComponentShutdown>(OnGunShutdown); // Frontier
        SubscribeLocalEvent<AutoShootGunComponent, ExaminedEvent>(OnGunExamine); // Frontier
        SubscribeLocalEvent<AutoShootGunComponent, PowerChangedEvent>(OnPowerChange); // Frontier
        SubscribeLocalEvent<AutoShootGunComponent, AnchorStateChangedEvent>(OnAnchorChange); // Frontier
    }

    private void OnBallisticPrice(EntityUid uid, BallisticAmmoProviderComponent component, ref PriceCalculationEvent args)
    {
        if (string.IsNullOrEmpty(component.Proto) || component.UnspawnedCount == 0)
            return;

        if (!ProtoManager.TryIndex<EntityPrototype>(component.Proto, out var proto))
        {
            Log.Error($"Unable to find fill prototype for price on {component.Proto} on {ToPrettyString(uid)}");
            return;
        }

        // Probably good enough for most.
        var price = _pricing.GetEstimatedPrice(proto);
        args.Price += price * component.UnspawnedCount;
    }

    protected override void Popup(string message, EntityUid? uid, EntityUid? user) { }

    protected override void CreateEffect(EntityUid gunUid, MuzzleFlashEvent message, EntityUid? user = null, EntityUid? player = null)
    {
        var filter = Filter.Pvs(gunUid, entityManager: EntityManager);
        if (TryComp<ActorComponent>(user, out var actor))
            filter.RemovePlayer(actor.PlayerSession);

        if (GunPrediction && TryComp(player, out actor))
            filter.RemovePlayer(actor.PlayerSession);

        RaiseNetworkEvent(message, filter);
    }


    // TODO: Pseudo RNG so the client can predict these.
    #region Hitscan effects

    private void FireEffects(EntityCoordinates fromCoordinates, float distance, Angle angle, HitscanPrototype hitscan, EntityUid? hitEntity = null, EntityUid? user = null)
    {
        // Raise custom event for radar tracking
        // Use the actual user as shooter instead of trying to derive from coordinates
        var shooter = user ?? GetShooterFromCoordinates(fromCoordinates);
        var radarEv = new _Mono.Radar.HitscanRadarSystem.HitscanFireEffectEvent(fromCoordinates, distance, angle, hitscan, hitEntity, shooter);
        RaiseLocalEvent(radarEv);

        // Lord
        // Forgive me for the shitcode I am about to do
        // Effects tempt me not
        var sprites = new List<(NetCoordinates coordinates, Angle angle, SpriteSpecifier sprite, float scale)>();
        var fromXform = Transform(fromCoordinates.EntityId);

        // We'll get the effects relative to the grid / map of the firer
        // Look you could probably optimise this a bit with redundant transforms at this point.

        var gridUid = fromXform.GridUid;
        if (gridUid != fromCoordinates.EntityId && TryComp(gridUid, out TransformComponent? gridXform))
        {
            var (_, gridRot, gridInvMatrix) = TransformSystem.GetWorldPositionRotationInvMatrix(gridXform);
            var map = TransformSystem.ToMapCoordinates(fromCoordinates);
            fromCoordinates = new EntityCoordinates(gridUid.Value, Vector2.Transform(map.Position, gridInvMatrix));
            angle -= gridRot;
        }
        else
        {
            angle -= TransformSystem.GetWorldRotation(fromXform);
        }

        if (distance >= 1f)
        {
            if (hitscan.MuzzleFlash != null)
            {
                var coords = fromCoordinates.Offset(angle.ToVec().Normalized() / 2);
                var netCoords = GetNetCoordinates(coords);

                sprites.Add((netCoords, angle, hitscan.MuzzleFlash, 1f));
            }

            if (hitscan.TravelFlash != null)
            {
                var coords = fromCoordinates.Offset(angle.ToVec() * (distance + 0.5f) / 2);
                var netCoords = GetNetCoordinates(coords);

                sprites.Add((netCoords, angle, hitscan.TravelFlash, distance - 1.5f));
            }
        }

        if (hitscan.ImpactFlash != null)
        {
            var coords = fromCoordinates.Offset(angle.ToVec() * distance);
            var netCoords = GetNetCoordinates(coords);

            sprites.Add((netCoords, angle.FlipPositive(), hitscan.ImpactFlash, 1f));
        }

        if (sprites.Count > 0)
        {
            RaiseNetworkEvent(new HitscanEvent
            {
                Sprites = sprites,
            }, Filter.Pvs(fromCoordinates, entityMan: EntityManager));
        }
    }

    // Helper method to find the shooter from coordinates, typically a gun or the coordinate's entity
    private EntityUid? GetShooterFromCoordinates(EntityCoordinates coordinates)
    {
        var entity = coordinates.EntityId;

        // Check if the entity is a gun
        if (HasComp<GunComponent>(entity))
            return entity;

        // Try to find a parent that might be the shooter
        if (TryComp<TransformComponent>(entity, out var transform) && transform.ParentUid != EntityUid.Invalid)
        {
            if (HasComp<GunComponent>(transform.ParentUid))
                return transform.ParentUid;

            // If parent has hands, it might be the shooter
            if (HasComp<HandsComponent>(transform.ParentUid))
                return transform.ParentUid;
        }

        // Default to the entity itself
        return entity;
    }

    #endregion
}
