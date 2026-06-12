# Agent Workflow

## Roles

| Agent | Scope | Can modify | Cannot modify |
|-------|-------|-----------|---------------|
| Agent A | Code hardening, build prep | `driver/*`, `src/*`, `tests/*` | docs, CI, VM, install |
| Agent B | Documentation sync | `docs/*`, `README.md` | driver, src, tests, CI |
| Agent C | Read-only safety review | Nothing (comments only) | All files |
| Orchestrator | Evidence and status check | Labels, issue status | Core code |
| Owner | Final approval for high-risk actions | Everything (with justification) | — |

## Issue lifecycle

```text
state:ready          → Agent picks up the issue
state:pr-open        → PR created, waiting for review
state:agent-c-review → Agent C is reviewing
state:owner-decision → Requires Owner approval
```

## GitHub Actions automation

Agent workflows run on GitHub Actions (cloud, no local session needed):

- Agent A: every 15 minutes
- Agent B: every 20 minutes
- Agent C: every 15 minutes

**Important:** These workflows only check labels and create branches/PRs. They do NOT approve merges, install drivers, or execute high-risk actions.

## Watchdog / GPT reminders

Owner-decision-watchdog is a **reminder/notification only** mechanism.

It does NOT:
- Approve VM testing
- Approve driver installation
- Approve signing
- Approve hardware testing
- Approve merges
- Block any action

It only posts a comment when an Issue or PR reaches `state:owner-decision`, to remind the Owner that a decision is needed.

**Owner is the sole authority for all high-risk actions.**

## High-risk labels

These labels indicate actions that require explicit Owner approval:

| Label | Meaning |
|-------|---------|
| `risk:high` | General high-risk task |
| `gate:owner-required` | Owner must approve before proceeding |
| `gate:vm-required` | Requires VM load test (Owner approves) |
| `gate:driver-install-required` | Requires driver install (Owner approves) |
| `gate:signing-required` | Requires code signing (Owner approves) |
| `gate:hardware-required` | Requires real hardware test (Owner approves) |
| `gate:rollback-required` | May need rollback plan (Owner reviews) |

## Safety boundary

All Agents are strictly forbidden from:

- `bcdedit /set testsigning on`
- `pnputil /add-driver`
- `devcon install`
- `sc create` / `sc start` / `sc delete`
- Modifying `HKLM\SYSTEM\...\LowerFilters`
- Disabling Secure Boot
- Installing or loading the driver
- Binding to real A2450 hardware
- Running VM passthrough tests
- Performing real key press tests
- Uploading unsigned `.sys` as release artifact

## Current project stage

MVP-A Pre-VM hardening completed and WDK verified.

- Driver: unsigned, not installed, not loaded, not bound to real hardware
- USB mode only (`VID_05AC&PID_029C`)
- VM test plan: draft exists, pending Owner approval
- Bluetooth: future (MVP-D)
