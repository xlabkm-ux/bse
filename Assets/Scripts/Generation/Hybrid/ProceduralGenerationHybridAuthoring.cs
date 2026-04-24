using UnityEngine;

#if UNITY_ENTITIES
using Unity.Entities;
using Unity.Mathematics;
#endif

namespace BreachScenarioEngine.Generation.Hybrid
{
    [DisallowMultipleComponent]
    public sealed class ProceduralGenerationHybridAuthoring : MonoBehaviour
    {
        [SerializeField] private int worldWidth = 64;
        [SerializeField] private int worldHeight = 64;
        [SerializeField] private int seed = 428193;
        [SerializeField] private bool useHybridRuntimeBake = true;

        public int WorldWidth => Mathf.Max(1, worldWidth);
        public int WorldHeight => Mathf.Max(1, worldHeight);
        public int Seed => Mathf.Max(0, seed);
        public bool UseHybridRuntimeBake => useHybridRuntimeBake;
    }

#if UNITY_ENTITIES
    public struct BspGenerationConfig : IComponentData
    {
        public int2 WorldSize;
        public int Seed;
        public bool UseHybridRuntimeBake;
    }

    public sealed class ProceduralGenerationHybridBaker : Baker<ProceduralGenerationHybridAuthoring>
    {
        public override void Bake(ProceduralGenerationHybridAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new BspGenerationConfig
            {
                WorldSize = new int2(authoring.WorldWidth, authoring.WorldHeight),
                Seed = authoring.Seed,
                UseHybridRuntimeBake = authoring.UseHybridRuntimeBake
            });
        }
    }
#endif
}
