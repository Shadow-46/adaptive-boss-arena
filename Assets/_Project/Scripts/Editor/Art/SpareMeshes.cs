namespace AdaptiveBossArena.Editor.Art
{
    /// <summary>
    /// Which meshes of an imported character are not its body: spare levels of detail and its own weapons.
    /// </summary>
    /// <remarks>
    /// A model taken from a game ships every level of detail it was drawn at and the weapon it carried there. Drawn
    /// together the levels doubled the figure, so the spares are removed. The weapon is kept apart from the body: it
    /// is drawn in the blade's material and held in the hand, not painted like armour. Matched by name, because that
    /// is all such a model says about its parts.
    /// </remarks>
    public static class SpareMeshes
    {
        /// <summary>Whether a mesh is a spare, lower level of detail, to be removed.</summary>
        /// <param name="name">The mesh object's name.</param>
        /// <returns>True for a mesh to remove.</returns>
        public static bool IsSpare(string name) =>
            Contains(name, "LOD1") || Contains(name, "LOD2") || Contains(name, "Lowmesh");

        /// <summary>Whether a mesh is the model's own weapon rather than its body.</summary>
        /// <param name="name">The mesh object's name.</param>
        /// <returns>True for the weapon.</returns>
        public static bool IsWeapon(string name) => Contains(name, "Weapon");

        /// <summary>Whether a mesh is the body itself: neither a spare nor the weapon.</summary>
        /// <param name="name">The mesh object's name.</param>
        /// <returns>True for the body.</returns>
        public static bool IsBody(string name) => !IsSpare(name) && !IsWeapon(name);

        private static bool Contains(string name, string part) =>
            name.IndexOf(part, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
