using UnityEngine;
using UnityEngine.Tilemaps;

namespace BreachScenarioEngine.Runtime
{
    [DisallowMultipleComponent]
    public sealed class MissionSceneContext : MonoBehaviour
    {
        public Grid Grid;
        public Tilemap BaseMap;
        public Tilemap CollisionMap;
        public Tilemap DecorMap;
        public Tilemap InteractablesMap;
        public Transform DoorsRoot;
        public Transform WindowsRoot;
        public Transform CoversRoot;
        public Transform EnemiesRoot;
        public Transform OperativesRoot;
        public Transform ObjectivesRoot;
        public Transform HostagesRoot;
        public Transform ExtractionRoot;
        public Transform DebugRoot;
    }
}
