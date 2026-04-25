using System.Collections.Generic;
using UnityEngine;

namespace BreachScenarioEngine.Runtime
{
    [CreateAssetMenu(fileName = "MissionCatalog", menuName = "Breach Scenario Engine/Mission Catalog")]
    public sealed class MissionCatalogAsset : ScriptableObject
    {
        [SerializeField] private string catalogId = "";
        [SerializeField] private string catalogType = "";
        [SerializeField] private string schemaVersion = "bse.catalog.v2.3";
        [SerializeField] private List<string> addressableLabels = new();

        public string CatalogId => catalogId;
        public string CatalogType => catalogType;
        public string SchemaVersion => schemaVersion;
        public IReadOnlyList<string> AddressableLabels => addressableLabels;
    }
}
