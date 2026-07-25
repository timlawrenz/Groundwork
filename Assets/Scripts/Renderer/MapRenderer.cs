using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Groundwork.Simulation;
using System.Collections.Generic;

namespace Groundwork.Renderer
{
    /// <summary>
    /// Renders the simulation state: ground grid, buildings (colored quads),
    /// and citizens (small dots). Manages pools of visual GameObjects for
    /// buildings and citizens, updating their positions each frame from the
    /// ECS simulation world.
    ///
    /// Architecture: stateless renderer — reads simulation state via EntityManager
    /// queries. The simulation never touches rendering code.
    /// </summary>
    public class MapRenderer : MonoBehaviour
    {
        [Header("Grid Settings")]
        public float tileSize = 1f;
        public Color groundColor = new Color(0.28f, 0.22f, 0.15f);
        public Color gridLineColor = new Color(0.15f, 0.11f, 0.06f);

        [Header("Entity Visuals")]
        public float buildingScale = 0.85f;
        public float buildingHeight = 0.02f;
        public float citizenScale = 0.3f;
        public float citizenHeight = 0.05f;
        public Color citizenColor = new Color(0.2f, 0.5f, 0.9f);

        // Building type → color palette
        private static readonly Dictionary<string, Color> BuildingColors = new Dictionary<string, Color>
        {
            { "house",         new Color(0.85f, 0.65f, 0.35f) }, // warm brown/yellow
            { "gatherer_hut",  new Color(0.25f, 0.60f, 0.25f) }, // green
            { "woodcutter",    new Color(0.55f, 0.35f, 0.20f) }, // dark brown
        };

        private GameLoop _gameLoop;
        private bool _initialized;

        // Ground
        private GameObject _groundObject;
        private Material _groundMat;

        // Entity queries (cached)
        private EntityQuery _buildingQuery;
        private EntityQuery _citizenQuery;

        // Visual pools
        private GameObject _buildingContainer;
        private GameObject _citizenContainer;
        private List<GameObject> _buildingVisuals = new List<GameObject>();
        private List<GameObject> _citizenVisuals = new List<GameObject>();
        private Mesh _quadMesh;
        private Material _visualMat;

        // Map dimensions (read from sim)
        private int _mapWidth = 100;
        private int _mapHeight = 100;

        void Start()
        {
            _gameLoop = GetComponent<GameLoop>();
            _quadMesh = CreateQuadMesh();

            // Find an unlit shader — built-in RP uses "Unlit/Color".
            // Fallback chain for different render pipelines.
            var shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Hidden/Internal-Colored");
            _visualMat = new Material(shader);

            _groundMat = new Material(shader);
            _groundMat.color = groundColor;

            _buildingContainer = new GameObject("Buildings");
            _buildingContainer.transform.SetParent(transform);

            _citizenContainer = new GameObject("Citizens");
            _citizenContainer.transform.SetParent(transform);

            // Initial creation of ground (dimensions may be refined after bootstrap)
            CreateGround();
        }

        void LateUpdate()
        {
            if (_gameLoop?.World == null || !_gameLoop.World.IsCreated)
                return;

            if (!_initialized)
            {
                InitializeEntityQueries();
                // Re-read actual map dimensions
                ReadMapDimensions();
                CreateGround(); // Recreate with correct dimensions
                _initialized = true;
            }

            UpdateEntityVisuals();
        }

        void OnDestroy()
        {
            if (_gameLoop?.World != null && _gameLoop.World.IsCreated)
            {
                _buildingQuery.Dispose();
                _citizenQuery.Dispose();
            }
            if (_visualMat != null) Destroy(_visualMat);
            if (_groundMat != null) Destroy(_groundMat);
            if (_quadMesh != null) Destroy(_quadMesh);
        }

        /// <summary>
        /// Draw grid lines via immediate-mode GL. Called by Unity's rendering pipeline.
        /// </summary>
        void OnRenderObject()
        {
            if (!_initialized || _groundMat == null) return;

            _groundMat.SetPass(0);
            GL.PushMatrix();
            GL.Begin(GL.LINES);
            GL.Color(gridLineColor);

            float y = 0.003f; // just above the ground plane

            // Vertical lines (along X)
            for (int x = 0; x <= _mapWidth; x++)
            {
                GL.Vertex3(x * tileSize, y, 0);
                GL.Vertex3(x * tileSize, y, _mapHeight * tileSize);
            }

            // Horizontal lines (along Z)
            for (int z = 0; z <= _mapHeight; z++)
            {
                GL.Vertex3(0, y, z * tileSize);
                GL.Vertex3(_mapWidth * tileSize, y, z * tileSize);
            }

            GL.End();
            GL.PopMatrix();
        }

        // ──────────────────────────────────────────────
        //  Ground mesh
        // ──────────────────────────────────────────────

        private void CreateGround()
        {
            if (_groundObject != null)
                Destroy(_groundObject);

            _groundObject = new GameObject("Ground");
            _groundObject.transform.SetParent(transform);

            var mf = _groundObject.AddComponent<MeshFilter>();
            var mr = _groundObject.AddComponent<MeshRenderer>();

            mf.mesh = CreateGroundMesh();
            mr.material = _groundMat;
        }

        private Mesh CreateGroundMesh()
        {
            var mesh = new Mesh();
            mesh.name = "GroundMesh";

            float w = _mapWidth * tileSize;
            float h = _mapHeight * tileSize;

            Vector3[] vertices = new Vector3[4];
            vertices[0] = new Vector3(0, 0, 0);
            vertices[1] = new Vector3(w, 0, 0);
            vertices[2] = new Vector3(0, 0, h);
            vertices[3] = new Vector3(w, 0, h);

            int[] triangles = new int[] { 0, 2, 1, 2, 3, 1 };

            Vector2[] uv = new Vector2[4];
            uv[0] = new Vector2(0, 0);
            uv[1] = new Vector2(1, 0);
            uv[2] = new Vector2(0, 1);
            uv[3] = new Vector2(1, 1);

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uv;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        private Mesh CreateQuadMesh()
        {
            var mesh = new Mesh();
            mesh.name = "QuadMesh";

            Vector3[] vertices = new Vector3[4];
            vertices[0] = new Vector3(-0.5f, 0, -0.5f);
            vertices[1] = new Vector3( 0.5f, 0, -0.5f);
            vertices[2] = new Vector3(-0.5f, 0,  0.5f);
            vertices[3] = new Vector3( 0.5f, 0,  0.5f);

            // Face up (Y)
            int[] triangles = new int[] { 0, 2, 1, 2, 3, 1 };

            Vector2[] uv = new Vector2[4];
            uv[0] = new Vector2(0, 0);
            uv[1] = new Vector2(1, 0);
            uv[2] = new Vector2(0, 1);
            uv[3] = new Vector2(1, 1);

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uv;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        // ──────────────────────────────────────────────
        //  Entity queries
        // ──────────────────────────────────────────────

        private void InitializeEntityQueries()
        {
            var em = _gameLoop.World.EntityManager;
            _buildingQuery = em.CreateEntityQuery(
                typeof(Building), typeof(MapPosition));
            _citizenQuery = em.CreateEntityQuery(
                typeof(Citizen), typeof(MapPosition));
        }

        private void ReadMapDimensions()
        {
            var mapQuery = _gameLoop.World.EntityManager.CreateEntityQuery(typeof(MapGridData));
            try
            {
                if (!mapQuery.IsEmpty)
                {
                    var mapData = mapQuery.GetSingleton<MapGridData>();
                    if (mapData.Grid.IsCreated)
                    {
                        ref var blob = ref mapData.Grid.Value;
                        _mapWidth = blob.Width;
                        _mapHeight = blob.Height;
                    }
                }
            }
            catch { /* MapGridData may not be created yet */ }
            finally { mapQuery.Dispose(); }
        }

        // ──────────────────────────────────────────────
        //  Entity visuals update
        // ──────────────────────────────────────────────

        private void UpdateEntityVisuals()
        {
            UpdateBuildingVisuals();
            UpdateCitizenVisuals();
        }

        private void UpdateBuildingVisuals()
        {
            if (!_initialized) return;

            var buildings = _buildingQuery.ToComponentDataArray<Building>(Allocator.Temp);
            var positions = _buildingQuery.ToComponentDataArray<MapPosition>(Allocator.Temp);

            EnsureVisualPool(ref _buildingVisuals, buildings.Length, _buildingContainer,
                "Building", buildingScale);

            for (int i = 0; i < buildings.Length; i++)
            {
                var visual = _buildingVisuals[i];
                visual.SetActive(true);

                var pos = positions[i].TileCoordinate;
                visual.transform.position = new Vector3(
                    pos.x * tileSize + tileSize * 0.5f,
                    buildingHeight,
                    pos.y * tileSize + tileSize * 0.5f);

                var renderer = visual.GetComponent<MeshRenderer>();
                renderer.material.color = GetBuildingColor(buildings[i].BuildingType);
            }

            // Hide unused
            for (int i = buildings.Length; i < _buildingVisuals.Count; i++)
                _buildingVisuals[i].SetActive(false);

            buildings.Dispose();
            positions.Dispose();
        }

        private void UpdateCitizenVisuals()
        {
            if (!_initialized) return;

            var citizens = _citizenQuery.ToComponentDataArray<Citizen>(Allocator.Temp);
            var positions = _citizenQuery.ToComponentDataArray<MapPosition>(Allocator.Temp);

            EnsureVisualPool(ref _citizenVisuals, citizens.Length, _citizenContainer,
                "Citizen", citizenScale);

            for (int i = 0; i < citizens.Length; i++)
            {
                var visual = _citizenVisuals[i];
                visual.SetActive(true);

                var pos = positions[i].TileCoordinate;
                visual.transform.position = new Vector3(
                    pos.x * tileSize + tileSize * 0.5f,
                    citizenHeight,
                    pos.y * tileSize + tileSize * 0.5f);

                var renderer = visual.GetComponent<MeshRenderer>();
                renderer.material.color = citizenColor;
            }

            for (int i = citizens.Length; i < _citizenVisuals.Count; i++)
                _citizenVisuals[i].SetActive(false);

            citizens.Dispose();
            positions.Dispose();
        }

        // ──────────────────────────────────────────────
        //  Visual pool management
        // ──────────────────────────────────────────────

        private void EnsureVisualPool(ref List<GameObject> pool, int needed,
            GameObject parent, string prefix, float scale)
        {
            while (pool.Count < needed)
            {
                var go = new GameObject($"{prefix}_{pool.Count}");
                go.transform.SetParent(parent.transform);

                var mf = go.AddComponent<MeshFilter>();
                mf.mesh = _quadMesh;

                var mr = go.AddComponent<MeshRenderer>();
                mr.material = new Material(_visualMat);

                go.transform.localScale = new Vector3(scale, 1f, scale);
                pool.Add(go);
            }
        }

        private static Color GetBuildingColor(FixedString32Bytes buildingType)
        {
            var key = buildingType.ToString();
            if (BuildingColors.TryGetValue(key, out var color))
                return color;
            return Color.gray; // unknown building type
        }
    }
}