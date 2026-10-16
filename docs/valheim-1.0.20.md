# Valheim 1.0.20 compatibility

This fork targets Valheim 1.0.20. Upstream main and its latest release were still 0.10.2.0 on 2026-10-16; shared-pin PR #163 remained open and unmerged. The combined feature/shared-map-pins branch keeps our pin protocol, controller support, and earlier smelter guard/diagnostics.

Required source changes found by comparing installed 1.0.20 assemblies with our saved 1.0.16 references:

- Smelter.Spawn and Fermenter.DelayedTap now use CheatUtils.IsCheated(ZDO), which checks the integer cheated state for value 2. V+ auto-deposit must use that helper instead of GetBool(s_cheated).
- CharacterDrop.Drop adds m_levelMultiplierNotAmount. V+ must copy this field when applying LootDrop modifiers so star levels can increase trophy chance rather than trophy quantity, as vanilla intends.
- Set the declared minimum and target game versions to 1.0.20. The upstream plugin version remains 0.10.2.0; this is a fork build identified by its commit and artifact hash, not an upstream release.

Validation: builds against the locally installed 1.0.20 game assemblies (Version.CurrentVersion confirmed); 589 game-member references and 200 explicit Harmony targets resolve in the unmerged plugin; the final DLL including ServerSync resolves 631 references and 211 targets; 43 shared-pin simulated-peer checks, 14 extracted smelter-prefix checks including integer cheat states, and two production loot-prefix checks pass. The Minimap code used by our pin hooks is unchanged; its sole class diff is a world-version constant used for cached textures. Smelter's sole class diff is its cheated-state check. The changed craft-from-chest, resource recovery and player-limit transpiler anchor windows were inspected and remain present.

Limits: metadata checks do not execute Harmony replacements or validate every conditional transpiler. The automated tests use game stubs, not Unity. A live 1.0.20 dedicated-server/two-client test remains necessary. Install the identical fork DLL on the server and clients; stop them before replacing it. Keep TXC SharedMap disabled when using our pin feature. Back up the previous DLL and BepInEx/vplus-data shared-pin files. The pin protocol and persisted file format have not changed, so no pin migration is required.

Official patch notes: https://www.valheimgame.com/news/patch-1-0-20/
Upstream shared-pin PR: https://github.com/Grantapher/ValheimPlus/pull/163
