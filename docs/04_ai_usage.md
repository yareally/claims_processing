# Using AI during the run

## Rules for timed runs

Use no AI outside the exercise: no Claude Code, ChatGPT, Copilot, or AI in a browser.

Someone who took an earlier Ropes assessment reports that outside AI is not allowed and is watched for, and that the built-in tool mainly answered questions about the language. That was a different assessment, so this one may differ. Practise for the harder case.

During a timed run, allow yourself language questions only:

| Allowed: about the language | Not allowed: about the design or your code |
|---|---|
| How do I remove the top item from a `Stack<T>`? | How should I compensate failed steps? |
| What does `with` do on a C# record? | Write `SubmitAsync` for me. |
| How do I return a 503 from a minimal API? | Why does this test fail? |

After a run, use any AI you like to explain what went wrong.

## On the day

1. Read the assessment's own rules. They override this page.
2. Stay inside the platform, and use only the AI tool it provides.
3. In Stage 1, ask the tool one language question and one design question. The answers show what it can do.
4. If it answers only language questions, use it for lookups and make every design decision yourself.
5. If it can write code, follow the advice below. The invitation lists "AI Usage Quality" as a tested skill, so how you use it may count.

## If the tool can write code

- **Plan first, then ask.** Decide the design yourself, then give the tool one small task at a time.
- **Give it the rules.** Include the relevant acceptance criteria and method signatures in your prompt.
- **Read every line before you accept it.** If you cannot explain a line, ask about it or rewrite it.
- **Run the tests after each change.** Passing tests are evidence. The tool saying the code works is not.
- **Say aloud what you changed and why.** For example: "It passed the caller's token to the undo call. I changed that, because a disconnect would leave the money reserved."

Avoid pasting the whole brief and accepting the whole answer, or asking "fix this" again and again without reading the error.

A weak prompt:

> Implement the claim workflow.

A better prompt:

> In this C# method, after `CreateReserveAsync` succeeds I want to record how to undo it, so that a later failure can release the reserve. The undo must not use the caller's cancellation token. Show me only the lines to add, and explain why you chose that data structure.
