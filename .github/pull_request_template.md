## What and why

<!-- What changes, and the problem it solves. Link the issue if there is one. -->

## Checklist

- [ ] `just lint-check` and `just test` pass
- [ ] `architests validate`, `architests waivers list --stale` and `architests api check` pass
- [ ] Tests added or updated, with **both a happy and an unhappy path**

### If this adds or changes a rule

- [ ] Evaluable from an in-memory fixture, with a test in `ArchitectureTesting.UnitTests`
- [ ] Paired behaviour scenarios in `ArchitectureTesting.AcceptanceTests`
- [ ] Added to `ArchitectureRules.Catalogue`
- [ ] Enabled in `.config/architecture-policy.json`, and this repository is clean under it
- [ ] Rule id added to both READMEs, `docs/Policy-Reference.md` and the skills

### If this changes public behaviour

- [ ] `architecture-policy.schema.json` updated; policy `version` bumped if a v3 reader would break
- [ ] `.config/api-surface.json` regenerated with `architests api capture`, and the diff reviewed
- [ ] `docs/`, both READMEs and the skills say the same thing

## Validation

<!-- State exactly what you ran. If a check was skipped or blocked, say so and why. -->
