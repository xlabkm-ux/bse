using UnityEngine;

namespace BreachScenarioEngine.Runtime
{
    public sealed class PilotMissionRuntimeState : MonoBehaviour
    {
        [SerializeField] private string missionId = "";
        [SerializeField] private int effectiveSeed;
        [SerializeField] private string layoutRevisionId = "";
        [SerializeField] private string verificationStatus = "";
        [SerializeField] private int roomCount;
        [SerializeField] private bool completed;

        public string MissionId => missionId;
        public int EffectiveSeed => effectiveSeed;
        public string LayoutRevisionId => layoutRevisionId;
        public string VerificationStatus => verificationStatus;
        public int RoomCount => roomCount;
        public bool Completed => completed;

        public void Initialize(string stateMissionId, int stateEffectiveSeed, string stateLayoutRevisionId, string stateVerificationStatus, int stateRoomCount)
        {
            missionId = stateMissionId;
            effectiveSeed = stateEffectiveSeed;
            layoutRevisionId = stateLayoutRevisionId;
            verificationStatus = stateVerificationStatus;
            roomCount = stateRoomCount;
        }

        public void MarkCompleted()
        {
            completed = true;
        }
    }
}
