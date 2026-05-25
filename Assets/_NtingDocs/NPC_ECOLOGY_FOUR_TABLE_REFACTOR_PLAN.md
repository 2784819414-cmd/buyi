# NPC Ecology Four Table Refactor Plan

## Goal

NPC routine ecology must be readable from four explicit mod-facing tables:

- `ActionCatalog`: the only source of canonical `ActionId` definitions.
- `ActionTargetRules`: the only source of target, room, facility, distance, and availability matching rules.
- `ActionChains`: ordered action sequences that reference `ActionId` values only.
- `NpcDecisionProfiles`: subjective NPC schedule and preference entries that choose action chains.

The rewrite intentionally drops the old `FacilityGroups`, `ActionSteps`, and `ScheduleTemplates` schema. Missing old fields should fail validation instead of being silently migrated.

## Ownership Boundaries

- `ActionCatalog` owns action meaning and execution mode.
- `ActionTargetRules` owns where an action can happen and what facts must be true before it is offered.
- `ActionChains` owns sequence only. It must not define targets, payloads, or role conditions.
- `NpcDecisionProfiles` owns NPC subjective choice inputs: role, character ids, duties, traits, time windows, intent labels, scores, and selected chain ids.

## Runtime Flow

`CampusConfigDrivenNpcAiController`
calls `CampusNpcEcologyPresetCatalog.TryResolveScheduledIntent`.

The catalog then evaluates:

1. Matching `NpcDecisionProfiles`.
2. Matching decision entries.
3. The current `ActionId` in the selected `ActionChains` record.
4. The `ActionCatalog` record for that `ActionId`.
5. Candidate `ActionTargetRules` for that `ActionId`.
6. `CampusNpcActionOpportunity`.
7. `CampusCharacterActionExecutor`.

Global systems still provide facts, target queries, validation, and execution only. They must not create NPC intent or decide behavior outside the per-NPC controller path.

## Three Stages

1. Rewrite the preset schema and data file to the four-table layout.
2. Rewrite parser, validation, selection, and opportunity building to use the new tables directly.
3. Align interaction-facing action IDs with the same canonical action catalog boundary, without adding compatibility aliases or normal-play fallbacks.

## Validation Rules

- Every `ActionCatalog.ActionId` must be non-empty and unique.
- Every `ActionChains.ActionIds` entry must reference `ActionCatalog`.
- Every `NpcDecisionProfiles.Entries.ActionChainId` must reference `ActionChains`.
- Every action that can be selected by NPC ecology must have at least one matching `ActionTargetRules` row.
- Every `ActionTargetRules.ActionId`, when present, must reference `ActionCatalog`.
- Every requirement id must be known to `CampusNpcActionRequirementCatalog`.
- Old table names are not accepted as active gameplay data.
