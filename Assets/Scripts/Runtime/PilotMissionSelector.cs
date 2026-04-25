using UnityEngine;

namespace BreachScenarioEngine.Runtime
{
    [DisallowMultipleComponent]
    public sealed class PilotMissionSelector : MonoBehaviour
    {
        [SerializeField] private MissionRuntimeLoader loader;
        [SerializeField] private MissionConfig[] missions = new MissionConfig[0];

        private Vector2 scroll;

        private void Awake()
        {
            if (loader == null)
            {
                loader = GetComponent<MissionRuntimeLoader>();
            }
        }

        private void Update()
        {
            if (loader == null || loader.CurrentRoot == null)
            {
                return;
            }

            var triggers = loader.CurrentRoot.GetComponentsInChildren<MissionCompleteTrigger>();
            foreach (var trigger in triggers)
            {
                if (trigger.Completed)
                {
                    var state = loader.CurrentRoot.GetComponent<PilotMissionRuntimeState>();
                    state?.MarkCompleted();
                    break;
                }
            }
        }

        private void OnGUI()
        {
            const int width = 340;
            GUILayout.BeginArea(new Rect(12, 12, width, Screen.height - 24), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("Pilot Missions");

            foreach (var mission in missions)
            {
                if (mission != null && GUILayout.Button(mission.MissionId, GUILayout.Height(28)))
                {
                    loader.LoadMission(mission);
                }
            }

            GUILayout.Space(8);
            if (loader != null)
            {
                GUILayout.Label("Loaded: " + (loader.CurrentMissionId.Length == 0 ? "-" : loader.CurrentMissionId));
                if (!string.IsNullOrEmpty(loader.LastError))
                {
                    GUILayout.Label("Error: " + loader.LastError);
                }

                var state = loader.CurrentRoot != null ? loader.CurrentRoot.GetComponent<PilotMissionRuntimeState>() : null;
                if (state != null)
                {
                    GUILayout.Label("Seed: " + state.EffectiveSeed);
                    GUILayout.Label("Layout: " + state.LayoutRevisionId);
                    GUILayout.Label("Status: " + state.VerificationStatus);
                    GUILayout.Label("Rooms: " + state.RoomCount);
                    GUILayout.Label("Complete: " + state.Completed);
                    if (GUILayout.Button("Complete Objective", GUILayout.Height(28)))
                    {
                        var trigger = loader.CurrentRoot.GetComponentInChildren<MissionCompleteTrigger>();
                        if (trigger != null)
                        {
                            trigger.Complete();
                        }
                        state.MarkCompleted();
                    }
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
