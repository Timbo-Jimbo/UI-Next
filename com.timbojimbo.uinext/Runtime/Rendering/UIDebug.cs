namespace TimboJimbo.UINext
{
    /// <summary>
    /// Views for seeing what the UI costs and how focus is shaped, drawn over the UI in the Game and Scene views (in
    /// builds too). The Rendering Debugger's UI Next panel turns them on and off, in the editor and in development builds.
    /// </summary>
    public static class UIDebug
    {
        private static bool s_overdraw;
        private static bool s_layerBounds;
        private static bool s_focusPath;

        /// <summary>Raised as a view is turned on or off.</summary>
        public static event System.Action Changed;

        /// <summary>
        /// Overdraw: what is behind the UI is darkened, and every pixel the GPU shades for it adds heat, brighter the more
        /// often it is drawn over (a quad's clear corners count too: they are shaded all the same; what clips cut away
        /// and what an opaque shape covers mostly are not). What is drawn into a layer's image shows through the layer's
        /// quad.
        /// </summary>
        public static bool Overdraw
        {
            get => s_overdraw;
            set => Set(ref s_overdraw, value);
        }

        /// <summary>
        /// Layer bounds: an outline where each layer's image lies (a node drawn to an image of its own first), coloured
        /// by why it is one: cyan fading as one (a composite group, or what a transition fades), magenta blurred, yellow masked, green one end of a
        /// matched flight.
        /// </summary>
        public static bool LayerBounds
        {
            get => s_layerBounds;
            set => Set(ref s_layerBounds, value);
        }

        /// <summary>
        /// Focus path: an outline round what has focus (white), and round every section (teal) and group (orange) it is
        /// in, out to its presentation, following them as they move.
        /// </summary>
        public static bool FocusPath
        {
            get => s_focusPath;
            set => Set(ref s_focusPath, value);
        }

        /// <summary>
        /// How many times over the UI is drawn each frame (1 as it is): for measuring what it costs the GPU. A phone's GPU
        /// slows down when it has little to do, which hides what its work costs; drawn several times over, the UI keeps it
        /// busy, at its full clock, and the time a frame takes it, less what it takes with nothing drawn, over the number of
        /// times, is what one drawing costs.
        /// </summary>
        public static int Repeat
        {
            get => s_repeat;
            set => s_repeat = value < 1 ? 1 : value;
        }

        private static int s_repeat = 1;

        /// <summary>
        /// Checks every layout pass that solves only what can have moved against solving every node again, logging an
        /// error for each node laid out anywhere else: for testing layout. Every such pass is solved twice over.
        /// </summary>
        public static bool VerifyLayout { get; set; }

        private static void Set(ref bool field, bool value)
        {
            if (field == value) return;
            field = value;
            Compositor.MarkOrderDirty();
            Changed?.Invoke();
        }
    }
}
