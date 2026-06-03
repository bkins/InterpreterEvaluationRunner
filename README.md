# InterpreterEvaluationRunner

A .NET console application that evaluates local Ollama models against the Cognitive Platform (CP) interpreter contract. Measures action selection accuracy, parameter extraction, schema compliance, missing-parameter guard, clarification triggering, and latency.

Part of **ENH-23 Phase 2** — evaluation infrastructure that must precede any fine-tuning (architectural principle #4: "Evaluation Before Training").

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Ollama](https://ollama.com) running at `http://localhost:11434`
- Target models pulled:

```
ollama pull phi3:mini
ollama pull llama3.1:8b
ollama pull qwen2.5:7b
ollama pull qwen2.5:14b
ollama pull mistral
```

---

## How to Run

```
dotnet run
```

Results are printed to the console with Spectre.Console and exported to `Output/results-<timestamp>.json`.

To override which models run, edit `appsettings.json`:

```json
{
  "Evaluation": {
    "Models": ["phi3:mini", "llama3.1:8b"]
  }
}
```

The default `appsettings.json` reproduces the ENH-23 Phase 1 benchmark: all 5 models in Phase 1 recommendation order.

---

## Project Structure

```
PromptBuilder.cs               — Builds the system prompt (v3, 9-action CP catalog including CompleteTask, BatchAddTasks, ChitChat, ListActions, DescribeAction)
OllamaClient.cs                — Ollama HTTP client (POST /api/generate)
ResultScorer.cs                — Scores each test case (parsing, validation, intent, params, clarification)
ResultExporter.cs              — Exports results to Output/*.json
Interpreter/Pipeline/          — Repair → Normalize → Validate pipeline
Data/benchmark/                — Test case JSON files (loaded at startup, all *.json files)
```

### Benchmark files

| File | Category | Cases | Description |
|------|----------|-------|-------------|
| `cp-phase1.json` | Phase 1 | 8 | ENH-23 Phase 1 T01–T08 reproduced in the runner |
| `cp-phase2.json` | Phase 2 | 17 | Action selection, parameter extraction, ambiguity, schema compliance |
| `cp-missing-param-guard.json` | MissingParamGuard | 5 | Missing required parameter variants |
| `cp-clarification.json` | Clarification | 4 | Clarification triggering (checks `ClarificationWasCorrect`) |
| `cp-schema-compliance.json` | SchemaCompliance | 8 | All action types, valid JSON structure |
| `exact-intent.json` | ExactIntent | 2 | Direct action mapping |
| `testcases.json` | Mixed | 3 | Basic add task, missing params, ambiguous intent |
| `missing-parameters.json` | MissingParameters | 1 | Missing journal content |
| `ambiguity.json` | AmbiguousIntent | 1 | Vague input |
| `conversational-noise.json` | ConversationalNoise | 1 | Reminder-style phrasing |
| `unsupported.json` | Unsupported | 1 | Out-of-schema request |
| `Ambiguous-task-listing.json` | AmbiguousIntent | 1 | Vague listing request |
| `Missing-task-title.json` | MissingParameters | 1 | AddTask with no title |
| `Typo-in-task-request.json` | ExactIntent | 1 | Typo tolerance |
| `Two-actions-requested.json` | AmbiguousIntent | 1 | Multi-action utterance |
| `Pronoun-reference.json` | AmbiguousIntent | 1 | Pronoun without referent (single-turn) |
| `Reference-previous-task.json` | AmbiguousIntent | 1 | Context-dependent reference (single-turn) |
| `Ignore-previous-instructions.json` | SecurityTest | 1 | Prompt injection attempt |

All files under `Data/benchmark/` are loaded automatically at startup via `*.json` glob.

---

## Scoring Model

Each test case is scored 0–100:

| Check | Points | Notes |
|-------|--------|-------|
| JSON parsed successfully | +10 | Raw parse — before any repair |
| Schema validation passes | +10 | actionName, confidence, failureType all valid |
| Correct action (intent) | +50 | `actionName` matches `expectedAction` |
| Correct failure type | +60 | Only for `expectsFailure: true` cases |
| Parameters correct | +20 | Exact key/value match; skipped if `skipParameterCheck: true` |
| No repair needed | +10 | Bonus for clean JSON that required no post-processing |

> **Note:** `expectsFailure: true` tests score via the failure-type path (max 80 pts). Standard tests score via the intent + parameter path (max 100 pts).

Clarification (`shouldRequireClarification: true`) is recorded in `ClarificationWasCorrect` but does not add or subtract score points — it is a supplementary signal.

---

## ENH-23 Phase 1 Baseline Results

These are the **original Phase 1 results** from `enh23_benchmark.py` (Python, full CP `system.txt` prompt ~5,917 tokens, CPU-only). Scoring was 0–10 per test (2 pts valid JSON, 3 pts action, 2 pts failureType, 3 pts parameters).

### Phase 1 Schema Compliance Scores (T01–T08)

| Model | T01 | T02 | T03 | T04 | T05 | T06 | T07 | T08 | Total | % |
|-------|-----|-----|-----|-----|-----|-----|-----|-----|-------|---|
| phi3:mini | 10 | 10 | 10 | 10 | 10 | 10 | 10 | 10 | **80/80** | **100%** |
| llama3.1:8b | 10 | 10 | 10 | 10 | 10 | 10 | 10 | 10 | **80/80** | **100%** |
| qwen2.5:14b | 10 | 10 | 10 | 10 | 10 | 10 | 10 | 10 | **80/80** | **100%** |
| qwen2.5:7b | 10 | 10 | 10 | 10 | 10 | 8 | 10 | 10 | **78/80** | **98%** |
| mistral | 10 | 10 | 10 | 7 | 10 | 10 | 10 | 10 | **77/80** | **96%** |

**T04 is the safety-critical missing-parameter guard test.** Mistral failed it by dispatching `AddTask` when `shortDescription` was absent. The CP engine requires `actionName: "None"` + `failureType: "MissingParameters"` in this case.

### Phase 1 Pass/Fail Per Model

| Model | Phase 4 Candidate | Notes |
|-------|------------------|-------|
| **phi3:mini (3.8B)** | ✅ **Primary** | 100% compliance, 14.5 tok/s, 2.2 GB, ~1 s warm TTFT |
| **llama3.1:8b (8.0B)** | ✅ **Secondary** | 100% compliance, 9.2 tok/s, 4.9 GB |
| qwen2.5:7b (7.6B) | ⚠️ Conditional | 98% — T06 formatting edge case, minor |
| qwen2.5:14b (14.8B) | ⚠️ GPU only | 100% but 4.9 tok/s on CPU (~12–16 s per call) |
| mistral (7.2B) | ❌ Disqualified | T04 missing-param guard failure is a CP safety invariant |

### Phase 1 Warm Latency (CPU-only, model already loaded)

| Model | Warm TTFT | tok/s | Simple total | Complex total |
|-------|-----------|-------|-------------|---------------|
| phi3:mini | ~1.0 s | 14.5 | ~4.7 s | ~8.0 s |
| qwen2.5:7b | ~1.1 s | 10.3 | ~4.0 s | ~5.9 s |
| llama3.1:8b | ~1.1 s | 9.2 | ~6.4 s | ~8.3 s |
| mistral | ~1.0 s | 9.4 | ~6.6 s | ~8.6 s |
| qwen2.5:14b | ~2.1 s | 4.9 | ~11.9 s | ~15.7 s |

All results on CPU only (no GPU VRAM). GPU acceleration would be 5–20× faster.

---

## Gap Analysis: Runner vs ENH-23 Phase 2 Needs

| ENH-23 Phase 2 Metric | Runner Coverage | Status |
|-----------------------|----------------|--------|
| Schema compliance % (valid JSON + structure) | ✅ Full — repair pipeline + validator | Covered |
| Action selection accuracy | ✅ Full — intent scoring path | Covered |
| Parameter extraction accuracy | ✅ Full — exact key/value match; `skipParameterCheck` for complex cases | Covered |
| Missing-parameter guard rate | ✅ Full — `cp-missing-param-guard.json` (5 cases) + T04 in Phase 1 | Covered |
| Clarification triggering | ⚠️ Partial — tracked in `ClarificationWasCorrect`, not score-weighted | Signal only |
| Warm TTFT / latency (ms) | ✅ Full — `LatencyMs` per result, avg in summary table | Covered |
| Tokens/sec | ✅ Full — captured from Ollama `eval_count`/`eval_duration`; shown in summary table | Covered |
| FastPath regression | ⚠️ Not in this runner — run `dotnet test` on `CognitivePlatform.Tests` | External |
| Ambiguity handling | ✅ Partial — `ambiguity.json` (1 case) + `cp-schema-compliance.json` SCH-04 | Sparse |
| Date parsing | ❌ No date-relative test cases (Phase 2 category #5) | Gap |
| Multi-turn / clarification rounds | ❌ Single-turn only | Gap |

### Priority gaps for Phase 2 expansion

1. **Ambiguity tests** — expand from 5 cases to ~10 covering calendar vs. task routing, overlapping action intent.
2. **Date parsing** — add test cases with relative date expressions ("this week", "yesterday", "last month").
3. **Clarification scoring** — currently tracked in `ClarificationWasCorrect` as a signal; weight it into the score.
4. **FastPath regression** — run `dotnet test` on `CognitivePlatform.Tests` separately; not in this runner.

---

## ENH-23 Phase 2 Acceptance Targets

| Metric | Target |
|--------|--------|
| Action accuracy | ≥ 98% |
| Parameter extraction accuracy | ≥ 95% |
| Schema compliance | 100% |
| Missing-parameter guard rate | 100% |
| Warm TTFT | < 2,000 ms |
| Clarification trigger rate | ≥ 90% |
