# Claude Code ToolBox — MCP & Skills awareness

_Generated: 2026-11-04T06:51:49.499Z_

## How to use this report

- **Saved copy:** This file is **`.claude/cloude-code-toolbox-mcp-skills-awareness.md`** — refreshed whenever the toolbox runs an MCP & Skills scan (including on workspace open when auto-scan is enabled). It is meant for **Claude Code workspace context** together with `CLAUDE.md` (which gets a shorter replaceable summary when auto-merge is on).
- **MCP:** Lists **configured** servers from Claude Code config (`~/.claude.json` for user scope, `.mcp.json` for project scope). Use `/mcp` in the Claude Code panel to connect servers for your session.
- **Skills:** **On-disk** folders with `SKILL.md`. Claude Code does not auto-load them; attach `SKILL.md` or paths in chat when useful.
- **Task routing:** When the user’s request matches a server’s purpose (e.g. Confluence → Confluence/Atlassian MCP), prefer that **server id** from the tables below.

---

## MCP — workspace

Workspace `mcp.json` _(folder: sabic-keemya-)_

- **c:\Users\HP\Desktop\sabic-keemya-\.mcp.json** — _File missing_

_No active workspace servers in mcp.json._

## MCP — user profile

- **C:\Users\HP\.claude.json** — _File exists — servers defined_

| Server id | Kind | Detail |
|-----------|------|--------|
| rea | stdio | C:\Program Files\nodejs\node.exe C:\Users\HP\AppData\Roaming\npm\node_modules\rea-agents\scripts\rea.mjs mcp |

## Skills (local `SKILL.md` folders)

### Project-scoped

_None found (or no workspace open)._

### User-scoped

- **agent-introspection-debugging** — `C:\Users\HP\.claude\skills\agent-introspection-debugging`
  - Structured self-debugging workflow for AI agent failures using capture, diagnosis, contained recovery, and introspection reports. Use when an agent run fails and you need a reproducible diagnosis instead of a retry.

- **agent-self-evaluation** — `C:\Users\HP\.claude\skills\agent-self-evaluation`
  - Use after completing any non-trivial task. The agent self-rates its output on 5 axes — accuracy, completeness, clarity, actionability, conciseness — with concrete evidence per criterion. Produces a structured 1-5 scoreca

- **agent-sort** — `C:\Users\HP\.claude\skills\agent-sort`
  - Build an evidence-backed ECC install plan for a specific repo by sorting skills, commands, rules, hooks, and extras into DAILY vs LIBRARY buckets using parallel repo-aware review passes. Use when ECC should be trimmed to

- **ai-regression-testing** — `C:\Users\HP\.claude\skills\ai-regression-testing`
  - Regression testing strategies for AI-assisted development. Sandbox-mode API testing without database dependencies, automated bug-check workflows, and patterns to catch AI blind spots where the same model writes and revie

- **architecture-decision-records** — `C:\Users\HP\.claude\skills\architecture-decision-records`
  - Capture architectural decisions as numbered ADR markdown files in docs/adr/ with context, alternatives considered, consequences, and an index README. Use when the user says 'record this decision' or 'ADR this', chooses b

- **browser-qa** — `C:\Users\HP\.claude\skills\browser-qa`
  - Run automated post-deploy UI verification with a browser automation MCP (claude-in-chrome, Playwright, or Puppeteer): console-error and Core Web Vitals smoke checks, form and auth-flow interaction tests, screenshot visua

- **ck** — `C:\Users\HP\.claude\skills\ck`
  - Persistent per-project memory for Claude Code (Context Keeper) driven by deterministic Node.js /ck commands: init, save, resume, info, list, forget, and v1-to-v2 migrate, plus a SessionStart hook that injects a compact p

- **click-path-audit** — `C:\Users\HP\.claude\skills\click-path-audit`
  - Trace every user-facing button/touchpoint through its full state change sequence to find bugs where functions individually work but cancel each other out, produce wrong final state, or leave the UI in an inconsistent sta

- **code-tour** — `C:\Users\HP\.claude\skills\code-tour`
  - Create CodeTour `.tour` files — persona-targeted, step-by-step walkthroughs with real file and line anchors. Use for onboarding tours, architecture walkthroughs, PR tours, RCA tours, and structured "explain how this work

- **codebase-onboarding** — `C:\Users\HP\.claude\skills\codebase-onboarding`
  - Analyze an unfamiliar codebase and generate a structured onboarding guide with architecture map, key entry points, conventions, and a starter CLAUDE.md. Use when joining a new project or setting up Claude Code for the fi

- **codehealth-mcp** — `C:\Users\HP\.claude\skills\codehealth-mcp`
  - Real-time structural Code Health via CodeScene MCP — review before edits, verify score deltas after changes, gate commits and PRs. Use when reviewing code quality, refactoring, checking if AI changes degraded a file, or 

- **config-gc** — `C:\Users\HP\.claude\skills\config-gc`
  - Garbage collection for your Claude Code configuration. Periodically scans ~/.claude (skills, memory, hooks, permissions, MCP servers, caches) for redundant, stale, orphaned, or low-value items, then walks the user throug

- **configure-ecc** — `C:\Users\HP\.claude\skills\configure-ecc`
  - Run the conversational ECC setup wizard inside the current harness: inventory the install, collect scope (user/project/local) and hook mode (off/minimal/standard/strict) in Claude Code, use Codex's native plugin lifecycl

- **context-budget** — `C:\Users\HP\.claude\skills\context-budget`
  - Audits Claude Code context window consumption across agents, skills, MCP servers, and rules. Identifies bloat, redundant components, and produces prioritized token-savings recommendations. Use when the context window is 

- **continuous-learning** — `C:\Users\HP\.claude\skills\continuous-learning`
  - [DEPRECATED - use continuous-learning-v2] Legacy v1 stop-hook skill extractor. v2 is a strict superset with instinct-based, project-scoped, hook-reliable learning. Do not invoke v1: when continuous learning, session lear

- **continuous-learning-v2** — `C:\Users\HP\.claude\skills\continuous-learning-v2`
  - Instinct-based learning system that observes sessions via hooks, creates atomic instincts with confidence scoring, and evolves them into skills/commands/agents. v2.1 adds project-scoped instincts to prevent cross-project

- **council** — `C:\Users\HP\.claude\skills\council`
  - Convene a four-voice council for ambiguous decisions, tradeoffs, and go/no-go calls. Use when multiple valid paths exist and you need structured disagreement before choosing.

- **council-multi-model** — `C:\Users\HP\.claude\skills\council-multi-model`
  - Add one optional external Codex critique after the existing council has produced a decision draft. Use when an ambiguous, high-consequence decision would benefit from a separate model invocation's attempt to break the sy

- **delivery-gate** — `C:\Users\HP\.claude\skills\delivery-gate`
  - Stop hook that blocks Claude from finishing until quality checks pass. Detects rationalization patterns (surface text heuristics), stale learning logs (filesystem mtime), and low disk space. Complements self-audit by mec

- **dev-team** — `C:\Users\HP\.claude\skills\dev-team`
  - Simulate a collaborative dev team session where multiple role-based personas (PM, Architect, Developer, QA) respond to the same problem together in one session. Use when designing a feature, reviewing a proposal, or onbo

- **e2e-testing** — `C:\Users\HP\.claude\skills\e2e-testing`
  - Playwright E2E testing patterns, Page Object Model, configuration, CI/CD integration, artifact management, and flaky test strategies. Use when writing Playwright tests, structuring page objects, or fixing flaky E2E runs 

- **ecc-guide** — `C:\Users\HP\.claude\skills\ecc-guide`
  - Answer questions about ECC by reading the live repo surface — agents, skills, commands, hooks, rules, install profiles, and docs — instead of memory. Use when the user asks what ECC includes, how to install or reset it, 

- **ecc-recipes** — `C:\Users\HP\.claude\skills\ecc-recipes`
  - Map a described workflow to the right ECC command group with run-order and stop condition, or browse all command-group recipe families read live from the commands directory. Advisory only — never executes. Use when asked

- **error-handling** — `C:\Users\HP\.claude\skills\error-handling`
  - Patterns for robust error handling across TypeScript, Python, and Go. Covers typed errors, error boundaries, retries, circuit breakers, and user-facing error messages. Use when designing error types, retries, circuit bre

- **eval-harness** — `C:\Users\HP\.claude\skills\eval-harness`
  - Eval-driven development (EDD) framework for AI coding sessions — define capability and regression evals before coding, grade with code-based, model-based, rule, or human graders, and track pass@k and pass^k reliability. 

- **git-workflow** — `C:\Users\HP\.claude\skills\git-workflow`
  - Git workflow patterns including branching strategies, commit conventions, keeping history clean and readable, tidying local commits before merging, merge vs rebase, conflict resolution, and collaborative development best

- **growth-log** — `C:\Users\HP\.claude\skills\growth-log`
  - Write growth log entries that extract reusable patterns from completed work — root cause, transferable rule, and a recognizable signal — instead of diary-style event narration, with a 4-8 sentence template and merge-dupl

- **hookify-rules** — `C:\Users\HP\.claude\skills\hookify-rules`
  - Create and configure hookify rules — markdown files with YAML frontmatter that match bash, file, prompt, or stop events by regex or conditions and show warn/block messages to the agent. Use when creating a hookify rule, 

- **inherit-legacy-style** — `C:\Users\HP\.claude\skills\inherit-legacy-style`
  - Prevent AI style drift on legacy projects by scanning the codebase for implicit conventions, resolving conflicts with the operator one at a time, and writing an enforceable .ai-style-rules.md (Golden Files, naming rules,

- **intent-driven-development** — `C:\Users\HP\.claude\skills\intent-driven-development`
  - Turn ambiguous or high-impact product and engineering changes into scoped, verifiable acceptance criteria before or alongside implementation. Use when a user asks to clarify a feature, define acceptance criteria, de-risk

- **iterative-retrieval** — `C:\Users\HP\.claude\skills\iterative-retrieval`
  - Pattern for progressively refining context retrieval to solve the subagent context problem. Use when a subagent lacks the context it needs and retrieval must be refined across passes.

- **living-docs-governance** — `C:\Users\HP\.claude\skills\living-docs-governance`
  - Keep a long-lived project's documentation from rotting by assigning existing project docs clear constitution, map, status, and history roles, then wiring the active agent harness to those canonical sources. Use in the ma

- **loop-design-check** — `C:\Users\HP\.claude\skills\loop-design-check`
  - Design a goal-oriented agent loop or review one for failure modes: spinning, Goodhart-gaming the verifier, or running a wrong answer to completion. Covers machine-decidable goals, loop types, plan/build/judge skeletons, 

- **plan-canvas** — `C:\Users\HP\.claude\skills\plan-canvas`
  - Open plans and HTML artifacts in a local browser canvas where the human annotates elements, chats, and approves or requests changes without leaving the page. Use when presenting a plan for review, or when feedback like "

- **plankton-code-quality** — `C:\Users\HP\.claude\skills\plankton-code-quality`
  - Write-time code quality enforcement using Plankton — auto-formatting, linting, and Claude-powered fixes on every file edit via hooks. Use when setting up write-time formatting, linting, or auto-fix hooks on file edits.

- **product-lens** — `C:\Users\HP\.claude\skills\product-lens`
  - Validate the why before building through four product diagnostics — a YC-style product diagnostic that produces PRODUCT-BRIEF.md with a go/no-go recommendation, a founder review scoring product-market-fit signals, a user

- **production-audit** — `C:\Users\HP\.claude\skills\production-audit`
  - Local-evidence production readiness audit for shipped apps, pre-launch reviews, post-merge checks, and "what breaks in prod?" questions without sending repo data to an external audit service. Use when auditing production

- **repo-scan** — `C:\Users\HP\.claude\skills\repo-scan`
  - Bootstrap pointer that installs the external repo-scan skill from a pinned, reviewable commit. Use when repo-scan must be installed before running its cross-stack source-code asset audit; this ECC pointer does not perfor

- **reverse-engineer-anything** — `C:\Users\HP\.claude\skills\reverse-engineer-anything`
  - Reverse engineer native, managed, Electron/JavaScript, packaged, firmware, and browser targets with REA. Use shipped-artifact or requested runtime evidence to explain features, compare versions, decompile code, or guide 

- **rules-distill** — `C:\Users\HP\.claude\skills\rules-distill`
  - Scan skills to extract cross-cutting principles and distill them into rules — append, revise, or create new rule files. Use when the same principle keeps recurring across skills and belongs in a rule file instead.

- **santa-method** — `C:\Users\HP\.claude\skills\santa-method`
  - Multi-agent adversarial verification: two independent reviewers with the same rubric must both pass before output ships, with a fix-and-re-review convergence loop and human escalation cap. Use when gating publishing, pro

- **skill-comply** — `C:\Users\HP\.claude\skills\skill-comply`
  - Visualize whether skills, rules, and agent definitions are actually followed — auto-generates scenarios at 3 prompt strictness levels, runs agents, classifies behavioral sequences, and reports compliance rates with full 

- **skill-scout** — `C:\Users\HP\.claude\skills\skill-scout`
  - Search existing local, marketplace, GitHub, and web skill sources before creating a new skill. Use when the user wants to create, build, fork, or find a skill for a workflow.

- **skill-stocktake** — `C:\Users\HP\.claude\skills\skill-stocktake`
  - Use when auditing Claude skills and commands for quality. Supports Quick Scan (changed skills only) and Full Stocktake modes with sequential subagent batch evaluation.

- **strategic-compact** — `C:\Users\HP\.claude\skills\strategic-compact`
  - Suggests manual context compaction at logical intervals to preserve context through task phases rather than arbitrary auto-compaction. Use when a session is approaching a context limit and a task phase is a natural place

- **tdd-workflow** — `C:\Users\HP\.claude\skills\tdd-workflow`
  - Test-driven development workflow: write a failing test first, watch it fail, implement the smallest change to green, then refactor with 80%+ coverage across unit, integration, and E2E tests. Use when writing a new featur

- **unified-memory** — `C:\Users\HP\.claude\skills\unified-memory`
  - Share durable, inspectable context and handoffs between Claude, Codex, Hermes, Cursor, OpenCode, and other agents through the local ECC Memory Vault. Use when an agent must save work state, transfer context, resume anoth

- **verification-loop** — `C:\Users\HP\.claude\skills\verification-loop`
  - Run a six-phase verification of a Claude Code session's work — build, type check, lint, tests with coverage, security grep, and diff review — then produce a PASS/FAIL verification report. Use when verifying work after co

- **windows-desktop-e2e** — `C:\Users\HP\.claude\skills\windows-desktop-e2e`
  - E2E testing for Windows native desktop apps (WPF, WinForms, Win32/MFC, Qt) using pywinauto and Windows UI Automation. Use when writing E2E tests for a Windows native desktop app with pywinauto or UI Automation.

---

## Suggested next steps

- **MCP:** Use this extension’s hub **MCP** tab, or `claude mcp list` in the terminal. In Claude Code, use `/mcp` to connect servers for the session.
- **Edit config:** Open `~/.claude.json` (user MCP) or `<workspace>/.mcp.json` (project MCP) via the extension commands.
- **Refresh this report:** run **Intelligence — scan MCP & Skills awareness** again after changing MCP config or adding skills.

_Report from Claude Code ToolBox extension._
