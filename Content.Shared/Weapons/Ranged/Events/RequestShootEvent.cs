// SPDX-FileCopyrightText: 2022 Kara
// SPDX-FileCopyrightText: 2023 metalgearsloth
// SPDX-FileCopyrightText: 2024 Aiden
// SPDX-FileCopyrightText: 2024 DrSmugleaf
// SPDX-FileCopyrightText: 2025 Ark
// SPDX-FileCopyrightText: 2025 sleepyyapril
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Weapons.Ranged.Events;

/// <summary>
/// Raised on the client to indicate it'd like to shoot.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestShootEvent : EntityEventArgs
{
    public NetEntity Gun;
    public NetCoordinates Coordinates;
    public NetEntity? Target;
    public List<int>? Shot;
}
