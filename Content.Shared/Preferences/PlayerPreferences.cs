using Content.Shared._Onyx.Ghost.Skins; // <Onyx-GhostSkins>
using Content.Shared.Construction.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Preferences
{
    /// <summary>
    ///     Contains all player characters and the index of the currently selected character.
    ///     Serialized both over the network and to disk.
    /// </summary>
    [Serializable]
    [NetSerializable]
    public sealed class PlayerPreferences
    {
        private Dictionary<int, HumanoidCharacterProfile> _characters;

        public PlayerPreferences(IEnumerable<KeyValuePair<int, HumanoidCharacterProfile>> characters, int selectedCharacterIndex, Color adminOOCColor, ProtoId<GhostSkinPrototype> ghostSkin, List<ProtoId<ConstructionPrototype>> constructionFavorites) // <Onyx-GhostSkins>
        {
            _characters = new Dictionary<int, HumanoidCharacterProfile>(characters);
            SelectedCharacterIndex = selectedCharacterIndex;
            AdminOOCColor = adminOOCColor;
            GhostSkin = ghostSkin; // <Onyx-GhostSkins>
            ConstructionFavorites = constructionFavorites;
        }

        public PlayerPreferences WithGhostSkin(ProtoId<GhostSkinPrototype> ghostSkin) => // <Onyx-GhostSkins>
            new(_characters, SelectedCharacterIndex, AdminOOCColor, ghostSkin, ConstructionFavorites); // <Onyx-GhostSkins>

        /// <summary>
        ///     All player characters.
        /// </summary>
        public IReadOnlyDictionary<int, HumanoidCharacterProfile> Characters => _characters;

        public HumanoidCharacterProfile GetProfile(int index)
        {
            return _characters[index];
        }

        /// <summary>
        ///     Index of the currently selected character.
        /// </summary>
        public int SelectedCharacterIndex { get; }

        /// <summary>
        ///     The currently selected character.
        /// </summary>
        public HumanoidCharacterProfile SelectedCharacter => Characters[SelectedCharacterIndex];

        public Color AdminOOCColor { get; set; }

        public ProtoId<GhostSkinPrototype> GhostSkin { get; set; } // <Onyx-GhostSkins>

        /// <summary>
        ///    List of favorite items in the construction menu.
        /// </summary>
        public List<ProtoId<ConstructionPrototype>> ConstructionFavorites { get; set; } = [];

        public int IndexOfCharacter(HumanoidCharacterProfile profile)
        {
            return _characters.FirstOrNull(p => p.Value == profile)?.Key ?? -1;
        }

        public bool TryIndexOfCharacter(HumanoidCharacterProfile profile, out int index)
        {
            return (index = IndexOfCharacter(profile)) != -1;
        }
    }
}
