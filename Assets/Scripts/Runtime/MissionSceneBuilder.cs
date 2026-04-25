using System;
using UnityEngine;

namespace BreachScenarioEngine.Runtime
{
    public sealed class MissionSceneBuilder : MonoBehaviour
    {
        public MissionSceneMaterializationReport LastReport { get; private set; }

        public GameObject Build(MissionConfig config, MissionManifest manifest, MissionLayout layout, MissionEntities entities)
        {
            var missionId = config != null ? config.MissionId : "UnknownMission";
            var layoutRevisionId = layout != null ? layout.layoutRevisionId : "";
            var root = new GameObject("GeneratedMissionRoot_" + missionId);
            AddMarker(root, missionId, missionId + ":scene_root", missionId, layoutRevisionId, "scene-root");

            var context = root.AddComponent<MissionSceneContext>();
            LastReport = MissionSceneMaterializer.Materialize(context, config, manifest, layout, entities);
            if (LastReport == null || !string.Equals(LastReport.status, "PASS", StringComparison.Ordinal))
            {
                DestroyOwned(root);
                return null;
            }

            root.AddComponent<PilotMissionRuntimeState>().Initialize(
                missionId,
                manifest != null ? manifest.effectiveSeed : 0,
                layoutRevisionId,
                manifest != null ? manifest.status : "",
                layout != null && layout.RoomGraph.rooms != null ? layout.RoomGraph.rooms.Length : 0);

            return root;
        }

        private static void AddMarker(GameObject target, string missionId, string entityId, string sourceId, string layoutRevisionId, string stableKey)
        {
            if (target == null)
            {
                return;
            }

            target.AddComponent<GeneratedOwnershipMarker>().Initialize("bse-pipeline", missionId, entityId, sourceId, layoutRevisionId, stableKey);
        }

        private static void DestroyOwned(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
