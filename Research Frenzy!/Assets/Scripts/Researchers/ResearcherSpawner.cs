using System;
using System.Collections.Generic;
using UnityEngine;


namespace Researchers
{
    /// <summary>
    /// Puts hired researchers into the world. A researcher's data exists from the start of the game;
    /// this creates the GameObject only when they're hired (or restored from a save).
    ///
    /// Spawns hired researchers and instantiates them.
    /// This only focuses on creating the objects when hired/loading a save.
    /// </summary>
    public class ResearcherSpawner : MonoBehaviour
    {
        [Tooltip("Prefab with a Researcher component. If empty, a bare GameObject is created (handy for testing).")]
        [SerializeField]
        private Researcher researcherPrefab;
        [Tooltip("One spot per cubicle. Element 0 = cubicle 1, element 1 = cubicle 2, and so on.")] [SerializeField]
        private Transform[] cubicleSpots;
        [SerializeField] private Transform spawnParent;

        // cubicle number -> the researcher standing in it
        private readonly Dictionary<int, Researcher> byCubicle = new Dictionary<int, Researcher>();

        /// <summary>Raised whenever a researcher appears in the world (hired, or restored from a save).</summary>
        public event Action<Researcher> ResearcherSpawned;

        public int CubicleCount => cubicleSpots == null ? 0 : cubicleSpots.Length;

        /// <summary>Cubicles are numbered 1 to CubicleCount (0 means "not placed").</summary>
        public bool IsValidCubicle(int cubicle) => cubicle >= 1 && cubicle <= CubicleCount;

        public bool IsCubicleFree(int cubicle) => IsValidCubicle(cubicle) && !byCubicle.ContainsKey(cubicle);

        //returns researcher at a given cubicle. if no researcher is there, return null
        public Researcher GetAtCubicle(int cubicle)
        {
            Researcher found;
            return byCubicle.TryGetValue(cubicle, out found) ? found : null;
        }

        /// <summary>
        /// Hires a researcher and spawns them at their cubicle.
        /// Null if cubicle is invalid,taken, or if researcher is already hired.
        /// data.Purchase triggers the purchase event, so it is not needed here.
        /// </summary>
        public Researcher Hire(ResearcherData data, int cubicle)
        {
            if (data == null) return null;

            if (data.IsPurchased)
            {
                Debug.LogWarning($"ResearcherSpawner: {data.Name} is already hired (cubicle {data.Position}).");
                return null;
            }

            if (!IsCubicleFree(cubicle))
            {
                Debug.LogWarning($"ResearcherSpawner: cubicle {cubicle} is invalid or already taken.");
                return null;
            }

            data.Purchase(cubicle);
            return Spawn(data);
        }

        /// <summary>
        /// Used after loading in a save. Spawns in hired researchers.
        /// </summary>
        public void SpawnHired(IEnumerable<ResearcherData> all)
        {
            foreach (ResearcherData data in all)
            {
                if (data == null || !data.IsPurchased) continue;

                if (!IsCubicleFree(data.Position))
                {
                    Debug.LogWarning(
                        $"ResearcherSpawner: can't place {data.Name}; cubicle {data.Position} is invalid or taken.");
                    continue;
                }

                Spawn(data);
            }
        }

        /// <summary>Destroys all spawned researcher bodies (their data is untouched).</summary>
        public void Clear()
        {
            foreach (Researcher r in byCubicle.Values)
                if (r != null)
                    Destroy(r.gameObject);
            byCubicle.Clear();
        }

        private Researcher Spawn(ResearcherData data)
        {
            Transform spot = cubicleSpots[data.Position - 1];
            Researcher instance;

            if (researcherPrefab != null)
            {
                instance = Instantiate(researcherPrefab, spot.position, spot.rotation, spawnParent);
            }
            else
            {
                GameObject go = new GameObject(data.Name);
                go.transform.position = spot.position;
                go.transform.rotation = spot.rotation;
                if (spawnParent != null) go.transform.SetParent(spawnParent);
                instance = go.AddComponent<Researcher>();
            }

            instance.Bind(data);
            byCubicle[data.Position] = instance;
            ResearcherSpawned?.Invoke(instance);
            return instance;
        }
    }
}