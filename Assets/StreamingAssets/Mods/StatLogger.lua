-- Example Groundwork mod: StatLogger
-- This is a Lua 5.2 script loaded by the LuaModSystem.
-- Define callback functions that the simulation calls automatically.

function on_init()
    -- Called once when the mod is first loaded
end

function on_tick()
    -- Called every game tick (24 ticks per day)
end

function on_season_change(season)
    -- season: 0=spring, 1=summer, 2=fall, 3=winter
end

function on_event(event_type, entity_id)
    -- Called for every simulation event emitted by EventDispatchSystem
end
