# Changelog

All notable changes to this project are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html). The **policy document
version** is versioned separately, in `architecture-policy.schema.json`; it is currently **3**.

## [Unreleased]

### Security

- **Paths in the policy are confined to the repository.** `sourceRoots`, `assemblyRules.assemblyRoots`
  and `apiRules.baseline` were joined onto the repository root with `Path.Combine`, which discards
  the root entirely when the second argument is absolute — so `/etc` or `C:\Windows` escaped with
  no `..` at all. The baseline is the serious one, because `architests api capture` *writes* it: a
  policy could choose where. A `--baseline` given on the command line is still honoured anywhere,
  because that is the operator's explicit instruction rather than the policy's.
- **`assemblyGlobs` may no longer contain a path separator or `..`.** .NET accepts a relative path
  in a search pattern, so `"../*.dll"` enumerated the parent directory and walked straight out of
  the root that had just been confined. Confining the root without validating the pattern left the
  hole open.
- **The assembly reader is bounded.** `ResolveScopeAssembly` followed a type reference's resolution
  scope in an unbounded loop. A corrupt or hand-crafted assembly whose scope chain points back at
  itself hung the process, and the input is every `*.dll` under a configured root.
- **An unknown key in the policy is now an error.** A mistyped rule group left that group at its
  default — usually off — so an author believed a rule was enforced while nothing checked it.
- A policy larger than 4 MB is reported rather than read, and both XML readers state their
  DTD-prohibited settings explicitly rather than inheriting them from a framework default.

### Fixed

- **`portPurity` matches a prefix, as it is named and documented.** It used a substring search, so
  `System.Data` flagged an unrelated `Contoso.System.DataPoint`. It now also looks inside generic
  arguments, where a technology is exposed just as plainly.
- **`waiverIntegrity` reports two waivers for the same project.** Only the first ever applied, so
  the rules named by the second were still enforced while appearing excused.
- A malformed `.csproj` or XML documentation file is reported against the file that caused it,
  rather than aborting the run with a stack trace that never names it.
- Corrected the swapped XML documentation on `forbiddenAttributes` and `requiredAttributes`, the
  `api check` usage text omitting `sarif`, and `docs/Extending.md` listing three of
  `ArchitectureContext`'s five members.

### Added

- **`architests check`** runs the whole rule set outside TUnit, with `--format text|json|sarif`.
  Until now the 22 rules were reachable only through a test project: `validate` checks the policy
  document, against an empty graph, so no rule that reads the repository ran. This is the command
  for a repository with no test project, or a pipeline step that should fail before the tests.
- **SARIF output carries rule metadata.** The renderer is now shared between `check` and
  `api check`, and builds its `rules` array from `ArchitectureRules.Catalogue`, so each finding
  arrives with the rule's title and description and a new rule needs no change here. Findings
  point at the offending `.csproj`; a non-waivable rule is reported at `error`, a waivable one at
  `warning`.
- `PolicyPath`, the shared containment helper, and `ArchitecturePolicy.Conventions` /
  `.Description` / `.Schema`, which bind the documentation-only keys the schema already allowed.

## [1.3.0]

A minor release: the public API grows, nothing is removed, and no policy change is required.
Package validation confirms binary compatibility with 1.2.0.

### Fixed

- **`apiSurface` no longer crashes on legitimate overloads.** A member's identity was built from
  its kind, name and parameter types, which collides on two overloads that differ only in generic
  arity or return type — both legal C#. `architests api check` threw `ArgumentException` out of the
  comparison before it could report anything. Members now carry their generic arity, and
  comparison groups by overload identity instead of keying a dictionary, so a duplicate is reported
  rather than thrown. A single overload whose return type changed is still reported as a *change*,
  not a removal plus an addition. Reported by `purview-dev/changeops`.
- **The type rules no longer report compiler-synthesized types.** A C# extension block is lowered
  into a nested container named `<G>$<hash>` that carries no attributes at all, so
  `excludeGeneratedCode` — which only looked for `[GeneratedCode]` and `[CompilerGenerated]` —
  missed it. `typeShape` reported it as a utility class that forgot to be `static` and
  `typeNamespace` as a type outside the root namespace, neither of which an author can act on.
  "Generated" now means attribute-marked **or** compiler-synthesized. The same exclusion applies to
  `apiDocumentation` and to the captured API baseline, where a hash in a recorded name would
  otherwise churn the committed contract on every nearby edit.
- **A public nested type now takes its namespace from the type that declares it.** Metadata records
  no namespace on a nested type, so `typeNamespace` reported every public nested type as living
  outside its project's root namespace.

### Changed

- **`portImplementation` accepts an `internal` implementation.** The rule asks whether a boundary
  was crossed, and visibility does not change the answer: an adapter only the composition root
  wires is commonly `internal`, as are generator-emitted ones. Set the new
  `portRules.requirePublicImplementation` to `true` to restore the stricter check. This only ever
  relaxes the rule, so no existing policy newly fails.

### Added

- `portRules.requirePublicImplementation` (boolean, default `false`).
- `src/tests/ArchitectureTesting.AcceptanceTests`: Given/When/Then behaviour scenarios written with
  [BDTest](https://github.com/thomhurst/BDTest), pairing a happy path with an unhappy path for every
  rule they cover.
- Supply-chain and governance hardening: SHA-pinned reusable workflows with an explicit secret set,
  a pinned `build-version`, `<clear />` and package source mapping in `nuget.config`, committed
  `packages.lock.json` files, `TreatWarningsAsErrors`, an escalated `NuGetAudit`, package
  validation against the 1.2.0 baseline, code coverage collection, CodeQL, dependency review,
  OSSF Scorecard, Dependabot, `SECURITY.md`, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `CODEOWNERS`
  and issue and pull request templates.

### Public API

`APIMember.GenericArity`, `APIBaselineMember.GenericArity`, `APIBaselineMember.OverloadKey`,
`APIBaselineMember.Signature`, `APIType.IsCompilerSynthesized` and
`PortRules.RequirePublicImplementation` are new. All are additive;
`.config/api-surface.json` has been regenerated.

## [1.2.0]

Released. See the [GitHub releases](https://github.com/purview-dev/architests/releases) for
earlier history.

[Unreleased]: https://github.com/purview-dev/architests/compare/v1.3.0...HEAD
[1.3.0]: https://github.com/purview-dev/architests/compare/v1.2.0...v1.3.0
[1.2.0]: https://github.com/purview-dev/architests/releases/tag/v1.2.0
