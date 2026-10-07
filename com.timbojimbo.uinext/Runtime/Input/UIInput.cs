namespace TimboJimbo.UINext
{
    /// <summary>Pointer, wheel and navigation input from the Input System, routed to gestures on the nodes under it.</summary>
    internal static partial class UIInput
    {
        internal static void Hook() => HookInput();

        static partial void HookInput();
    }
}
