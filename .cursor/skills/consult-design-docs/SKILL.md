---
name: consult-design-docs
description: Consult design documentation in dict/ before implementing features or making changes. Use when implementing new features, modifying existing code, or when the user asks about architecture or design patterns, or when debugging errors. Always check relevant dict/ files first to ensure consistency with the established design.
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

## Error Troubleshooting Workflow

When user reports an issue or error:

### Step 1: Check Mod Logs

**Log Location**: `D:\steam\steamapps\common\Slay the Spire 2\mods\STS2Agent\logs\`

Available log files:
- `debug.log` - General debugging information
- `card_reward.log` - Card reward specific logs
- Other `[feature].log` files for specific modules

**Before providing any solution**, MUST check relevant logs to understand the actual error.

### Step 2: Check Project README

Read `README.md` for:
- Project structure overview
- Build and deployment instructions
- API endpoints and usage
- Environment configuration

### Step 3: Analyze and Diagnose

Based on log contents:
1. Identify the error type and location
2. Cross-reference with `dict/model/` documentation
3. Check if the implementation matches documented design
4. Determine root cause

### Step 4: Provide Solution

If fix is possible:
- Explain what went wrong
- Provide corrected code based on design documentation
- Reference relevant `dict/` files

If root cause is unclear:
- Follow the **User Information Collection Format** below
- Ask user to provide missing information

## User Information Collection Format

When diagnostic information is insufficient, ask user to provide:

```
## Required Information

### 1. Problem Description
[What were you trying to do?]

### 2. Expected Behavior
[What should have happened?]

### 3. Actual Behavior
[What actually happened?]

### 4. Log Files
Please share the content of relevant log files from:
`D:\steam\steamapps\common\Slay the Spire 2\mods\STS2Agent\logs\`
- `debug.log`
- [Other relevant log files]

### 5. Environment
- Game version: [Check in-game]
- Mod version: [Check mod listing]
- Steps to reproduce: [How can I replicate this issue?]

### 6. Recent Changes
[Any recent code changes or configuration updates?]
```

**IMPORTANT**: Do NOT make up or guess any of the above information. Only provide information that can be verified from logs, documentation, or user input.

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

### Error Diagnosis Rules

- **MUST** check mod logs before providing any solution
- **MUST** reference design documentation when explaining errors
- **MUST** use the standard User Information Collection Format when logs are insufficient
- **DO NOT** guess error causes without log evidence
- **DO NOT** provide solutions based on assumptions - verify with logs first

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

# View project README
cat README.md

# View mod logs (Windows path)
# D:\steam\steamapps\common\Slay the Spire 2\mods\STS2Agent\logs\
```

## Example Scenarios

**Scenario 1: Adding new card**
-> Consult `dict/model/card.md` for `CardModel` structure -> Consult `dict/model/flow.md` for card play flow -> Review existing card implementations -> Implement

**Scenario 2: Modifying combat logic**
-> Consult `dict/model/combat.md` -> Consult `dict/model/flow.md` combat flow -> Consult `dict/service/hook.md` for appropriate hook -> Implement

**Scenario 3: Adding new relic**
-> Consult `dict/model/relic.md` -> Review existing relic implementations -> Note `RelicOn` queue timing -> Implement

**Scenario 4: Multiplayer feature**
-> Consult `dict/model/multiplayer.md` -> Understand action queue sync -> Understand state sync mechanism -> Implement

**Scenario 5: Debugging an error**
-> Check `D:\steam\steamapps\common\Slay the Spire 2\mods\STS2Agent\logs\debug.log` -> Read `README.md` -> Cross-reference with `dict/` docs -> If insufficient, ask user for information using standard format -> Provide solution based on verified evidence