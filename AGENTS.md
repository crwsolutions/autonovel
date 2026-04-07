# AGENTS.md

## Setup

```bash
uv sync
cp .env.example .env  # Configure AUTONOVEL_MODEL and API endpoint
```

Environment variables:
- `AUTONOVEL_MODEL` — model name for OpenAI-compatible endpoint (default: `openai-compatible`)
- `AUTONOVEL_API_BASE_URL` — llama.cpp server URL (default: `http://crw-amd3900x:8080`)

## Commands

**Full pipeline:** `uv run python run_pipeline.py --from-scratch`

**Phase-specific:**
- Foundation: `uv run python run_pipeline.py --phase foundation`
- Drafting: `uv run python run_pipeline.py --phase drafting`
- Revision: `uv run python run_pipeline.py --phase revision --max-cycles 5`
- Export: `uv run python run_pipeline.py --phase export`

**Evaluation:**
- `uv run python evaluate.py --phase=foundation` — score planning docs
- `uv run python evaluate.py --chapter=5` — score single chapter
- `uv run python evaluate.py --full` — score entire novel

**Typesetting:** `python3 typeset/build_tex.py && cd typeset && tectonic novel.tex`

## Architecture

**Five co-evolving layers** (changes propagate both directions):
```
Layer 5: voice.md       — Style guardrails + discovered exemplars
Layer 4: world.md       — Lore, magic system, geography, history
Layer 3: characters.md  — Registry with wound/want/need/lie chains
Layer 2: outline.md     — Beats + foreshadowing ledger
Layer 1: chapters/ch_*.md — The prose
Cross-cutting: canon.md — Hard facts database (consistency source of truth)
```

**State tracking:** `state.json` tracks current phase, iteration, and propagation debts.

## Workflow Conventions

**Branching:** Always create branch `autonovel/<tag>` from master. Master contains only framework/templates; never story content.

**Keep/discard loop:** Every generation is evaluated immediately.
- Foundation: keep if `foundation_score > 7.5 AND lore_score > 7.0`
- Drafting: keep if `score > 6.0` (forward progress over perfection)
- Revision: stop when scores plateau (Δ < 0.5 across 2 cycles) or Opus review has no major unqualified items

**Git workflow:** Commit after every successful evaluation. If score regresses, `git reset --hard HEAD~1`.

**Canon updates:** After every chapter draft, extract `new_canon_entries` from eval output and append to `canon.md`.

## Critical Pitfalls

**Stability trap:** AI favors stability over change. Actively fight:
- Characters must end truly different
- Let bad things stay bad
- Withhold information; maintain mystery
- Irreversible decisions and loss

**Slop patterns:** `evaluate.py` scans for:
- OVER-EXPLAIN (~32% of cuts) — narrator explains what scenes showed
- REDUNDANT (~26%) — same insight restated 3-4 times
- Tier-1 banned words, em-dash overuse, sentence-length uniformity

**Revision gotchas:**
- Over-compression below 1800w makes chapters weakest
- `gen_revision.py` adds ~30% more words than briefed
- Fixing one pacing stretch exposes the next (7 may be ceiling)

## File Types

**Framework** (master, never edited by pipeline): `program.md`, `CRAFT.md`, `ANTI-SLOP.md`, `ANTI-PATTERNS.md`, `PIPELINE.md`, `WORKFLOW.md`

**Templates** (empty shells, filled per-novel): `voice.md` Part 2, `world.md`, `characters.md`, `outline.md`, `canon.md`, `MYSTERY.md`

**Generated:** `chapters/ch_*.md`, `state.json`, `results.tsv`, `edit_logs/*.json`, `eval_logs/*.json`, `briefs/*.md`

## Opus Review Loop (Phase 3b)

After automated revision cycles, send full manuscript to `review.py` with dual-persona prompt (literary critic + professor of fiction). Stop when:
- No major unqualified items remain
- >50% of items are qualified/hedged
- ≤2 items found

The reviewer will always find something; stopping condition is severity, not zero defects.

## External Dependencies

- `tectonic` — LaTeX compiler for typesetting
