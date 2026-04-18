---
name: consult-design-docs
description: Consult design documentation in dict/ before implementing features or making changes. Use when implementing new features, modifying existing code, or when the user asks about architecture or design patterns. Always check relevant dict/ files first to ensure consistency with the established design.
---

# Consult Design Documentation

Before implementing any code changes, you MUST consult the design documentation in `dict/` to ensure consistency with project architecture and design.

## Workflow

### Step 1: Identify Relevant Documentation

Based on the task type, consult the corresponding documents:

| Task Type | Documentation | Notes |
|-----------|---------------|-------|
| Card features | `dict/model/card.md` | CardModel, energy costs, piles, runtime reflection |
| Relic features | `dict/model/relic.md` | RelicModel, rarity, status management |
| Potion features | `dict/model/potion.md` | PotionModel, usage logic |
| Powers/buffs | `dict/model/power.md` | Power system, stack types |
| Orb system | `dict/model/orb.md` | OrbModel, queue management |
| Combat logic | `dict/model/combat.md` | CombatState, CombatManager, history |
| Enemies/creatures | `dict/model/creature.md` | Creature entities, damage results, monster models |
| Event system | `dict/model/event.md` | EventModel, option handling |
| Encounters | `dict/model/encounter.md` | EncounterModel, generation logic |
| Run/map | `dict/model/run.md` | RunState, rooms, map |
| Game actions | `dict/model/gameaction.md` | Action system, PlayCardAction, player choices |
| Enums | `dict/model/enum.md` | Key enum reference, Godot enum extraction methods |
| Modding | `dict/model/modding.md` | Mod interfaces, managers, initializers |
| Rewards | `dict/model/reward.md` | Reward base classes, card reward extraction flow |
| Multiplayer | `dict/model/multiplayer.md` | Network sync, action queues, connection management |
| Data flow | `dict/model/flow.md` | Combat flow, reward flow, run flow overview |
| Core tech | `dict/service/tech.md` | Engine, project structure, dependencies, debugging |
| Hook system | `dict/service/hook.md` | Complete hook list and usage timing |
| Card stats plan | `dict/plan/plan_card_stats.md` | Card statistics collection plan |

### Step 2: Read Core Documentation

**MUST READ FIRST**:
1. `dict/model/core.md` - Understand `AbstractModel`, `ModelDb`, `IRunState` core infrastructure
2. `dict/model/flow.md` - Understand data flow through game phases
3. `dict/service/tech.md` - Understand technical constraints and architecture

### Step 3: Review Specific Implementation References

For specific card/relic/monster implementations:
- Review existing code in `mod/Models/` and `mod/Services/`
- Reference corresponding system docs in `dict/model/`
- Pay attention to Godot enum handling (see `dict/model/enum.md`)

### Step 4: Validate Design Consistency

Before implementation, verify:
- [ ] Data models inherit from correct base class (usually `AbstractModel`)
- [ ] Use `ModelDb` for model registration and lookup
- [ ] Align with flow phases described in `flow.md`
- [ ] Insert logic at appropriate hook points (see `dict/service/hook.md`)
- [ ] Handle Godot enum `_value` field extraction (see `dict/model/enum.md` section 1.2)
- [ ] Follow sync protocols for multiplayer features (see `dict/model/multiplayer.md`)

### Step 5: Implement and Update Documentation

After implementation:
- If design is outdated or needs adjustment, update corresponding `dict/` docs
- Reference relevant design docs in commit messages

## Critical Rules

### Mandatory Rules

- **DO NOT** skip design docs and code directly
- **DO NOT** assume existing code matches design - follow docs, fix deviations
- **DO NOT** ignore sync mechanisms in multiplayer features
- **MUST** use `ModelDb.GetId<T>()` for model IDs, never hardcode
- **MUST** properly handle Godot enum `_value` field extraction
- **MUST** find appropriate hook points from hook list, dont modify core flows arbitrarily

### Data Structure Rules

- **DO NOT** guess or invent game data structures that are not documented
- **DO NOT** assume field names, types, or relationships without explicit documentation
- **DO NOT** create speculative models or properties based on inference
- If design documentation is incomplete or missing:
  1. **STOP** and ask the project maintainer/manager for clarification
  2. Wait for explicit guidance before implementing
  3. Document the clarification in `dict/` once confirmed
- **MUST** base all implementations on documented structures in `dict/model/` files
- **MUST** verify existence of fields/properties by checking both docs and existing code

## Quick Reference

```bash
# List all design docs
ls dict/

# View core architecture
cat dict/model/core.md

# View flow overview
cat dict/model/flow.md

# View tech stack
cat dict/service/tech.md

# View specific system (e.g., cards)
cat dict/model/card.md
```

## Example Scenarios

**Scenario 1: Adding new card**
鈫?Consult `dict/model/card.md` for `CardModel` structure 鈫?Consult `dict/model/flow.md` for card play flow 鈫?Review existing card implementations 鈫?Implement

**Scenario 2: Modifying combat logic**
鈫?Consult `dict/model/combat.md` 鈫?Consult `dict/model/flow.md` combat flow 鈫?Consult `dict/service/hook.md` for appropriate hook 鈫?Implement

**Scenario 3: Adding new relic**
鈫?Consult `dict/model/relic.md` 鈫?Review existing relic implementations 鈫?Note `RelicOn` queue timing 鈫?Implement

**Scenario 4: Multiplayer feature**
鈫?Consult `dict/model/multiplayer.md` 鈫?Understand action queue sync 鈫?Understand state sync mechanism 鈫?Implement