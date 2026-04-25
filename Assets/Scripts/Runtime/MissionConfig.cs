using UnityEngine;

namespace BreachScenarioEngine.Runtime
{
    [CreateAssetMenu(fileName = "MissionConfig", menuName = "Breach Scenario Engine/Mission Config")]
    public sealed class MissionConfig : ScriptableObject
    {
        [SerializeField] private string missionId = "";
        [SerializeField] private string missionTitle = "";
        [SerializeField] private string schemaVersion = "tb.mission_template.v2.2";
        [SerializeField] private int initialSeed;
        [SerializeField] private int maxRetries;
        [SerializeField] private string templatePath = "";
        [SerializeField] private string payloadPath = "";
        [SerializeField] private string layoutPath = "";
        [SerializeField] private string entitiesPath = "";
        [SerializeField] private string verificationSummaryPath = "";
        [SerializeField] private string generationManifestPath = "";

        public string MissionId => missionId;
        public string MissionTitle => missionTitle;
        public string SchemaVersion => schemaVersion;
        public int InitialSeed => initialSeed;
        public int MaxRetries => maxRetries;
        public string TemplatePath => templatePath;
        public string PayloadPath => payloadPath;
        public string LayoutPath => layoutPath;
        public string EntitiesPath => entitiesPath;
        public string VerificationSummaryPath => verificationSummaryPath;
        public string GenerationManifestPath => generationManifestPath;
    }
}
