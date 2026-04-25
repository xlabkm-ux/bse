using System;
using UnityEngine;

namespace BreachScenarioEngine.Runtime
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class MissionCompleteTrigger : MonoBehaviour
    {
        [SerializeField] private string missionId = "";
        [SerializeField] private string objectiveId = "";
        [SerializeField] private bool completed;

        public bool Completed => completed;
        public string MissionId => missionId;
        public string ObjectiveId => objectiveId;
        public event Action<MissionCompleteTrigger> CompletedChanged;

        public void Initialize(string triggerMissionId, string triggerObjectiveId)
        {
            missionId = triggerMissionId;
            objectiveId = triggerObjectiveId;
        }

        public void Complete()
        {
            if (completed)
            {
                return;
            }

            completed = true;
            CompletedChanged?.Invoke(this);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            Complete();
        }
    }
}
