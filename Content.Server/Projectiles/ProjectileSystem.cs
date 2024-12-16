using Content.Server.Administration.Logs;
using Content.Server.Destructible;
using Content.Server.Effects;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Camera;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.Projectiles;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using Content.Shared.StatusEffect;
using Content.Shared.Eye.Blinding.Components; // Frontier
using Content.Shared.Eye.Blinding.Systems; // Frontier
using Robust.Shared.Random; // Frontier
using Content.Server.Chat.Systems; // Frontier
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using Robust.Shared.Physics.Components;
using System.Linq;
using System.Numerics;
using Content.Shared.Physics;
using Robust.Shared.Physics;

namespace Content.Server.Projectiles;

public sealed class ProjectileSystem : SharedProjectileSystem;
