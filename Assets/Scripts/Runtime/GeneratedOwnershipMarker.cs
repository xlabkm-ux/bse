using UnityEngine;

namespace BreachScenarioEngine.Runtime
{
    public sealed class GeneratedOwnershipMarker : MonoBehaviour
    {
        [SerializeField] private string owner = "bse-pipeline";
        [SerializeField] private string missionId = "";
        [SerializeField] private string entityId = "";
        [SerializeField] private string sourceId = "";
        [SerializeField] private string layoutRevisionId = "";
        [SerializeField] private string stableKey = "";

        public string Owner => owner;
        public string MissionId => missionId;
        public string EntityId => entityId;
        public string SourceId => sourceId;
        public string LayoutRevisionId => layoutRevisionId;
        public string StableKey => stableKey;

        public void Initialize(string markerOwner, string markerMissionId, string markerEntityId, string markerSourceId, string markerLayoutRevisionId, string markerStableKey)
        {
            owner = markerOwner;
            missionId = markerMissionId;
            entityId = markerEntityId;
            sourceId = markerSourceId;
            layoutRevisionId = markerLayoutRevisionId;
            stableKey = markerStableKey;
        }
    }
}
