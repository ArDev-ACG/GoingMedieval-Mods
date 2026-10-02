
using NSMedieval.FloatingOverlaySystem;
using NSMedieval.State;
using NSMedieval.View;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// Says on screen what the log has been saying all along.
    ///
    /// Two things the player could not see until now:
    ///
    ///   - <b>a bite that never landed.</b> The order is given, the vampire
    ///     walks over, the victim wanders off and the goal quietly ends. From
    ///     the outside that is indistinguishable from a button that does
    ///     nothing, which is exactly how it was reported.
    ///   - <b>a bite on an animal.</b> A goat has no mood, so it has no thought
    ///     bubble, so nothing at all happened on screen - the health bar moved
    ///     a few percent and that was the entire feedback.
    ///
    /// Both get the same bubble the game puts over a settler when an effector
    /// starts, because it is the one floating element the player is already
    /// trained to read. HumanoidBehaviour.OnEffectorStarted builds it in three
    /// steps - find the view, ask it for its overlay hook, hand the hook and an
    /// icon name to the factory - and none of the three care whether the
    /// creature is a person or a goat: AnimatedAgentView is the base of both.
    ///
    /// The icon names are sprite-asset addresses, so they only resolve once
    /// <see cref="ModSpriteAssets"/> has registered the PNG. That is by design:
    /// the same registration that fixes the role shield fixes these.
    /// </summary>
    internal static class BiteFeedback
    {
        /// <summary>Two punctures and a drop: something drank here.</summary>
        internal const string BittenIcon = "aldrich_bubble_bite_mark";

        /// <summary>The same fangs behind a struck-through ring: it got away.</summary>
        internal const string MissedIcon = "aldrich_bubble_bite_missed";

        internal static void Bubble(CreatureBase creature, string icon)
        {
            if (creature == null || creature.HasDisposed || string.IsNullOrEmpty(icon)) return;

            try
            {
                // Vanilla guards this with MonoSingleton<FloatingOverlayManager>
                // .IsInstantiated(), which a mod cannot say out loud - the
                // manager is internal to the game assembly. The view is the
                // same guarantee one step down: a creature only has an
                // AnimatedAgentView with an overlay hook once the world is up
                // and drawing it, which is the only state where the manager
                // exists. Anything the check would have caught lands in the
                // catch instead.
                var view = creature.GetAgentView<AnimatedAgentView>();
                if (view == null) return;

                var hook = view.GetGuiOverlayHookTransform();
                if (hook == null) return;

                // The second icon is the small mood arrow vanilla puts next to
                // the glyph. There is no mood behind either of these, so it
                // stays empty and the bubble draws the one icon.
                FloatingElementFactory.ProduceThoughtBubbleElement(hook, icon, string.Empty);
            }
            catch (System.Exception e)
            {
                GMPlugin.Log?.LogError($"[bite] could not show {icon}: {e.Message}");
            }
        }
    }
}
