# Project_MobileMarket (working title: Project_TruckerMarket)

Respond in Korean.

> Solo project for a game jam/contest entry · End goal: Steam release
> Timeline: 1 month, solo + heavy AI use · Engine: Unity

---

## 1. Overview
- **Genre**: Farm sim + apocalypse + extraction shooter (semi-open world, stage-based)
- **Platform**: PC
- **Logline**: 20 years after civilization collapsed, the land is ruined by contamination and mutants. The player is a settlement outsider who drives a trailer truck into the ruins, scavenges goods, and sells them to survive.
  - ⚠️ Collapse cause is inconsistent ("virus" vs "war") — needs to be unified, but low priority (story detail)
- **Intent**: contest entry, long-term Steam release

## 2. Concept
- **Core fun**: explore ruins (loot) → craft/farm → sell at settlement → grow — repeat this loop to grow yourself and your base
- **Differentiator**: unlike typical farm sims, crops/materials must be scavenged across the map or bought at high prices
- **Target audience**: ages 10–40
- **References**: Stardew Valley, Project Zomboid, Zero Sievert 2

## 3. World & Story
*(TBD — low priority)*
- Setting, storyline, main characters

## 4. Gameplay
- **Core loop**: loot → farm/craft → sell at settlement → earn currency → upgrade character → tackle harder loot zones → get better items
- **Controls**: quarter-view 2.5D, WASD + mouse aim
- **Combat**: ammo is scarce (costly to buy/craft), so combat is a deliberate choice, not a default. On encountering enemies: (1) spend ammo to clear the threat and loot safely, or (2) avoid via stealth to loot without engaging. Fighting clears the area but costs resources; avoiding saves resources but risks detection.
- **Looting**: open containers or kill enemies for items. Items are raw materials (scrap, electronics, copper, etc.) or crops. Zone danger level (low/mid/high) affects item rarity/value.
- **Progression**: use or sell farmed/crafted goods → buy gear from merchants → gear changes character stats.

### Stages
| Zone | Description |
|---|---|
| Trailer Park | Player's home base. Has the truck/trailer. Farming & crafting. |
| Survivor Settlement | Small town of survivors. Sell goods here. |
| Ruined City | Loot zone. Raiders + mutants. Some areas need hazmat gear (radiation). |

- **Win/lose condition**: none. Goal is to loot and extract without dying (extraction-shooter structure).

## 5. Content

- **Scope (confirmed, demo)**: 1 base + 1 settlement + 1–2 loot maps
  - (full release target: 1 base + 3 settlements + 5 loot maps — reference for later expansion)

### Crops (raw)
Potato, corn, tomato, mushroom, rice, wheat, chili, kidney bean

### Loot drops (pre-collapse relics, found while looting)
Old spam can, old tuna can

### Crafting materials (raw → processed)
| Material | Sourced from |
|---|---|
| Steel parts | Scrap, bolts, nuts |
| Steel | Wrenches, spanners, construction beams (needs welder) |
| Gunpowder | Fireworks, industrial explosive crates |
| Electronics | Old phones, PC parts |
| Copper | Wire bundles, electric motors |
| Rubber | Any tires |
| Fabric | Clothes |
| Adhesive | Industrial glue |
| Fuel (oil) | Jerry cans |

### Finished goods (recipes)

**Electronics**
- Steel parts + copper + electronics → Radio
- Steel parts + copper + electronics + fabric → Walkie-talkie
- Electronics + copper + rubber → Portable charger
- Electronics + copper + steel parts → Electronic watch
- Electronics + copper + fabric → Radio antenna

**Canning station**
- Canning station + wheat → Canned bread
- Canning station + potato + old spam → Spam & potato can
- Canning station + mushroom + rice → Mushroom fried rice can
- Canning station + potato + kidney bean → Veggie mix can
- Canning station + corn + kidney bean → Corn soup can
- Canning station + tomato + kidney bean + chili → Chili can
- Canning station + tomato + corn + kidney bean → Vegetable stew can
- Canning station + potato + corn + old spam → Army stew can
- Canning station + mushroom + potato + old spam → Mushroom potato stew can
- Canning station + chili + old tuna → Chili tuna can

### Mutants / NPCs
| Type | Attack | Notes |
|---|---|---|
| Raider (melee) | Melee | - |
| Raider (ranged) | Pistol | - |
| Mutant A | Melee (assumed) | Average speed |
| Mutant B | Melee (assumed) | Slow, high HP (tank) |
- ⚠️ Need to confirm Mutant A/B are melee-only (ranged would spike difficulty) — low priority
- Recommended AI: 2 base types — melee (raider-melee/Mutant A/B, differ by speed/HP/damage) + ranged (raider-ranged, separate aim/ballistics logic)

### Difficulty curve (full release; demo needs a scaled-down version)
- 3 settlements each buy different goods at different prices → selling in the right place matters
- Loot zones: 2 low-risk / 2 mid-risk / 1 high-risk, player picks difficulty
- ⚠️ Demo (1 settlement, 1–2 loot maps) needs its own scaled-down version of this — TBD

## 6. UI/UX
*(TBD)*
- Screen flow (main / in-game / results)
- Control flow

## 7. Art & Sound
- **Art style**: 2.5D or 3D (undecided)
- **Sound/BGM direction**: TBD

## 8. Tech Plan
- **Engine/tools**: Unity
- **Timeline**: 1 month, solo + heavy AI use

---

## Open Items
- World/story/characters
- UI/UX flow (especially the trading screen)
- Sound direction

## Resolved
1. Item/material list — confirmed
2. Combat/growth/looting systems — confirmed (ammo-scarcity combat vs. stealth choice)
3. Stage scope — confirmed (demo: 1 base + 1 settlement + 1–2 loot maps)
4. Mutant/NPC list — confirmed (4 types: raider melee/ranged, Mutant A/B); melee-only confirmation still pending, low priority

## Deferred (low priority, story detail)
- Trading UI flow
- Collapse cause (virus vs. war)
