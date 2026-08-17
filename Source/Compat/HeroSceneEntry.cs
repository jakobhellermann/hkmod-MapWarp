using System;

namespace MapWarp.Compat;

internal static class HeroSceneEntry {
    /// Runs once the hero's entry position is settled and before the entry sequence finishes. 1.2.2.1 has no
    /// PositionHeroAtSceneEntrance; BeginScene calls EnterHero at that point. EnterHero's dreamGate branch never
    /// positions the hero and calls FinishedEnteringScene itself, so there the handler runs before the original.
    internal static void OnPositioned(Action<GameManager> handler) =>
#if HK1221
        Hooks.Add(typeof(GameManager), "EnterHero",
            (Action<Action<GameManager, bool>, GameManager, bool>)((orig, self, additiveGateSearch) => {
                handler(self);
                orig(self, additiveGateSearch);
            }));
#else
        Hooks.Add(typeof(GameManager), "PositionHeroAtSceneEntrance",
            (Action<Action<GameManager>, GameManager>)((orig, self) => {
                orig(self);
                handler(self);
            }));
#endif

    /// Postfix after the game finished the scene entry sequence. 1.2.2.1's FinishedEnteringScene predates the
    /// preventRunBob parameter.
    internal static void OnEntered(Action<HeroController> handler) =>
#if HK1221
        Hooks.Add(typeof(HeroController), "FinishedEnteringScene",
            (Action<Action<HeroController, bool>, HeroController, bool>)((orig, self, setHazardMarker) => {
                orig(self, setHazardMarker);
                handler(self);
            }));
#else
        Hooks.Add(typeof(HeroController), "FinishedEnteringScene",
            (Action<Action<HeroController, bool, bool>, HeroController, bool, bool>)(
                (orig, self, setHazardMarker, preventRunBob) => {
                    orig(self, setHazardMarker, preventRunBob);
                    handler(self);
                }));
#endif
}
