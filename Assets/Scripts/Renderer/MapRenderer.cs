using UnityEngine;
using UnityEngine.UI;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Groundwork.Simulation;
using System.Collections.Generic;

namespace Groundwork.Renderer
{
    /// <summary>
    /// Proper renderer: 3D building meshes (cube walls + roof), citizen sprites
    /// with direction indicators, click-to-select buildings with info panel.
    /// Architecture: stateless renderer reads ECS world each frame.
    /// </summary>
    public class MapRenderer : MonoBehaviour
    {
        [Header("Grid")]
        public float tileSize = 1f;
        public Color groundColor = new Color(0.35f, 0.40f, 0.22f); // lighter olive for contrast
        public Color gridLineColor = new Color(0.10f, 0.08f, 0.03f);

        [Header("Buildings")]
        public float buildingFootprintScale = 0.9f;
        public float wallHeight = 1.5f;     // was 0.4 — increased for top-down visibility
        public float roofHeight = 0.5f;     // was 0.15
        public float roofOverhang = 0.15f;

        [Header("Citizens")]
        public float citizenScale = 0.25f;
        public Color citizenColor = new Color(0.2f, 0.5f, 0.9f);
        public Color haulerColor = new Color(0.9f, 0.7f, 0.1f);

        [Header("Selection")]
        public Color selectedTint = new Color(0.3f, 0.6f, 1f);
        public Color hoverTint = new Color(0.5f, 0.5f, 0.5f);

        // Building type → visual config
        private struct BuildingVisualConfig
        {
            public Color WallColor;
            public Color RoofColor;
            public bool HasPeakedRoof;
        }

        private static readonly Dictionary<string, BuildingVisualConfig> BuildingConfigs = new()
        {
            ["house"] = new() { WallColor = new Color(0.85f, 0.70f, 0.45f), RoofColor = new Color(0.65f, 0.30f, 0.15f), HasPeakedRoof = true },
            ["gatherer_hut"] = new() { WallColor = new Color(0.55f, 0.80f, 0.40f), RoofColor = new Color(0.35f, 0.55f, 0.25f), HasPeakedRoof = false },
            ["forester_hut"] = new() { WallColor = new Color(0.35f, 0.70f, 0.30f), RoofColor = new Color(0.18f, 0.45f, 0.15f), HasPeakedRoof = false },
            ["woodcutter"] = new() { WallColor = new Color(0.75f, 0.50f, 0.30f), RoofColor = new Color(0.55f, 0.30f, 0.15f), HasPeakedRoof = true },
        };

        private GameLoop _gameLoop;
        private bool _initialized;

        // Ground
        private GameObject _groundObject;
        private Material _groundMat;

        // Entity queries
        private EntityQuery _buildingQuery;
        private EntityQuery _citizenQuery;

        // Visual containers
        private GameObject _buildingContainer;
        private GameObject _citizenContainer;
        private List<BuildingVisual> _buildingVisuals = new();
        private List<CitizenVisual> _citizenVisuals = new();

        // Selection
        private Entity _selectedBuilding = Entity.Null;
        private BuildingVisual _selectedVisual;
        private Camera _mainCamera;

        // Info panel
        private Canvas _infoCanvas;
        private GameObject _infoPanel;
        private Text _infoText;

        // Map dimensions
        private int _mapWidth = 100;
        private int _mapHeight = 100;

        // Mesh templates
        private Mesh _cubeMesh;
        private Mesh _peakRoofMesh;
        private Mesh _flatRoofMesh;
        private Mesh _citizenMesh;

        public class BuildingVisual
        {
            public GameObject Root;
            public MeshRenderer WallRenderer;
            public MeshRenderer RoofRenderer;
            public Entity BuildingEntity;
            public int2 TilePos;
        }

        public class CitizenVisual
        {
            public GameObject Root;
            public MeshRenderer Renderer;
            public int2 LastPos;
        }

        void Start()
        {
            _gameLoop = GetComponent<GameLoop>();
            _mainCamera = Camera.main;
            CreateMeshTemplates();
            CreateGround();
            CreateContainers();
            CreateInfoPanel();
        }

        void LateUpdate()
        {
            if (_gameLoop?.World == null || !_gameLoop.World.IsCreated)
                return;

            if (!_initialized)
            {
                InitializeEntityQueries();
                ReadMapDimensions();
                CreateGround(); // Recreate with correct dimensions
                _initialized = true;
            }

            HandleInput();
            UpdateBuildingVisuals();
            UpdateCitizenVisuals();
            UpdateInfoPanel();
        }

        void OnDestroy()
        {
            if (_gameLoop?.World != null && _gameLoop.World.IsCreated)
            {
                _buildingQuery.Dispose();
                _citizenQuery.Dispose();
            }
            CleanupMeshes();
        }

        // ═══════════════════════════════════════════
        //  Mesh Templates
        // ═══════════════════════════════════════════

        private void CreateMeshTemplates()
        {
            _cubeMesh = CreateCubeMesh();
            _peakRoofMesh = CreatePeakRoofMesh();
            _flatRoofMesh = CreateFlatRoofMesh();
            _citizenMesh = CreateCitizenMesh();
        }

        private void CleanupMeshes()
        {
            if (_cubeMesh != null) Destroy(_cubeMesh);
            if (_peakRoofMesh != null) Destroy(_peakRoofMesh);
            if (_flatRoofMesh != null) Destroy(_flatRoofMesh);
            if (_citizenMesh != null) Destroy(_citizenMesh);
        }

        private static Mesh CreateCubeMesh()
        {
            return CreatePrimitiveMesh(PrimitiveType.Cube);
        }

        private static Mesh CreatePrimitiveMesh(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            var copy = Instantiate(mesh);
            copy.name = type.ToString();
            Destroy(go);
            return copy;
        }

        private Mesh CreatePeakRoofMesh()
        {
            // Triangle prism shape — like a gable roof
            var mesh = new Mesh { name = "PeakRoof" };
            float hw = 0.5f + roofOverhang * 0.3f;
            float hd = 0.5f + roofOverhang;
            float hh = roofHeight * 0.5f;
            Vector3[] verts = new Vector3[]
            {
                new(-hw, 0, -hd), new(hw, 0, -hd), new(0, hh, 0), // front triangle
                new(-hw, 0,  hd), new(hw, 0,  hd), new(0, hh, 0), // back triangle
            };
            int[] tris = new int[]
            {
                0,1,2, 3,5,4, // triangles
                0,3,1, 1,3,4, // sides
            };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private Mesh CreateFlatRoofMesh()
        {
            // Flat slab slightly wider than the building
            var mesh = new Mesh { name = "FlatRoof" };
            float hw = 0.5f + roofOverhang * 0.3f;
            float hd = 0.5f + roofOverhang;
            float h = roofHeight * 0.4f;
            Vector3[] verts = new Vector3[]
            {
                new(-hw, 0, -hd), new(hw, 0, -hd), new(-hw, 0, hd), new(hw, 0, hd),
                new(-hw, h, -hd), new(hw, h, -hd), new(-hw, h, hd), new(hw, h, hd),
            };
            int[] tris = new int[]
            {
                0,2,1, 1,2,3, // bottom
                4,5,6, 5,7,6, // top
                0,1,4, 1,5,4, // front
                2,6,3, 3,6,7, // back
                0,4,2, 2,4,6, // left
                1,3,5, 3,7,5, // right
            };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private Mesh CreateCitizenMesh()
        {
            // Small upwards-pointing triangle (arrow shape) to show direction
            // Actually a small cylinder is more visible
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            var copy = Instantiate(mesh);
            copy.name = "CitizenCylinder";
            Destroy(go);
            return copy;
        }

        // ═══════════════════════════════════════════
        //  Ground + Containers
        // ═══════════════════════════════════════════

        private void CreateGround()
        {
            if (_groundObject != null)
                Destroy(_groundObject);

            var shader = Shader.Find("Groundwork/UnlitColor");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            _groundMat = new Material(shader) { color = groundColor };

            _groundObject = new GameObject("Ground");
            _groundObject.transform.SetParent(transform);
            var mf = _groundObject.AddComponent<MeshFilter>();
            var mr = _groundObject.AddComponent<MeshRenderer>();
            mf.mesh = CreateGroundMesh();
            mr.material = _groundMat;
        }

        private Mesh CreateGroundMesh()
        {
            var mesh = new Mesh { name = "Ground" };
            float w = _mapWidth * tileSize;
            float h = _mapHeight * tileSize;
            mesh.vertices = new[] { new Vector3(0,0,0), new Vector3(w,0,0), new Vector3(0,0,h), new Vector3(w,0,h) };
            mesh.triangles = new[] { 0,2,1, 2,3,1 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void CreateContainers()
        {
            _buildingContainer = new GameObject("Buildings");
            _buildingContainer.transform.SetParent(transform);
            _citizenContainer = new GameObject("Citizens");
            _citizenContainer.transform.SetParent(transform);
        }

        // ═══════════════════════════════════════════
        //  Info Panel (click-to-select)
        // ═══════════════════════════════════════════

        private void CreateInfoPanel()
        {
            // Find or create canvas for overlay
            var existingCanvas = FindAnyObjectByType<Canvas>();
            if (existingCanvas != null)
            {
                _infoCanvas = existingCanvas;
            }
            else
            {
                var canvasGo = new GameObject("InfoCanvas");
                _infoCanvas = canvasGo.AddComponent<Canvas>();
                _infoCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGo.AddComponent<CanvasScaler>();
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            _infoPanel = new GameObject("InfoPanel");
            _infoPanel.transform.SetParent(_infoCanvas.transform, false);

            var bg = _infoPanel.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.75f);

            var rt = _infoPanel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 0.5f);
            rt.anchorMax = new Vector2(1, 0.5f);
            rt.pivot = new Vector2(1, 0.5f);
            rt.anchoredPosition = new Vector2(-16, 0);
            rt.sizeDelta = new Vector2(260, 180);

            var textGo = new GameObject("InfoText");
            textGo.transform.SetParent(_infoPanel.transform, false);
            _infoText = textGo.AddComponent<Text>();
            _infoText.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
            _infoText.fontSize = 14;
            _infoText.color = Color.white;
            var trt = _infoText.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(8, 8);
            trt.offsetMax = new Vector2(-8, -8);

            _infoPanel.SetActive(false);
        }

        // ═══════════════════════════════════════════
        //  Input Handling
        // ═══════════════════════════════════════════

        private void HandleInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out var hit, 200f))
                {
                    var wrapper = hit.collider.GetComponentInParent<BuildingVisualWrapper>();
                    if (wrapper != null && wrapper.Visual != null)
                    {
                        SelectBuilding(wrapper.Visual.BuildingEntity);
                    }
                    else
                    {
                        DeselectBuilding();
                    }
                }
                else
                {
                    DeselectBuilding();
                }
            }
        }

        private void SelectBuilding(Entity entity)
        {
            // Deselect previous
            if (_selectedVisual != null)
            {
                _selectedVisual.WallRenderer.material.color = GetBuildingConfig(_selectedVisual.BuildingEntity).WallColor;
            }

            _selectedBuilding = entity;
            _selectedVisual = FindBuildingVisual(entity);
            if (_selectedVisual != null)
            {
                _selectedVisual.WallRenderer.material.color = selectedTint;
                _infoPanel.SetActive(true);
            }
        }

        private void DeselectBuilding()
        {
            if (_selectedVisual != null)
            {
                _selectedVisual.WallRenderer.material.color = GetBuildingConfig(_selectedVisual.BuildingEntity).WallColor;
            }
            _selectedBuilding = Entity.Null;
            _selectedVisual = null;
            _infoPanel.SetActive(false);
        }

        private BuildingVisual FindBuildingVisual(Entity entity)
        {
            foreach (var v in _buildingVisuals)
                if (v.BuildingEntity == entity)
                    return v;
            return null;
        }

        private void UpdateInfoPanel()
        {
            if (!_infoPanel.activeSelf || _selectedBuilding == Entity.Null || _gameLoop?.World == null)
                return;

            var em = _gameLoop.World.EntityManager;
            if (!em.Exists(_selectedBuilding))
            {
                DeselectBuilding();
                return;
            }

            var bldg = em.GetComponentData<Building>(_selectedBuilding);
            var outputInv = em.GetBuffer<OutputSlot>(_selectedBuilding);
            string invStr = "";
            for (int i = 0; i < outputInv.Length; i++)
                invStr += $"{outputInv[i].ItemId}: {outputInv[i].Quantity}\n";

            int workers = 0;
            if (bldg.MaxWorkers > 0)
            {
                var citizenQuery = em.CreateEntityQuery(typeof(Citizen));
                var citizens = citizenQuery.ToComponentDataArray<Citizen>(Allocator.Temp);
                for (int i = 0; i < citizens.Length; i++)
                    if (citizens[i].WorkplaceBuilding == _selectedBuilding)
                        workers++;
                citizens.Dispose();
                citizenQuery.Dispose();
            }

            var pos = em.GetComponentData<MapPosition>(_selectedBuilding);
            _infoText.text = $"<b>{bldg.BuildingType}</b>\n" +
                $"[{pos.TileCoordinate.x}, {pos.TileCoordinate.y}]\n\n" +
                $"Workers: {workers}/{bldg.MaxWorkers}\n" +
                $"Size: {bldg.FootprintSize}x{bldg.FootprintSize}\n\n" +
                $"Inventory:\n{invStr}";
        }

        // ═══════════════════════════════════════════
        //  Entity Queries
        // ═══════════════════════════════════════════

        private void InitializeEntityQueries()
        {
            var em = _gameLoop.World.EntityManager;
            _buildingQuery = em.CreateEntityQuery(typeof(Building), typeof(MapPosition));
            _citizenQuery = em.CreateEntityQuery(typeof(Citizen), typeof(MapPosition));
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
            catch { }
            finally { mapQuery.Dispose(); }
        }

        // ═══════════════════════════════════════════
        //  Building Visuals
        // ═══════════════════════════════════════════

        private static BuildingVisualConfig GetBuildingConfig(Entity entity)
        {
            // This is accessed from BuildingVisual via the entity reference;
            // we need the BuildingType but don't have it here.
            // The config is set at creation time — just return default.
            return new BuildingVisualConfig { WallColor = Color.gray, RoofColor = Color.gray };
        }

        private BuildingVisualConfig GetBuildingConfig(string buildingType)
        {
            var typeStr = buildingType.ToString();
            if (BuildingConfigs.TryGetValue(typeStr, out var cfg))
                return cfg;
            return new BuildingVisualConfig { WallColor = Color.gray, RoofColor = Color.gray };
        }

        private void UpdateBuildingVisuals()
        {
            if (!_initialized) return;

            var buildings = _buildingQuery.ToComponentDataArray<Building>(Allocator.Temp);
            var entities = _buildingQuery.ToEntityArray(Allocator.Temp);
            var positions = _buildingQuery.ToComponentDataArray<MapPosition>(Allocator.Temp);

            // Grow or shrink visual pool
            while (_buildingVisuals.Count < buildings.Length)
                _buildingVisuals.Add(CreateBuildingVisual());
            while (_buildingVisuals.Count > buildings.Length)
            {
                var last = _buildingVisuals[_buildingVisuals.Count - 1];
                if (last == _selectedVisual) _selectedVisual = null;
                Destroy(last.Root);
                _buildingVisuals.RemoveAt(_buildingVisuals.Count - 1);
            }

            for (int i = 0; i < buildings.Length; i++)
            {
                var visual = _buildingVisuals[i];
                visual.Root.SetActive(true);
                visual.BuildingEntity = entities[i];

                var pos = positions[i].TileCoordinate;
                visual.TilePos = pos;
                var footSize = buildings[i].FootprintSize > 0 ? buildings[i].FootprintSize : (byte)1;
                float size = buildingFootprintScale * footSize;
                float cx = pos.x * tileSize + tileSize * footSize * 0.5f;
                float cz = pos.y * tileSize + tileSize * footSize * 0.5f;

                visual.Root.transform.position = new Vector3(cx, 0, cz);
                visual.Root.transform.localScale = new Vector3(size, 1f, size);

                // Set wall height by adjusting the child wall object
                var wallTransform = visual.WallRenderer.transform;
                wallTransform.localScale = new Vector3(1f, wallHeight, 1f);
                wallTransform.localPosition = new Vector3(0, wallHeight * 0.5f, 0);

                var roofTransform = visual.RoofRenderer.transform;
                roofTransform.localPosition = new Vector3(0, wallHeight, 0);

                var cfg = GetBuildingConfig(buildings[i].BuildingType.ToString());
                visual.WallRenderer.material.color = entities[i] == _selectedBuilding ? selectedTint : cfg.WallColor;
                visual.RoofRenderer.material.color = cfg.RoofColor;
            }

            buildings.Dispose();
            entities.Dispose();
            positions.Dispose();
        }

        private BuildingVisual CreateBuildingVisual()
        {
            var root = new GameObject("Building");
            root.transform.SetParent(_buildingContainer.transform);

            // Add a collider for click detection
            var boxCollider = root.AddComponent<BoxCollider>();
            boxCollider.size = new Vector3(1, wallHeight + roofHeight, 1);
            boxCollider.center = new Vector3(0, (wallHeight + roofHeight) * 0.5f, 0);

            // Wall (cube)
            var wallGo = new GameObject("Wall");
            wallGo.transform.SetParent(root.transform);
            var wallMf = wallGo.AddComponent<MeshFilter>();
            wallMf.sharedMesh = _cubeMesh;
            var wallMr = wallGo.AddComponent<MeshRenderer>();
            wallMr.material = new Material(_groundMat);

            // Roof
            var roofGo = new GameObject("Roof");
            roofGo.transform.SetParent(root.transform);
            var roofMf = roofGo.AddComponent<MeshFilter>();
            roofMf.sharedMesh = _flatRoofMesh; // default
            var roofMr = roofGo.AddComponent<MeshRenderer>();
            roofMr.material = new Material(_groundMat);

            // Attach a wrapper MonoBehaviour for raycast lookup
            var wrapper = root.AddComponent<BuildingVisualWrapper>();
            var visual = new BuildingVisual
            {
                Root = root,
                WallRenderer = wallMr,
                RoofRenderer = roofMr,
            };
            wrapper.Visual = visual;

            return visual;
        }

        // ═══════════════════════════════════════════
        //  Citizen Visuals
        // ═══════════════════════════════════════════

        private void UpdateCitizenVisuals()
        {
            if (!_initialized) return;

            var citizens = _citizenQuery.ToComponentDataArray<Citizen>(Allocator.Temp);
            var positions = _citizenQuery.ToComponentDataArray<MapPosition>(Allocator.Temp);

            while (_citizenVisuals.Count < citizens.Length)
                _citizenVisuals.Add(CreateCitizenVisual());
            while (_citizenVisuals.Count > citizens.Length)
            {
                var last = _citizenVisuals[_citizenVisuals.Count - 1];
                Destroy(last.Root);
                _citizenVisuals.RemoveAt(_citizenVisuals.Count - 1);
            }

            for (int i = 0; i < citizens.Length; i++)
            {
                var visual = _citizenVisuals[i];
                visual.Root.SetActive(true);

                var pos = positions[i].TileCoordinate;
                float cx = pos.x * tileSize + tileSize * 0.5f;
                float cz = pos.y * tileSize + tileSize * 0.5f;

                // Compute direction from last position
                float angle = 0f;
                if (visual.LastPos.x != pos.x || visual.LastPos.y != pos.y)
                {
                    int2 dir = pos - visual.LastPos;
                    angle = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                }
                visual.LastPos = pos;

                visual.Root.transform.position = new Vector3(cx, wallHeight * 0.1f, cz);
                visual.Root.transform.localScale = new Vector3(citizenScale, citizenScale * 0.3f, citizenScale);
                visual.Root.transform.rotation = Quaternion.Euler(0, angle, 0);

                bool isHauler = citizens[i].WorkplaceBuilding == Entity.Null;
                visual.Renderer.material.color = isHauler ? haulerColor : citizenColor;
            }

            citizens.Dispose();
            positions.Dispose();
        }

        private CitizenVisual CreateCitizenVisual()
        {
            var root = new GameObject("Citizen");
            root.transform.SetParent(_citizenContainer.transform);
            var mf = root.AddComponent<MeshFilter>();
            mf.sharedMesh = _citizenMesh;
            var mr = root.AddComponent<MeshRenderer>();
            mr.material = new Material(_groundMat);
            var visual = new CitizenVisual
            {
                Root = root,
                Renderer = mr,
            };
            return visual;
        }

        // ═══════════════════════════════════════════
        //  Grid Lines (GL)
        // ═══════════════════════════════════════════

        void OnRenderObject()
        {
            if (!_initialized || _groundMat == null) return;
            _groundMat.SetPass(0);
            GL.PushMatrix();
            GL.Begin(GL.LINES);
            GL.Color(gridLineColor);
            float y = 0.005f;
            for (int x = 0; x <= _mapWidth; x++)
            {
                GL.Vertex3(x * tileSize, y, 0);
                GL.Vertex3(x * tileSize, y, _mapHeight * tileSize);
            }
            for (int z = 0; z <= _mapHeight; z++)
            {
                GL.Vertex3(0, y, z * tileSize);
                GL.Vertex3(_mapWidth * tileSize, y, z * tileSize);
            }
            GL.End();
            GL.PopMatrix();
        }
    }

    /// <summary>
    /// MonoBehaviour wrapper so BuildingVisual can be found via GetComponent in raycast.
    /// </summary>
    public class BuildingVisualWrapper : MonoBehaviour
    {
        public MapRenderer.BuildingVisual Visual;
    }
}