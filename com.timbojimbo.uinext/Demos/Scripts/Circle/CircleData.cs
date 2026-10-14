using UnityEngine;

namespace TimboJimbo.UINext.Demos.Circle
{
    /// <summary>
    /// What Circle shows: its people, their stories, the feed's posts, their reactions and comments, the chats, and the
    /// scenes its photos are of.
    /// </summary>
    public static class CircleData
    {
        /// <summary>Who is in the app: name, initials, colour, handle and a line about them.</summary>
        public static readonly (string Name, string Initials, Color Colour, string Handle, string Bio)[] People =
        {
            ("Maya Okafor", "MO", new Color32(0xFF, 0x7E, 0xB6, 0xFF), "@maya", "Climbing, coffee and very long walks."),
            ("Theo Park", "TP", new Color32(0x5C, 0x8D, 0xFF, 0xFF), "@theo.park", "Photographer. Chasing sunrises."),
            ("Priya Shah", "PS", new Color32(0xF2, 0xA9, 0x3B, 0xFF), "@priyashah", "Designer by day, baker by night."),
            ("Sam Lund", "SL", new Color32(0x3F, 0xC1, 0xC9, 0xFF), "@samlund", "Plants, mostly. One very brave fern."),
            ("Ines Varga", "IV", new Color32(0xA7, 0x7B, 0xFF, 0xFF), "@ines", "Board games and bad puns."),
            ("Jonah Lee", "JL", new Color32(0xFF, 0x6B, 0x6B, 0xFF), "@jonahlee", "Always up for a rematch."),
        };

        /// <summary>The person using the app.</summary>
        public static readonly (string Name, string Initials, Color Colour, string Handle, string Bio) Me =
            ("Alex Kim", "AK", new Color32(0x7E, 0xE0, 0xC9, 0xFF), "@alexkim", "Weekend hiker, weekday tinkerer.");

        /// <summary>The stories along the top of the feed: whose, what it says, and its colours top and bottom.</summary>
        public static readonly (int Person, string Text, Color Top, Color Bottom)[] Stories =
        {
            (0, "First climb of the season", new Color32(0xFF, 0x7E, 0xB6, 0xFF), new Color32(0x6D, 0x59, 0x7A, 0xFF)),
            (1, "Up at five for this view", new Color32(0xF4, 0xA2, 0x61, 0xFF), new Color32(0x2B, 0x2D, 0x5C, 0xFF)),
            (2, "Croissants, attempt number four", new Color32(0xF2, 0xA9, 0x3B, 0xFF), new Color32(0x8C, 0x4A, 0x2F, 0xFF)),
            (3, "The fern lives", new Color32(0x7E, 0xE0, 0xC9, 0xFF), new Color32(0x1D, 0x5C, 0x8C, 0xFF)),
            (4, "Game night, Saturday?", new Color32(0xA7, 0x7B, 0xFF, 0xFF), new Color32(0x2B, 0x2D, 0x5C, 0xFF)),
        };

        /// <summary>
        /// The feed's posts: whose, when, what they wrote, the scene its photo is of, and the reactions it has (an index
        /// into <see cref="Reactions"/>, and how many).
        /// </summary>
        public static readonly (int Person, string Time, string Caption, int Scene, (int Emoji, int Count)[] Reactions)[] Posts =
        {
            (1, "2h", "Summit at sunrise. Worth the 5am start.", 2, new[] { (0, 24), (1, 9), (10, 3) }),
            (0, "5h", "New crag, same sore fingers.", 1, new[] { (8, 7), (0, 5), (7, 2) }),
            (2, "Yesterday", "Lemon and thyme. Recipe in the comments!", 6, new[] { (1, 31), (4, 6), (9, 4) }),
            (3, "2d", "Morning light in the living room.", 7, new[] { (1, 4), (3, 2) }),
        };

        /// <summary>The reactions a post can have, in the order the picker shows them.</summary>
        public static readonly string[] Reactions = { "🔥", "😍", "😂", "👍", "🎉", "😮", "😢", "👀", "🙌", "💯", "🤯", "🥳" };

        /// <summary>The comments under a post: who, and what they said.</summary>
        public static readonly (int Person, string Text)[] Comments =
        {
            (1, "This is unreal"),
            (2, "Saving this for later"),
            (4, "Okay, I need the recipe"),
            (5, "How early is 5am exactly"),
            (0, "Next time I'm coming with you"),
            (3, "The light!"),
        };

        /// <summary>What a new post says, as it is drafted on the compose card.</summary>
        public const string Draft = "Dinner at six? I found a place by the station with a terrace.";

        /// <summary>The chats: with whom, when, the last line, and the conversation (theirs, mine, theirs).</summary>
        public static readonly (int Person, string Time, string Preview, string[] Thread)[] Chats =
        {
            (0, "9:41", "See you at the station at six?", new[] { "Are we still on for tonight?", "Yes! Can't wait.", "See you at the station at six?" }),
            (1, "9:12", "Sent you the photos", new[] { "Sent you the photos from the hike", "These are amazing", "The summit one is my favourite" }),
            (2, "Yesterday", "Running ten minutes late", new[] { "Running ten minutes late, sorry!", "No rush, I'll grab a table", "You're the best" }),
            (3, "Yesterday", "The fern survived", new[] { "The fern survived the week", "Even the big one?", "Even the big one." }),
            (4, "Mon", "Game night moved to Saturday", new[] { "Game night moved to Saturday", "Same time?", "Same time, new place: mine" }),
            (5, "Sun", "Rematch next week?", new[] { "Good game today", "You got lucky on the last round", "Rematch next week?" }),
        };

        /// <summary>What a conversation offers to send back.</summary>
        public static readonly string[] Replies = { "On my way!", "Sounds good", "Haha, yes" };

        /// <summary>What the photos are of, standing in for pictures: a sky down to a glow at the horizon, and the hills under it.</summary>
        /// <summary>The photos, on a CDN that makes copies at any width and shape (see the builder's loader).</summary>
        public static readonly string[] Photos =
        {
            "https://cdn.staticshiftracing.com/build-share-dev/0b0ec06f-bdae-4576-b2f2-a1f160a965cc.jpeg",
            "https://cdn.staticshiftracing.com/build-share-dev/e182026f-c3e8-4d0c-aaec-31b56eb608fa.jpeg",
            "https://cdn.staticshiftracing.com/build-share-dev/f84c08c0-d8bd-46ab-b3c2-720c3c72301a.jpeg",
        };

        public static readonly (Color Sky, Color Glow, Color Hills)[] Scenes =
        {
            (new Color32(0x5F, 0xA8, 0xD3, 0xFF), new Color32(0xCF, 0xE8, 0xF3, 0xFF), new Color32(0x3E, 0x6B, 0x5A, 0xFF)),
            (new Color32(0x7F, 0xB7, 0xBE, 0xFF), new Color32(0xD3, 0xE4, 0xCD, 0xFF), new Color32(0x58, 0x6F, 0x4E, 0xFF)),
            (new Color32(0x6D, 0x59, 0x7A, 0xFF), new Color32(0xF4, 0xA2, 0x61, 0xFF), new Color32(0x2E, 0x24, 0x3A, 0xFF)),
            (new Color32(0x0B, 0x1D, 0x3A, 0xFF), new Color32(0x5B, 0x4B, 0x8A, 0xFF), new Color32(0x06, 0x0E, 0x1E, 0xFF)),
            (new Color32(0x9D, 0xB4, 0xC0, 0xFF), new Color32(0xE3, 0xED, 0xF2, 0xFF), new Color32(0x6A, 0x7D, 0x86, 0xFF)),
            (new Color32(0x8E, 0xA8, 0xD8, 0xFF), new Color32(0xF2, 0xD0, 0xE0, 0xFF), new Color32(0x5D, 0x5A, 0x8C, 0xFF)),
            (new Color32(0x4F, 0x9C, 0xD6, 0xFF), new Color32(0xFF, 0xE0, 0xB2, 0xFF), new Color32(0x8C, 0x5A, 0x3C, 0xFF)),
            (new Color32(0x3A, 0x86, 0xC8, 0xFF), new Color32(0xBD, 0xE0, 0xFE, 0xFF), new Color32(0x2F, 0x5D, 0x7A, 0xFF)),
        };

        /// <summary>The scene of the <paramref name="index"/>th photo on <paramref name="person"/>'s profile.</summary>
        public static int ProfileScene(int person, int index) => (person * 3 + index * 7) % Scenes.Length;

        /// <summary>The first of someone's names.</summary>
        public static string FirstName(string name)
        {
            int space = name.IndexOf(' ');
            return space > 0 ? name.Substring(0, space) : name;
        }
    }
}
