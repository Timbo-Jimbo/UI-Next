using UnityEngine;

namespace TimboJimbo.UINext.Demos.Circle
{
    /// <summary>What Circle shows: its people, stories, posts and chats, and the scenes its photos are of.</summary>
    public static class CircleData
    {
        public static readonly (string Name, string Initials, Color Colour)[] People =
        {
            ("Maya Okafor", "MO", new Color32(0xFF, 0x7E, 0xB6, 0xFF)),
            ("Theo Park", "TP", new Color32(0x5C, 0x8D, 0xFF, 0xFF)),
            ("Priya Shah", "PS", new Color32(0xF2, 0xA9, 0x3B, 0xFF)),
            ("Sam Lund", "SL", new Color32(0x3F, 0xC1, 0xC9, 0xFF)),
            ("Ines Varga", "IV", new Color32(0xA7, 0x7B, 0xFF, 0xFF)),
            ("Jonah Lee", "JL", new Color32(0xFF, 0x6B, 0x6B, 0xFF)),
        };

        public static readonly (string Name, string Initials, Color Colour, string Handle, string Bio) Me =
            ("Alex Kim", "AK", new Color32(0x7E, 0xE0, 0xC9, 0xFF), "@alexkim", "Weekend hiker, weekday tinkerer.");

        /// <summary>The feed's posts: whose, when, what they wrote, the scene its photo is of, and its reactions.</summary>
        public static readonly (int Person, string Time, string Caption, int Scene, (string Emoji, int Count)[] Reactions)[] Posts =
        {
            (1, "2h", "Summit at sunrise. Worth the **5am** start.", 2, new[] { ("🔥", 24), ("😍", 9), ("🤯", 3) }),
            (0, "5h", "New crag, same sore fingers.", 1, new[] { ("🙌", 7), ("🔥", 5) }),
            (2, "Yesterday", "Lemon and thyme. Recipe in the comments!", 6, new[] { ("😍", 31), ("🎉", 6), ("💯", 4) }),
            (3, "2d", "Morning light in the living room.", 7, new[] { ("😍", 4), ("👍", 2) }),
        };

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
    }
}
