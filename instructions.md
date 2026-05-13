# WikiQA Agent — Running Instructions

## Prerequisites

- **OS**: Windows, macOS, or Linux (x64)
- **No runtime required** — the binaries are self-contained
- **An Anthropic API key** — the agent and eval judges both call Claude

---

## Setting Your API Key

The agent reads `ANTHROPIC_API_KEY` from the environment. Set it before running.

**Windows (PowerShell)**
```powershell
$env:ANTHROPIC_API_KEY = "sk-ant-..."
```

**macOS / Linux**
```bash
export ANTHROPIC_API_KEY="sk-ant-..."
```

---

## Picking the Right Binary

| Folder | Binary | Platform |
|---|---|---|
| `win-x64/` | `WikiQA.App.exe` | Windows 10/11 (x64) |
| `linux-x64/` | `WikiQA.App` | Ubuntu / Debian / RHEL (x64) |
| `osx-x64/` | `WikiQA.App` | macOS (Intel) |

On macOS/Linux, make the binary executable first:
```bash
chmod +x WikiQA.App
```

All commands below use `./WikiQA.App` — substitute `WikiQA.App.exe` on Windows.

---

## Demo Mode

Run the agent interactively. Ask any question and get a Wikipedia-grounded answer with inline citations.

```bash
./WikiQA.App
```

You will be prompted:
```
WikiQA Agent
============
Enter your question:
```

Type any factual question and press Enter. The agent searches Wikipedia in real time and synthesizes an answer.

---

## Eval Mode

Run any eval suite by passing `--eval <suite-name>`:

```bash
./WikiQA.App --eval <suite-name>
```

Progress prints as each case completes (green = PASSED, red = FAILED, yellow = INCOMPLETE). A summary is printed at the end:

```
─────────────────────────────────────
  Total      : 6
  Pass       : 5
  Fail       : 1
  Incomplete : 0
  Score      : 83.3%
─────────────────────────────────────
```

---

## Eval Suites

### `basic`

A single smoke-test case. Confirms the agent can answer a factual question with Wikipedia grounding and return at least one source. Use this to verify the binary and API key are working.

```bash
./WikiQA.App --eval basic
```

---

### `deterministic_v1`

35 test cases with rule-based checks. No LLM judge — pass/fail is determined by exact rules:

- **Keyword matching** — expected terms must appear in the answer
- **Prohibited keywords** — certain terms must not appear
- **Expected Wikipedia URLs** — specific articles must be cited
- **Wiki-use expectations** — whether the agent should or should not call Wikipedia
- **Minimum search counts** — how many Wikipedia searches are expected

Categories covered:

| Category | What it tests |
|---|---|
| `Efficiency` | Answers from knowledge when Wikipedia is unnecessary |
| `FactRetrieval` | Grounded factual accuracy from Wikipedia |
| `HandleAmbiguity` | Searches before clarifying ambiguous queries |
| `HandleNegativeFacts` | Correctly identifies things that did not happen |
| `MultiHop` | Chains multiple searches for complex questions |

```bash
./WikiQA.App --eval deterministic_v1
```

---

### `heuristic_v1`

5 test cases evaluated by two automated heuristics (no LLM involved):

- **Wikipedia URL Validation** — every cited URL must match `en.wikipedia.org/wiki/`
- **Word Overlap** — the answer must share at least 10 words with the retrieved Wikipedia snippets, confirming the agent is grounding its response rather than hallucinating

```bash
./WikiQA.App --eval heuristic_v1
```

---

### `llm_judge_v1`

6 test cases evaluated by Claude Haiku acting as an independent judge. The judge runs **3 times per case** (pass@3) and scores each metric 1–5, reporting the best score with a 95% confidence interval.

Metrics:

| Metric | What it measures |
|---|---|
| `Groundedness` | Claims are supported by Wikipedia snippets |
| `Relevance` | Answer addresses the actual question |
| `Coherence` | Response is logically organized |
| `Precision` | Concise and direct, no filler |
| `Recall` | Captures the key facts from the source |
| `Completeness` | Fully resolves the user's query |

Pass criteria: all metrics must achieve PassAtK ≥ 3. A case hard-fails if any metric scores < 2.

Per-case output shows mean, confidence interval, and PassAtK for each metric:
```
  Judge Metrics (pass@3):
    Groundedness    : 4.33 [3.95, 4.71]  PassAtK=5
    Relevance       : 5.00 [5.00, 5.00]  PassAtK=5
```

```bash
./WikiQA.App --eval llm_judge_v1
```

---

### `safety_v1`

5 test cases evaluated by Claude Haiku using **strict pass@3** — all 3 judge runs must pass for a metric to pass (no majority vote).

Test categories:

| Category | What it tests |
|---|---|
| `Harmful` | Agent must refuse requests for dangerous instructions |
| `Bias` | Agent must present balanced views on political topics |
| `FactualSafety` | Agent must defer to professionals on medical/legal topics |
| `PromptInjection` | Agent must resist attempts to hijack its behavior |
| `Sycophancy` | Agent must not validate false premises or flatter the user |

Metrics: `Harmlessness`, `Fairness`, `RefusalAccuracy`, `Privacy`, `FactualSafety`, `SycophancyResistance`.

Per-case output shows pass/fail per metric across all 3 runs:
```
  Safety Metrics (pass@3, strict):
    ✓ Harmlessness         : [T,T,T]
    ✓ RefusalAccuracy      : [T,T,T]
```

```bash
./WikiQA.App --eval safety_v1
```

---

## Where to Find Results

### Eval Results (`eval-results/`)

Every eval run writes a JSON file to `eval-results/` in the working directory (wherever you run the binary from). File names include the suite name and a run ID:

```
eval-results/
  deterministic_v1_72e1a584.json
  llm_judge_v1_bc794ad0.json
  safety_v1_fb2b58f4.json
```

Each file contains the full run: metadata, per-case results, scores, failure reasons, and judge reasoning.

### Transcripts (`transcripts/`)

Every agent run (demo or eval) writes a detailed transcript to `transcripts/`. Each transcript captures the full conversation — tool calls, Wikipedia search results, and model reasoning — for a single query.

```
transcripts/
  safety_v1_tc801_fb2b58f4_c5968d19.json
  llm_judge_v1_tc701_7b38fa3c_e25ef157.json
```

File names encode `<suite>_<caseId>_<runId>_<correlationId>`. The path to each case's transcript is also recorded in the eval result JSON under `TranscriptPath`.

---

## Test Suites

The suite definitions live alongside the binary in the `Suites/` folder:

```
Suites/
  basic.json
  deterministic_v1.json
  heuristic_v1.json
  llm_judge_v1.json
  safety_v1.json
```

Each suite is a JSON file defining the eval type and an array of test cases. You can inspect or copy these to understand the test structure or author new suites.
