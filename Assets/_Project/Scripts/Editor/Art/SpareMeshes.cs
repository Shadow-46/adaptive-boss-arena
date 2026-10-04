namespace AdaptiveBossArena.Editor.Art
{
    /// <summary>
    /// Which meshes of an imported character are not its body: spare levels of detail and its own weapons.
    /// </summary>
    /// <remarks>
    /// A model taken from a game ships every level of detail it was drawn at and the weapon it carried there. Drawn
    /// together the levels doubled the figure, and a built-in weapon would hang beside the game's own, which carries
    /// the strike volume. Matched by name, because that is all such a model says about its parts.
    /// </remarks>
    public static class SpareMeshes
    {
        /// <summary>Whether a mesh is a spare level of detail or a weapon rather than the body.</summary>
        /// <param name="name">The mesh object's name.</param>
        /// <returns>True for a mesh to remove.</returns>
        public static bool IsSpare(string name) =>
            Contains(name, "LOD1") || Contains(name, "LOD2") || Contains(name, "Lowmesh") || Contains(name, "Weapon");

        private static bool Contains(string name, string part) =>
            name.IndexOf(part, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
