using System.Text;

namespace AdaptiveBossArena.Core.Constants
{
    /// <summary>
    /// The boss's name and title, as the player reads them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The boss is a corrupted knight in the image of Igris, the Blood-Red Commander, and takes his name. It was
    /// written out twice, letter-spaced by hand, in the health bar and in the entrance - and renaming the boss meant
    /// finding both. Defined once here, in Core, because the scene builder and the encounter director both show it
    /// and neither can see the other.
    /// </para>
    /// <para>
    /// The health bar shows the name alone: the full title, spaced in the fight's lettering, is wider than the bar.
    /// The entrance has the room, and gives the title its own smaller line beneath the name.
    /// </para>
    /// </remarks>
    public static class BossIdentity
    {
        /// <summary>The boss's name.</summary>
        public const string Name = "Igris";

        /// <summary>The boss's title.</summary>
        public const string Epithet = "The Blood-Red Commander";

        /// <summary>Size of the title's line in the entrance, beneath the name's.</summary>
        private const int EntranceEpithetSize = 26;

        /// <summary>The name over the health bar.</summary>
        public static string HealthBarTitle => Spaced(Name);

        /// <summary>The name and, on a smaller line beneath it, the title, as the boss is met.</summary>
        public static string EntranceTitle => $"{Spaced(Name)}\n<size={EntranceEpithetSize}>{Spaced(Epithet)}</size>";

        /// <summary>Upper-case, a space between letters and three between words: the fight's lettering.</summary>
        /// <param name="text">The words to space.</param>
        /// <returns>The spaced text.</returns>
        public static string Spaced(string text)
        {
            var spaced = new StringBuilder();
            string[] words = text.ToUpperInvariant().Split(' ');

            for (int w = 0; w < words.Length; w++)
            {
                if (w > 0)
                {
                    spaced.Append("   ");
                }

                for (int c = 0; c < words[w].Length; c++)
                {
                    if (c > 0)
                    {
                        spaced.Append(' ');
                    }

                    spaced.Append(words[w][c]);
                }
            }

            return spaced.ToString();
        }
    }
}
