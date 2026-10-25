# Security policy

`Purview.Architecture.Testing` is a build- and test-time package. It reads a repository's project
files and compiled assemblies and reports policy violations. It is nonetheless a **supply-chain
component**: it runs inside the CI of every repository that adopts it, so a defect here can affect
those repositories' builds.

## Reporting a vulnerability

**Do not open a public issue for a security report.**

Use GitHub's private reporting:
[Report a vulnerability](https://github.com/purview-dev/architests/security/advisories/new).

Please include the package and version, the policy or repository shape that triggers it, what you
observed, and what you expected. A minimal reproduction — a policy file and a project layout — is
far more useful than a description.

You can expect an acknowledgement within 5 working days and an assessment within 10. If a fix is
warranted, we will agree disclosure timing with you and credit you in the advisory unless you ask
us not to.

## Supported versions

| Version | Supported |
| -- | -- |
| 1.2.x and later | Yes |
| Earlier than 1.2 | No — upgrade |

Only the latest minor release receives fixes. The package follows semantic versioning, and the
policy document carries its own `version` field, so a patch release never requires a policy change.

## Scope

In scope:

- Arbitrary code execution, file write, or file read outside the repository root, triggered by a
  policy file, a project file, or a compiled assembly the tool reads.
- A rule that silently passes when it should fail — a false negative is the most damaging defect
  this package can have, because it converts a green build into false assurance.
- Credential or environment disclosure in output, reports, or the committed API baseline.

Out of scope:

- Findings that require an attacker to already control the build machine or the repository's own
  policy file *and* have write access to the repository.
- Denial of service from a deliberately enormous repository.
- Vulnerabilities in a dependency with no exploitable path through this package. Report those
  upstream; tell us too, and we will bump.

## Design commitments

These are properties the package maintains deliberately. A change that breaks one is a security
regression, not a refactor:

- **Assemblies are read by metadata only** (`System.Reflection.Metadata`). The package never calls
  `Assembly.Load`, `Assembly.LoadFrom`, or any API that executes code from an assembly it reads.
- **No network access at evaluation time.** The policy JSON Schema is embedded in the package
  rather than fetched, so validation never depends on, or leaks to, a remote host.
- **No process launching and no shell invocation.**
- **No regular expressions**, so there is no ReDoS surface in policy or path matching.
