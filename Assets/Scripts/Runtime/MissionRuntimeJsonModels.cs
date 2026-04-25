using System;

namespace BreachScenarioEngine.Runtime
{
    [Serializable]
    public sealed class MissionManifest
    {
        public string schemaVersion = "";
        public string pipelineVersion = "";
        public string missionId = "";
        public string status = "";
        public int requestedSeed;
        public int effectiveSeed;
        public string layoutRevisionId = "";
        public MissionCatalogRefs catalogRefs = new();
    }

    [Serializable]
    public sealed class MissionCatalogRefs
    {
        public string enemyCatalog = "";
        public string environmentCatalog = "";
        public string objectiveCatalog = "";
    }

    [Serializable]
    public sealed class MissionLayout
    {
        public string schemaVersion = "";
        public string pipelineVersion = "";
        public string missionId = "";
        public string layoutRevisionId = "";
        public MissionLayoutGenerator generator = new();
        public MissionRetryPolicy retryPolicy = new();
        public LayoutGraphData LayoutGraph = new();
        public RoomGraphData RoomGraph = new();
        public PortalGraphData PortalGraph = new();
        public CoverGraphData CoverGraph = new();
        public VisibilityGraphData VisibilityGraph = new();
        public HearingGraphData HearingGraph = new();
    }

    [Serializable]
    public sealed class LayoutGraphData
    {
        public string layoutRevisionId = "";
        public int[] bounds = Array.Empty<int>();
        public string theme = "";
        public int ppu;
        public string entryRoomId = "";
        public string[] objectiveRoomIds = Array.Empty<string>();
        public MissionBreachPoint[] breachPoints = Array.Empty<MissionBreachPoint>();
    }

    [Serializable]
    public sealed class MissionLayoutGenerator
    {
        public string id = "";
        public string sourcePayloadPath = "";
        public int step;
        public bool pureData;
    }

    [Serializable]
    public sealed class MissionRetryPolicy
    {
        public int retryFromStep;
        public string retryAction = "";
        public int placementStep;
    }

    [Serializable]
    public sealed class MissionBreachPoint
    {
        public string id = "";
        public string layoutRevisionId = "";
        public string roomId = "";
        public string navNodeId = "";
        public string kind = "";
        public string side = "";
        public float x;
        public float y;
        public float width;
    }

    [Serializable]
    public sealed class RoomGraphData
    {
        public string layoutRevisionId = "";
        public MissionRoom[] rooms = Array.Empty<MissionRoom>();
    }

    [Serializable]
    public sealed class MissionRoom
    {
        public string id = "";
        public string layoutRevisionId = "";
        public string tag = "";
        public MissionRect rect = new();
        public string navNodeId = "";
    }

    [Serializable]
    public sealed class MissionRect
    {
        public float x;
        public float y;
        public float width;
        public float height;
    }

    [Serializable]
    public sealed class PortalGraphData
    {
        public string layoutRevisionId = "";
        public MissionPortal[] portals = Array.Empty<MissionPortal>();
    }

    [Serializable]
    public sealed class MissionPortal
    {
        public string id = "";
        public string layoutRevisionId = "";
        public string fromRoomId = "";
        public string toRoomId = "";
        public string kind = "";
        public string orientation = "";
        public float x;
        public float y;
        public float width;
    }

    [Serializable]
    public sealed class CoverGraphData
    {
        public string layoutRevisionId = "";
        public MissionCoverPoint[] coverPoints = Array.Empty<MissionCoverPoint>();
    }

    [Serializable]
    public sealed class MissionCoverPoint
    {
        public string id = "";
        public string layoutRevisionId = "";
        public string roomId = "";
        public string navNodeId = "";
        public string quality = "";
        public float x;
        public float y;
    }

    [Serializable]
    public sealed class VisibilityGraphData
    {
        public string layoutRevisionId = "";
        public MissionVisibilityEdge[] edges = Array.Empty<MissionVisibilityEdge>();
    }

    [Serializable]
    public sealed class MissionVisibilityEdge
    {
        public string layoutRevisionId = "";
        public string fromRoomId = "";
        public string toRoomId = "";
        public float openness;
    }

    [Serializable]
    public sealed class HearingGraphData
    {
        public string layoutRevisionId = "";
        public float wallMultiplier;
        public float doorPenalty;
        public MissionHearingEdge[] edges = Array.Empty<MissionHearingEdge>();
    }

    [Serializable]
    public sealed class MissionHearingEdge
    {
        public string layoutRevisionId = "";
        public string fromRoomId = "";
        public string toRoomId = "";
        public float attenuation;
    }

    [Serializable]
    public sealed class MissionEntities
    {
        public string schemaVersion = "";
        public string pipelineVersion = "";
        public string missionId = "";
        public string layoutRevisionId = "";
        public int placementStep;
        public int requiresLayoutStep;
        public MissionActorEntity[] actors = Array.Empty<MissionActorEntity>();
        public MissionObjectiveEntity[] objectives = Array.Empty<MissionObjectiveEntity>();
    }

    [Serializable]
    public sealed class MissionActorEntity
    {
        public string entityId = "";
        public string kind = "";
        public string sourceActorId = "";
        public string type = "";
        public string navigationPolicy = "";
        public string placementPolicy = "";
        public string roomId = "";
        public string navNodeId = "";
        public string layoutRevisionId = "";
        public MissionOwnership ownership = new();
    }

    [Serializable]
    public sealed class MissionObjectiveEntity
    {
        public string entityId = "";
        public string kind = "";
        public string objectiveSet = "";
        public string type = "";
        public bool requiresLayoutGraph;
        public string targetRoomTag = "";
        public bool optional;
        public string roomId = "";
        public string navNodeId = "";
        public string layoutRevisionId = "";
        public MissionOwnership ownership = new();
    }

    [Serializable]
    public sealed class MissionOwnership
    {
        public string owner = "";
        public string generatedBy = "";
        public string missionId = "";
        public string sourceId = "";
        public string entityId = "";
        public string layoutRevisionId = "";
        public string stableKey = "";
    }
}
