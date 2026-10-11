using UnityEngine;

namespace TimboJimbo.UINext.Demos.Arena
{
    /// <summary>How rare a card is: its frame's colour and how many it takes to upgrade.</summary>
    public enum Rarity
    {
        Common,
        Rare,
        Epic,
        Legendary,
    }

    /// <summary>The kinds of chest, each its own colours.</summary>
    public enum ChestKind
    {
        Wooden,
        Silver,
        Golden,
        Magical,
    }

    /// <summary>What a chest slot holds: nothing, a chest waiting to be unlocked, one unlocking, or one ready to open.</summary>
    public enum ChestState
    {
        Empty,
        Locked,
        Unlocking,
        Ready,
    }

    /// <summary>Arena's made-up player, cards, chests, shop, clan and events.</summary>
    public static class ArenaData
    {
        public const string PlayerName = "Timbo";
        public const int PlayerLevel = 11;
        public const int Experience = 3200;
        public const int ExperienceNeeded = 5000;
        public const int Trophies = 4120;
        public const int NextReward = 4300;
        public const int Gold = 18450;
        public const int Gems = 312;
        public const string ArenaName = "Arena 7 · Frost Hollow";

        /// <summary>Every card: its name, its picture (an emoji), its cost, rarity, level, how many you have and need.</summary>
        public static readonly (string Name, string Emoji, int Elixir, Rarity Rarity, int Level, int Count, int Needed)[] Cards =
        {
            ("Knight", "⚔️", 3, Rarity.Common, 11, 640, 800),
            ("Archers", "🏹", 3, Rarity.Common, 10, 820, 800),
            ("Goblins", "👺", 2, Rarity.Common, 10, 312, 800),
            ("Fireball", "🔥", 4, Rarity.Rare, 9, 214, 200),
            ("Giant", "👹", 5, Rarity.Rare, 9, 96, 200),
            ("Wizard", "🧙", 5, Rarity.Rare, 8, 150, 200),
            ("Baby Dragon", "🐉", 4, Rarity.Epic, 7, 12, 20),
            ("Skeletons", "💀", 1, Rarity.Common, 11, 410, 800),
            ("Balloon", "🎈", 5, Rarity.Epic, 6, 22, 20),
            ("Ice Spirit", "❄️", 1, Rarity.Common, 10, 500, 800),
            ("Princess", "👸", 3, Rarity.Legendary, 5, 1, 2),
            ("Lightning", "⚡", 6, Rarity.Epic, 6, 8, 20),
            ("Bats", "🦇", 2, Rarity.Common, 9, 260, 400),
            ("Golem", "🗿", 8, Rarity.Epic, 6, 4, 20),
            ("Musketeer", "🎯", 4, Rarity.Rare, 9, 120, 200),
            ("Prince", "🤴", 5, Rarity.Epic, 7, 15, 20),
        };

        /// <summary>The battle deck: eight of the cards, by index.</summary>
        public static readonly int[] Deck = { 0, 1, 2, 3, 4, 5, 6, 7 };

        /// <summary>The four chest slots.</summary>
        public static readonly (ChestKind Kind, ChestState State, int Hours)[] Slots =
        {
            (ChestKind.Silver, ChestState.Ready, 3),
            (ChestKind.Golden, ChestState.Locked, 8),
            (ChestKind.Silver, ChestState.Locked, 3),
            (ChestKind.Wooden, ChestState.Empty, 0),
        };

        /// <summary>The daily deals: a card, how many, and the gold they cost (0 is free).</summary>
        public static readonly (int Card, int Count, int Price)[] Deals =
        {
            (1, 10, 0),
            (3, 6, 1200),
            (6, 1, 3000),
            (0, 40, 400),
            (5, 8, 1600),
            (8, 2, 6000),
        };

        /// <summary>Chests for gems.</summary>
        public static readonly (ChestKind Kind, int Price)[] ShopChests =
        {
            (ChestKind.Golden, 60),
            (ChestKind.Magical, 250),
            (ChestKind.Silver, 30),
        };

        /// <summary>Gold for gems.</summary>
        public static readonly (int Gold, int Price)[] GoldOffers =
        {
            (1000, 60),
            (10000, 500),
            (100000, 4500),
        };

        /// <summary>Gems for money (not for sale in a demo).</summary>
        public static readonly (int Gems, string Price)[] GemOffers =
        {
            (80, "$0.99"),
            (500, "$4.99"),
            (1200, "$9.99"),
        };

        /// <summary>The clan's chat: who, their role, and what they said; a request for cards has its card and how many came.</summary>
        public static readonly (string Name, string Role, string Text, int Card, int Have, int Need)[] Chat =
        {
            ("Ava", "Leader", "Clan war starts tonight, everyone in!", -1, 0, 0),
            ("Marco", "Elder", "", 3, 2, 8),
            ("Lin", "Member", "gg on that last push", -1, 0, 0),
            ("Sofia", "Co-leader", "", 0, 31, 40),
            ("Ravi", "Member", "anyone want to 2v2?", -1, 0, 0),
            ("Ava", "Leader", "", 9, 12, 40),
        };

        /// <summary>The events: a title, what it is, its colours, its picture, and how long it runs.</summary>
        public static readonly (string Title, string Subtitle, Color From, Color To, string Emoji, string Ends)[] Events =
        {
            ("Dragon Draft", "Pick cards as you go. 12 wins for the top prize.", new Color32(0xF2, 0x6B, 0x3A, 0xFF), new Color32(0x9C, 0x1F, 0x4A, 0xFF), "🐉", "2d 5h"),
            ("Triple Elixir", "Everything costs the same; elixir flows three times as fast.", new Color32(0xC0, 0x4F, 0xE8, 0xFF), new Color32(0x52, 0x2A, 0xB8, 0xFF), "💧", "18h"),
            ("Mirror Match", "Both players get the same random deck.", new Color32(0x3A, 0xC4, 0xD8, 0xFF), new Color32(0x1D, 0x5C, 0xB0, 0xFF), "🪞", "4d 1h"),
            ("Weekend Tournament", "Climb the ladder of 50 players for gems.", new Color32(0xF5, 0xC0, 0x3A, 0xFF), new Color32(0xC0, 0x6A, 0x12, 0xFF), "🏆", "1d 9h"),
        };

        /// <summary>A chest's name.</summary>
        public static string ChestName(ChestKind kind) => kind switch
        {
            ChestKind.Wooden => "Wooden Chest",
            ChestKind.Silver => "Silver Chest",
            ChestKind.Golden => "Golden Chest",
            _ => "Magical Chest",
        };

        /// <summary>A chest's body and trim colours.</summary>
        public static (Color Body, Color Trim) ChestColours(ChestKind kind) => kind switch
        {
            ChestKind.Wooden => (new Color32(0xA8, 0x6A, 0x3A, 0xFF), new Color32(0x6E, 0x40, 0x1E, 0xFF)),
            ChestKind.Silver => (new Color32(0xB9, 0xC6, 0xD8, 0xFF), new Color32(0x6E, 0x7E, 0x96, 0xFF)),
            ChestKind.Golden => (new Color32(0xF5, 0xC5, 0x3B, 0xFF), new Color32(0xB0, 0x74, 0x10, 0xFF)),
            _ => (new Color32(0xA3, 0x5B, 0xF2, 0xFF), new Color32(0x5B, 0x26, 0xA8, 0xFF)),
        };

        /// <summary>A card frame's colours for a rarity: top and bottom of its gradient.</summary>
        public static (Color Top, Color Bottom) RarityColours(Rarity rarity) => rarity switch
        {
            Rarity.Common => (new Color32(0x8F, 0xB4, 0xE8, 0xFF), new Color32(0x4A, 0x6F, 0xB0, 0xFF)),
            Rarity.Rare => (new Color32(0xFF, 0xC0, 0x5C, 0xFF), new Color32(0xE0, 0x7A, 0x1F, 0xFF)),
            Rarity.Epic => (new Color32(0xD6, 0x8A, 0xFF, 0xFF), new Color32(0x8B, 0x3B, 0xD9, 0xFF)),
            _ => (new Color32(0x6F, 0xF0, 0xD8, 0xFF), new Color32(0xE8, 0xC9, 0x4A, 0xFF)),
        };

        /// <summary>What a card's stats are at a level: hitpoints and damage, growing about a tenth a level.</summary>
        public static (int Hitpoints, int Damage) Stats(int card, int level)
        {
            var (_, _, elixir, _, _, _, _) = Cards[card];
            float growth = Mathf.Pow(1.1f, level - 1);
            return (Mathf.RoundToInt(elixir * 140f * growth), Mathf.RoundToInt(elixir * 32f * growth));
        }

        /// <summary>What upgrading a card from a level costs in gold.</summary>
        public static int UpgradeCost(int level) => level switch
        {
            < 7 => 1000,
            < 9 => 4000,
            < 11 => 20000,
            _ => 50000,
        };

        /// <summary>A number as the game writes it: 18,450.</summary>
        public static string Number(int value) => value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
    }
}
