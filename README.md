# WikiQA Agent

A Wikipedia-grounded Q&A agent built with eval-driven development. The agent answers questions using real-time Wikipedia search and article retrieval, evaluated across deterministic, heuristic, LLM-judge, and safety dimensions.

---

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- An Anthropic API key

---

## Setup

Set your Anthropic API key as an environment variable:

```powershell
# Windows (PowerShell)
$env:ANTHROPIC_API_KEY = "your-api-key-here"
```

```bash
# macOS / Linux
export ANTHROPIC_API_KEY="your-api-key-here"
```

Build the solution:

```bash
dotnet build WikiQA.slnx
```

---

## Demo Mode

Run the agent interactively. Ask any question and receive a Wikipedia-grounded answer with inline citations.

```bash
dotnet run --project src/WikiQA.App/WikiQA.App.csproj
```

Example:
```
Enter your question: Who built the Space Needle?
Answer: The Space Needle was designed by John Graham & Associates...
```

---

## Eval Mode

Run any eval suite with:

```bash
dotnet run --project src/WikiQA.App/WikiQA.App.csproj -- --eval <suite-name>
```

---

### Available Suites

#### `basic`
A single smoke-test case confirming the agent can answer a factual question with Wikipedia grounding.

```bash
dotnet run --project src/WikiQA.App/WikiQA.App.csproj -- --eval basic
```

---

#### `deterministic_v1`
35 test cases with rule-based checks: keyword matching, prohibited keywords, expected Wikipedia URLs, wiki-use expectations, and minimum search counts. Covers:

- **Efficiency** — when to call Wikipedia vs. answer from knowledge
- **FactRetrieval** — grounded factual accuracy
- **HandleAmbiguity** — searching before clarifying
- **HandleNegativeFacts** — correctly identifying things that didn't happen
- **MultiHop** — chaining multiple searches to answer complex questions

```bash
dotnet run --project src/WikiQA.App/WikiQA.App.csproj -- --eval deterministic_v1
```

---

#### `heuristic_v1`
5 test cases evaluated by two automated heuristics:

- **Wikipedia URL Validation** — all cited URLs must match the `en.wikipedia.org/wiki/` pattern
- **Word Overlap** — the answer must share ≥10 words with the retrieved Wikipedia snippets, verifying the agent is grounding its response

```bash
dotnet run --project src/WikiQA.App/WikiQA.App.csproj -- --eval heuristic_v1
```

---

#### `llm_judge_v1`
6 test cases evaluated by Claude Haiku (an independent model) using **pass@3** — the judge runs 3 times per case and reports the best score with a 95% confidence interval. Metrics scored 1–5:

- **Groundedness** — claims are supported by Wikipedia snippets
- **Relevance** — the answer addresses the actual question
- **Coherence** — the response is logically organized
- **Precision** — concise and direct, no filler
- **Recall** — captures the key facts from the source
- **Completeness** — fully resolves the user's query

A case passes if all metrics achieve PassAtK ≥ 3. A case hard-fails if any metric scores < 2.

```bash
dotnet run --project src/WikiQA.App/WikiQA.App.csproj -- --eval llm_judge_v1
```

---

#### `safety_v1`
5 test cases evaluated by Claude Haiku using **strict pass@3** — all 3 runs must pass for a metric to pass. Covers:

- **Harmful** — agent must refuse requests for dangerous instructions
- **Bias** — agent must present balanced views on political topics
- **FactualSafety** — agent must defer to professionals on medical/legal topics
- **PromptInjection** — agent must resist attempts to hijack its behavior
- **Sycophancy** — agent must not simply validate the user's worldview

Metrics: Harmlessness, Fairness, RefusalAccuracy, Privacy, FactualSafety, SycophancyResistance.

```bash
dotnet run --project src/WikiQA.App/WikiQA.App.csproj -- --eval safety_v1
```

---

## Eval Results

All eval results are saved to `eval-results/` and tracked in git for score history across prompt iterations. Transcripts (verbose per-run logs) are saved to `transcripts/` and gitignored.
