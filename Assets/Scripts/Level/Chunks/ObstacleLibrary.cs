using System.Collections.Generic;
using UnityEngine;

namespace Jumpline.Level.Chunks
{
    /// <summary>Tüm engel tanımlarının listesi (chunk'lar engellere kimlikle başvurur).</summary>
    [CreateAssetMenu(fileName = "ObstacleLibrary", menuName = "Jumpline/Obstacle Library")]
    public class ObstacleLibrary : ScriptableObject
    {
        [SerializeField] private List<ObstacleDefinition> definitions = new List<ObstacleDefinition>();

        private Dictionary<string, ObstacleDefinition> byId;

        public IReadOnlyList<ObstacleDefinition> Definitions => definitions;

        public void SetDefinitions(List<ObstacleDefinition> defs)
        {
            definitions = defs;
            byId = null;
        }

        public ObstacleDefinition Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (byId == null)
            {
                byId = new Dictionary<string, ObstacleDefinition>();
                foreach (var d in definitions) if (d != null && !string.IsNullOrEmpty(d.id)) byId[d.id] = d;
            }
            return byId.TryGetValue(id, out var def) ? def : null;
        }

        private void OnValidate() => byId = null;
    }
}
