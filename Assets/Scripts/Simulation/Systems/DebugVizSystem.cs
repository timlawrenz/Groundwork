using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Debug visualization: renders an ASCII grid of the simulation world
    /// driven entirely by the event buffer. Never queries component data.
    /// Buildings: B. Citizens: digit showing count (1-9) or + for 10+.
    /// Runs after Death, before EventDispatch. Renders every TickSampleInterval ticks.
    /// </summary>
    public partial struct DebugVizSystem : ISystem
    {
        private NativeHashMap<int2, char> _buildingTiles;
        private NativeHashMap<int2, int> _citizenCounts;
        private bool _initialized;
        private int _ticksSinceLastRender;

        private const int WINDOW_X = 0;
        private const int WINDOW_Y = 0;
        private const int WINDOW_W = 100;
        private const int WINDOW_H = 30;
        private const int SAMPLE_EVERY = 720; // one frame per season

        public void OnCreate(ref SystemState state)
        {
            _buildingTiles = new NativeHashMap<int2, char>(500, Allocator.Persistent);
            _citizenCounts = new NativeHashMap<int2, int>(2000, Allocator.Persistent);
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_buildingTiles.IsCreated) _buildingTiles.Dispose();
            if (_citizenCounts.IsCreated) _citizenCounts.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<SimulationEventSingleton>(out var eventEntity))
                return;
            var events = state.EntityManager.GetBuffer<SimulationEvent>(eventEntity);

            bool stateChanged = false;

            for (int i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                var pos = new int2((int)evt.Data0, (int)evt.Data1);

                switch (evt.Type)
                {
                    case EventType.BuildingPlaced:
                        _buildingTiles[pos] = 'B';
                        stateChanged = true;
                        break;

                    case EventType.CitizenSpawned:
                    case EventType.TileEnter:
                        if (_citizenCounts.TryGetValue(pos, out int count))
                            _citizenCounts[pos] = count + 1;
                        else
                            _citizenCounts[pos] = 1;
                        stateChanged = true;
                        break;

                    case EventType.TileLeave:
                        if (_citizenCounts.TryGetValue(pos, out int leaveCount) && leaveCount > 0)
                        {
                            if (leaveCount == 1)
                                _citizenCounts.Remove(pos);
                            else
                                _citizenCounts[pos] = leaveCount - 1;
                        }
                        stateChanged = true;
                        break;

                    case EventType.CitizenDied:
                        if (_citizenCounts.TryGetValue(pos, out int dieCount) && dieCount > 0)
                        {
                            if (dieCount == 1)
                                _citizenCounts.Remove(pos);
                            else
                                _citizenCounts[pos] = dieCount - 1;
                        }
                        stateChanged = true;
                        break;

                    default:
                        stateChanged = true;
                        break;
                }
            }

            // Only render at sample intervals (except first frame — always render)
            _ticksSinceLastRender++;

            bool shouldRender = !_initialized;

            if (stateChanged || !_initialized)
            {
                if (!_initialized)
                {
                    shouldRender = true;
                    _initialized = true;
                    _ticksSinceLastRender = 0;
                }
                else if (_ticksSinceLastRender >= SAMPLE_EVERY)
                {
                    shouldRender = true;
                    _ticksSinceLastRender = 0;
                }
            }

            if (shouldRender)
                RenderGrid();
        }

        private void RenderGrid()
        {
            var sb = new System.Text.StringBuilder();
            var border = new string('-', WINDOW_W + 2);
            var topBorder = "+" + border + "+";
            sb.AppendLine(topBorder);
            sb.AppendLine("| Groundwork Debug Viz" + new string(' ', WINDOW_W - 20) + "|");
            sb.AppendLine(topBorder);

            for (int y = WINDOW_Y; y < WINDOW_Y + WINDOW_H; y++)
            {
                sb.Append("| ");
                for (int x = WINDOW_X; x < WINDOW_X + WINDOW_W; x++)
                {
                    var pos = new int2(x, y);
                    _buildingTiles.TryGetValue(pos, out char b);
                    _citizenCounts.TryGetValue(pos, out int cc);
                    if (cc > 0)
                        sb.Append(cc < 10 ? (char)('0' + cc) : '+');
                    else if (b != 0)
                        sb.Append(b);
                    else
                        sb.Append('.');
                }
                sb.AppendLine(" |");
            }

            sb.AppendLine(topBorder);
            sb.AppendLine("  Legend: 1-9=N citizens  +=10+  B=Building  .=Empty");
            sb.AppendLine();

            var output = sb.ToString();
            UnityEngine.Debug.Log(output);

            try
            {
                System.IO.File.AppendAllText("/tmp/groundwork_viz.txt", output);
            }
            catch { /* ignore file write errors */ }
        }
    }
}