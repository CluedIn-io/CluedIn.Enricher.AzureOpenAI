# Migrating a Connector/Enricher to Multi-Version Targeting

This document tracks the migration of `CluedIn.Enricher.AzureOpenAI` from a single-version build to
the multi-version targeting pattern. Part of a batch effort covering multiple CluedIn repos;
prior art consulted: `CluedIn.Connector.Dataverse.V2`, `CluedIn.Enricher.Gleif`,
`CluedIn.Enricher.OpenCorporates`, `CluedIn.Enricher.Permid`, `CluedIn.Enricher.Brreg`,
`CluedIn.Enricher.KnowledgeGraph`, `CluedIn.Enricher.ClearBit`, `CluedIn.Enricher.CompanyHouse`,
`CluedIn.Enricher.CVR`, `CluedIn.Enricher.DuckDuckGo` (all completed earlier in the same effort).

Branch: `feature/multi-version-targeting` (off `develop`).

---

## Overview

| CluedIn version | .NET TFM | Package suffix |
|---|---|---|
| 4.7.0 | net6.0 | `.470` |
| 4.8.0 | net6.0 | `.480` |
| 5.0.0-beta.* | net10.0 | `.500` |

4.6.0 excluded — no `IStreamRepository`/stream-API usage in this repo's small
`ExternalSearchProvider` surface that would require it (same reasoning as every prior repo in this
batch except AzureEventHubs/AzureDataLake, which had a concrete reason to include it).

This repo has **real test projects** (unlike most of the batch) — `test/unit` and `test/integration`
both have live csproj files, so the xunit v2/v3 split actually applies here, not just in theory.

---

## Step 1 — Pipeline template (`azure-pipelines.yml`)

Switched from `crawler.build.yml` (steps-template, explicit `UseDotNet@2` installing 8.0.x SDK —
stale, repo builds net10.0 via `global.json`) to `crawler.build.jobs.yml` with
`multiVersionCluedInTargets`. Also dropped `createIntegrationEnvironmentScriptFilePath:
"./build/integration-test.ps1"` — that script doesn't exist in this repo (`build/` only has
`assets/`), same dead-reference pattern GoogleMaps' doc found in a different repo.
`executeIntegrationTests` left at the caller's existing default (`false`): the one integration test
class (`AzureOpenAITests.cs`) is entirely wrapped in a disabled `#if AzureOpenAI_DEV` block with no
live test methods, so there's nothing to run either way.

---

## Step 2 — `Directory.Build.props`

Honours `CluedInMultiVersionTargetFramework` (net10.0 local fallback), derives
`CLUEDIN_V47`/`V48`/`V50` `DefineConstants`, pins `LangVersion` to 13.0 up front (a risk flagged by
prior repos in this batch; didn't actually bite here, but cheap to pin defensively).

---

## Step 3 — `Packages.props`

Guarded `_CluedIn`. Split test-tooling packages (`Microsoft.NET.Test.Sdk`, xunit v2 vs v3,
`AutoFixture.Xunit2` vs `.Xunit3`) into `CLUEDIN_V50`-conditioned `ItemGroup`s — this repo actually
needed this (has real test projects), unlike most of the batch. `CluedIn.Testing.Base` referenced
via the version-suffixed package ID (`CluedIn.Testing.Base.$(_CluedInPackageSuffix)` →
`.470`/`.480`/`.500`) — verified all three exist on the develop feed via the packaging REST API
before wiring this up (`CluedIn.Testing.Base.470`/`.480`/`.500`, all at `1.0.0-pr0013.2`).

`NuGet.Config` renamed from `Nuget.config` (wrong casing) — no feed changes needed, `develop`/
`release`/`AzurePipelines` already sufficient for 4.7.0/4.8.0 (no `public` feed required, unlike
AzureEventHubs).

---

## Step 4 — Test projects

`test/Directory.Build.props` stripped to just `IsTestProject` (was unconditionally referencing
`xunit.v3`/`AutoFixture.Xunit3` — would've caused a CS0433 clash on a non-V50 leg once its csproj
added xunit v2 conditionally, same trap the MasterDataServices doc describes). Added conditional
`ItemGroup`s (xunit v2+`AutoFixture.Xunit2` vs xunit.v3+`AutoFixture.Xunit3`) directly to both
`test/unit/.../ExternalSearch.AzureOpenAI.Unit.Tests.csproj` and
`test/integration/.../ExternalSearch.AzureOpenAI.Integration.Tests.csproj`.

No `GlobalUsings.cs` needed — checked both test files for `AutoFixture`/`ITestOutputHelper` usage,
found none (the unit test is a bare `[Fact]` using only `Xunit`; the integration test file is
entirely disabled by `#if AzureOpenAI_DEV`).

---

## Step 5 — API compatibility audit across 4.7.0 / 4.8.0 / 5.0.0-beta.*

Built for real (`dotnet build -p:_CluedIn=<v> -p:CluedInMultiVersionTargetFramework=<tfm>`) against
all three targets, both src projects and both test projects.

**RestSharp 106-vs-114 break** (same family every repo in this batch has hit) —
`AzureOpenAIExternalSearchProvider.cs` had 4 call sites:
- `Method.Post` doesn't exist pre-107 (106.x uses `Method.POST`) — 3 call sites, fixed with a
  `HttpPostMethod` const swapped via `#if CLUEDIN_V50`.
- `WaitDueToTooManyRequests`'s `response` parameter was typed as the concrete `RestResponse` (114+
  only); under 106.x, `client.Execute<T>()`/`client.Execute()` return `IRestResponse<T>`/
  `IRestResponse` instead. Fixed with a `RestResponseCompat` using-alias (`RestSharp.RestResponse`
  under `CLUEDIN_V50`, `RestSharp.IRestResponse` otherwise) — the method only touches `.Headers`,
  present on both.

Two other `client.Execute<T>()` call sites (`var`-inferred locals) needed no guard — same finding as
GoogleMaps' doc, only explicitly-declared-type locals/parameters break.

`4.7.0`/`4.8.0`/net6.0 and `5.0.0-beta.*`/net10.0 all build clean (0 errors) for both src projects
and both test projects after the fix.

---

## Step 6 — Reset the semantic version (`GitVersion.yml`)

This repo already had an `ignore: sha: []` block — merged `commits-before` into the existing block
rather than adding a second top-level `ignore:` key (CompanyHouse and CVR both hit this: a duplicate
YAML key is valid syntax but the later one silently wins, clobbering the earlier block with zero
error).

```yaml
next-version: 1.0
ignore:
  sha: []
  commits-before: 2026-06-20T00:00:00
```

Highest pre-existing tag is `4.7.1` at `2026-06-17T17:26:37+10:00` — padded 2 full days (not 1),
per Gleif's finding that `GitVersion.Tool 5.9.0` parses `commits-before` using local machine time
and fails silently on a tight margin.

---

## Checklist

- [x] `azure-pipelines.yml` — switched to `crawler.build.jobs.yml`; dropped dead integration-env script reference
- [x] `Directory.Build.props` — honours `CluedInMultiVersionTargetFramework`; `DefineConstants` derived; `LangVersion` pinned to 13.0
- [x] `Packages.props` — `_CluedIn` guarded; test-tooling split by `CLUEDIN_V50`; `CluedIn.Testing.Base` suffixed package ID (verified on feed)
- [x] `NuGet.Config` — renamed from `Nuget.config`; feeds confirmed sufficient
- [x] `test/Directory.Build.props` — stripped to `IsTestProject` only
- [x] Both test csprojs — conditional xunit v2/v3 + AutoFixture `ItemGroup`s; no `GlobalUsings.cs` needed
- [x] Source — `#if CLUEDIN_V50` guards for the RestSharp 106↔114 break (4 call sites, `HttpPostMethod` const + `RestResponseCompat` alias)
- [x] `GitVersion.yml` — merged into existing `ignore:` block; `next-version: 1.0`; `commits-before` padded 2 days
- [x] All legs build clean locally (src + both test projects) before pushing
- [x] Pushed branch and confirmed the Azure DevOps pipeline is green end-to-end — PR #24, build 151986: all three legs (4.7.0, 4.8.0, 5.0.0-beta.*) + `Multi-version: publish` passed on the first push
