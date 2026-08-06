---
name: ponytail-philosophy
description: "Lazy Senior Dev approach - YAGNI principle applied to coding, minimize code written"
metadata: 
  node_type: memory
  type: project
---

# Ponytail: Lazy Senior Dev Mode

Before writing ANY code for IdleDefenseSurvival, apply the **6-rung ladder** of efficiency. Stop at the first rung that holds:

1. **Based on Game Android Wild Survival - Idle Defense**
2. **Does this need to exist?** Apply YAGNI rigorously. Can the feature/fix be avoided entirely?
3. **Stdlib does it?** Use .NET/C# built-ins first (System.*, Unity built-ins).
4. **Native platform feature?** Unity provides 80% of what games need out-of-the-box.
5. **Installed dependency?** Use existing packages (in Packages/manifest.json).
6. **Only then:** Build the minimum that works.

## Expected Outcomes

Following this ladder yields:
- ~54% less code on average
- ~20% cheaper (fewer tokens, faster development)
- ~27% faster implementation
- 100% safety (understanding problem first, still full error handling)

## Application to Unity 2D Idle Defense

- **Based on Game Android Wild Survival - Idle Defense**
- **Reuse Unity systems**: Input System (already added), UGUI, Physics2D, Animator — don't build custom wrappers for features that exist.
- **Use ScriptableObject** for game config instead of custom JSON parsers or custom serialization.
- **Prefer composition**: Don't create deep class hierarchies when MonoBehaviour + ScriptableObject composition works.
- **Object pooling**: Only add if profiling shows GC pressure is a bottleneck — Unity 6 is pretty efficient.

## Why This Matters

This project is at risk of over-engineering: idle defense games are simple. Resist the urge to build "extensible" or "enterprise" patterns. Write the shortest working code first; refactor only when needed.

**Related:** [[CLAUDE-md]] (architecture for this project)
