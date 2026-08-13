// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Onyx.Abductor;

public abstract class SharedAbductorSystem : EntitySystem
{
    protected virtual void UpdateGui(NetEntity? target, Entity<AbductorConsoleComponent> computer) { }
}
