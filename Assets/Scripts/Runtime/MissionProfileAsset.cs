using UnityEngine;

namespace BreachScenarioEngine.Runtime
{
    [CreateAssetMenu(fileName = "MissionProfile", menuName = "Breach Scenario Engine/Mission Profile")]
    public sealed class MissionProfileAsset : ScriptableObject
    {
        [SerializeField] private string profileId = "";
        [SerializeField] private string profileType = "";
        [SerializeField] private string schemaVersion = "bse.profile.v2.2";

        public string ProfileId => profileId;
        public string ProfileType => profileType;
        public string SchemaVersion => schemaVersion;
    }
}
